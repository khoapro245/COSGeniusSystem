using COSGeniusSystem.Data;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

// Model and graph checks require no database connection.
using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer("Server=localhost;Database=ModelChecks;Integrated Security=true;TrustServerCertificate=true")
    .Options);
if (db.Model.FindEntityType(typeof(DispenseDetail))!.FindProperty(nameof(DispenseDetail.QuantityTaken))!.GetColumnType() != "decimal(18,3)")
    throw new Exception("Scaffolded column type must not override dispense milligram precision.");
if (db.Model.FindEntityType(typeof(Batch))!.FindProperty(nameof(Batch.CurrentQuantity))!.GetColumnType() != "decimal(18,6)")
    throw new Exception("Scaffolded column type must not override stock conversion precision.");
var outboundDetailType = db.Model.FindEntityType(typeof(HerbOutboundDetail))!;
if (outboundDetailType.IsSqlOutputClauseUsed())
    throw new Exception("Outbound receipt writes must not use OUTPUT without INTO on a table with triggers.");
foreach (var dispenseType in new[] { typeof(MedicineDispense), typeof(DispenseDetail) })
{
    if (db.Model.FindEntityType(dispenseType)!.IsSqlOutputClauseUsed())
        throw new Exception($"Prescription completion writes to {dispenseType.Name} must support enabled triggers.");
}
var lineType = db.Model.FindEntityType(typeof(HerbRequisitionDetail))!;
var relationship = lineType.GetForeignKeys().SingleOrDefault(f =>
    f.PrincipalEntityType.ClrType == typeof(HerbRequisitionPrescription));
if (relationship == null || relationship.GetConstraintName() != "FK_HL_ReqLinePrescriptionV2"
    || !relationship.Properties.Select(p => p.Name).SequenceEqual(new[] { "RequestId", "PrescriptionId" })
    || !relationship.PrincipalKey.Properties.Select(p => p.Name).SequenceEqual(new[] { "RequestId", "PrescriptionId" }))
    throw new Exception("Requisition lines must depend on their request/prescription link.");

var request = new HerbRequisition
{
    Prescriptions = [new() { PrescriptionId = 11 }, new() { PrescriptionId = 12 }],
    Details = [new() { PrescriptionId = 11 }, new() { PrescriptionId = 11 }, new() { PrescriptionId = 12 }]
};
db.HerbRequisitions.Add(request);
foreach (var line in request.Details)
{
    var link = request.Prescriptions.Single(p => p.PrescriptionId == line.PrescriptionId);
    if (!Equals(db.Entry(line).Property(x => x.RequestId).CurrentValue,
        db.Entry(link).Property(x => x.RequestId).CurrentValue))
        throw new Exception("New lines and prescription links must share the generated request key.");
}
Console.WriteLine("PASS: trigger-compatible outbound and dispense mappings, composite foreign key and multi-prescription creation graph.");
