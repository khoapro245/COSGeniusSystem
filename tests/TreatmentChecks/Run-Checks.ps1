param([string]$BackendRoot = 'D:\NET\COSGeniusSystem')
$ErrorActionPreference = 'Stop'
$changes = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$sdkInfo = & dotnet --list-sdks | Where-Object { $_ -match '^9\.' } | Select-Object -Last 1
if (!$sdkInfo) { throw 'Offline compatibility check needs the installed .NET 9 SDK.' }
$sdkVersion = ($sdkInfo -split ' ')[0]
$sdkRoot = ($sdkInfo -replace '^.*\[','' -replace '\]$','')
$dotnetRoot = Split-Path $sdkRoot -Parent
$compiler = Join-Path $sdkRoot "$sdkVersion/Roslyn/bincore"
$outDir = Join-Path $PSScriptRoot 'obj/offline'
New-Item -ItemType Directory -Force $outDir | Out-Null
$references = @()
foreach ($pack in @('Microsoft.NETCore.App.Ref', 'Microsoft.AspNetCore.App.Ref')) {
    $ref = Get-ChildItem (Join-Path $dotnetRoot "packs/$pack") -Directory | Where-Object Name -Like '9.*' | Sort-Object { [version]$_.Name } | Select-Object -Last 1
    $references += Get-ChildItem (Join-Path $ref.FullName 'ref/net9.0') -Filter *.dll | ForEach-Object { '/r:"' + $_.FullName + '"' }
}
foreach ($package in @('microsoft.entityframeworkcore', 'microsoft.entityframeworkcore.abstractions', 'microsoft.entityframeworkcore.relational')) {
    $dlls = Get-ChildItem (Join-Path $env:USERPROFILE ".nuget/packages/$package/9.0.9/lib/net8.0") -Filter *.dll
    if (!$dlls) { throw "Missing cached $package 9.0.9 (no package restore performed)." }
    $references += $dlls | ForEach-Object { '/r:"' + $_.FullName + '"' }
    $dlls | Copy-Item -Destination $outDir
}
$original = Join-Path $BackendRoot 'COSGeniusSystem'
$sources = @()
foreach ($dir in @('Models', 'DTOs/Examinations', 'DTOs/Patients', 'Exceptions')) {
    $sources += Get-ChildItem (Join-Path $original $dir) -Filter *.cs -Recurse | ForEach-Object { '"' + $_.FullName + '"' }
}
$sources += Get-ChildItem (Join-Path $original 'Commons') -Filter *.cs | ForEach-Object { '"' + $_.FullName + '"' }
foreach ($path in @('Controllers/BaseApiController.cs', 'Common/RoleConstants.cs', 'Services/Interfaces/IPatientService.cs')) {
    $sources += '"' + (Join-Path $original $path) + '"'
}
$sources += @('Controllers/TreatmentPlansController.cs','Controllers/TreatmentSessionsController.cs') | ForEach-Object { '"' + (Join-Path $original $_) + '"' }
$sources += Get-ChildItem (Join-Path $original 'DTOs/Treatments'),(Join-Path $original 'Services/Treatments') -Filter *.cs -Recurse | ForEach-Object { '"' + $_.FullName + '"' }
$sets = Select-String -Path (Join-Path $original 'Data/ApplicationDbContext.cs') -Pattern 'public virtual DbSet<' | ForEach-Object { $_.Line.Trim() + ' = null!;' }
# Type compatibility only: original model properties and DbSet surface, not mapping/SQL execution.
$stub = 'using COSGeniusSystem.Models; using Microsoft.EntityFrameworkCore; namespace COSGeniusSystem.Data; public class ApplicationDbContext : DbContext { public ApplicationDbContext() {} public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) {} ' + ($sets -join ' ') + ' }'
Set-Content (Join-Path $outDir 'ContextApi.cs') $stub
Set-Content (Join-Path $outDir 'GlobalUsings.cs') 'global using System; global using System.Collections.Generic; global using System.Linq; global using System.Threading; global using System.Threading.Tasks; global using Microsoft.Extensions.Configuration;'
$sources += @('ContextApi.cs','GlobalUsings.cs') | ForEach-Object { '"' + (Join-Path $outDir $_) + '"' }
$sources += '"' + (Join-Path $PSScriptRoot 'Program.cs') + '"'
$dll = Join-Path $outDir 'TreatmentChecks.dll'
$responseFile = Join-Path $outDir 'checks.rsp'
Set-Content $responseFile (@('/nologo','/target:exe','/nullable:enable',('/out:"' + $dll + '"')) + $references + $sources)
& dotnet (Join-Path $compiler 'csc.dll') "@$responseFile"
if ($LASTEXITCODE -ne 0) { throw 'Compatibility compilation failed.' }
Set-Content (Join-Path $outDir 'TreatmentChecks.runtimeconfig.json') '{"runtimeOptions":{"tfm":"net9.0","frameworks":[{"name":"Microsoft.NETCore.App","version":"9.0.0"},{"name":"Microsoft.AspNetCore.App","version":"9.0.0"}]}}'
& dotnet $dll
if ($LASTEXITCODE -ne 0) { throw 'Contract checks failed.' }

# Compile the SQL suite as well, without running it or touching a database.
$sqlProvider = Join-Path $env:USERPROFILE '.nuget/packages/microsoft.entityframeworkcore.sqlserver/9.0.9/lib/net8.0/Microsoft.EntityFrameworkCore.SqlServer.dll'
$sqlClient = Get-ChildItem (Join-Path $env:USERPROFILE '.nuget/packages/microsoft.data.sqlclient') -Filter Microsoft.Data.SqlClient.dll -Recurse | Where-Object FullName -Match 'ref\\net8.0\\' | Select-Object -First 1
if (!$sqlClient -or !(Test-Path $sqlProvider)) { throw 'Missing cached SQL reference assemblies for SQL suite compatibility compilation.' }
$sqlSources = $sources | Where-Object { $_ -ne ('"' + (Join-Path $PSScriptRoot 'Program.cs') + '"') }
$sqlSources += Get-ChildItem (Join-Path $original 'Services/Examinations') -Filter *.cs | ForEach-Object { '"' + $_.FullName + '"' }
foreach ($file in @('Services/PatientService.cs','Services/PatientMapping.cs')) { $sqlSources += '"' + (Join-Path $original $file) + '"' }
$sqlSources += '"' + (Join-Path $changes 'tests/TreatmentSqlChecks/Program.cs') + '"'
$sqlResponse = Join-Path $outDir 'sql-checks.rsp'
Set-Content $sqlResponse (@('/nologo','/target:exe','/nullable:enable',('/out:"' + (Join-Path $outDir 'SqlChecks.dll') + '"'),('/r:"' + $sqlProvider + '"'),('/r:"' + $sqlClient.FullName + '"')) + $references + $sqlSources)
& dotnet (Join-Path $compiler 'csc.dll') "@$sqlResponse"
if ($LASTEXITCODE -ne 0) { throw 'SQL suite compatibility compilation failed.' }
Write-Output 'SQL suite compatibility compilation passed (not executed).'
