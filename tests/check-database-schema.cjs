// Static cross-check of table/column mappings, including EF convention scalar properties.
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const sql = fs.readFileSync(path.join(root, 'database.sql'), 'utf16le');
const context = fs.readFileSync(path.join(root, 'COSGeniusSystem/Data/ApplicationDbContext.cs'), 'utf8');
const tables = new Map();
for (const match of sql.matchAll(/CREATE TABLE \[dbo\]\.\[([^\]]+)\]\(([\s\S]*?)\n\s*(?:CONSTRAINT|PRIMARY KEY|UNIQUE)/g)) {
    const columns = new Map([...match[2].matchAll(/^\s*\[([^\]]+)\]\s+(?:\[([^\]]+)\]|(AS))([^\r\n]*)/gm)]
        .map(m => [m[1].toLowerCase(), { name: m[1], type: m[2] ?? 'computed', nullable: !m[4].includes('NOT NULL') }]));
    tables.set(match[1].toLowerCase(), columns);
}
let errors = [];
let count = 0;
for (const match of context.matchAll(/modelBuilder.Entity<(\w+)>\(entity =>\s*\{([\s\S]*?)\n        \}\);/g)) {
    const [, type, config] = match;
    const table = /\.ToTable\("([^"]+)"/.exec(config)?.[1] ?? type;
    const columns = tables.get(table.toLowerCase());
    if (!columns) { errors.push(`Missing table: ${type} -> ${table}`); continue; }
    const model = fs.readFileSync(path.join(root, `COSGeniusSystem/Models/${type}.cs`), 'utf8');
    const mapped = new Set();
    for (const property of model.matchAll(/public (int|short|long|bool|decimal|double|float|DateTime|DateOnly|TimeOnly|string|byte\[\])(\?)? (\w+) \{ get; set; \}/g)) {
        const [, clr, nullable, name] = property;
        const escaped = name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        const mapping = new RegExp(`entity.Property\\(e => e\\.${escaped}\\)([\\s\\S]*?);`).exec(config)?.[1] ?? '';
        const column = /HasColumnName\("([^"]+)"\)/.exec(mapping)?.[1] ?? name;
        mapped.add(column.toLowerCase()); count++;
        const actual = columns.get(column.toLowerCase());
        if (!actual) errors.push(`Missing column: ${table}.${column} (${type}.${name})`);
        else if (actual.nullable !== !!nullable && !['string','byte[]'].includes(clr))
            errors.push(`Nullability mismatch: ${table}.${column}: SQL nullable=${actual.nullable}, CLR ${clr}${nullable ?? ''}`);
    }
    for (const [column, definition] of columns) if (!mapped.has(column)) errors.push(`Unmapped SQL column: ${table}.${definition.name}`);
}
if (tables.size !== 39) errors.push(`Expected 39 SQL tables, parsed ${tables.size}`);
if (errors.length) { console.error(errors.join('\n')); process.exitCode = 1; }
else console.log(`PASS: ${tables.size} tables and ${count} scalar column mappings match database.sql.`);
