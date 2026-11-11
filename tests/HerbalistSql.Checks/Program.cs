using System.Security.Claims;
using System.Text.Json;
using COSGeniusSystem.Controllers;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

try
{
var connection = Environment.GetEnvironmentVariable("HERBALIST_TEST_CONNECTION") ?? throw new Exception("Set HERBALIST_TEST_CONNECTION to an isolated HerbalistIntegration_* database.");
var cs = new SqlConnectionStringBuilder(connection);
if (!cs.InitialCatalog.StartsWith("HerbalistIntegration_", StringComparison.Ordinal)) throw new Exception("Refusing to write outside an isolated integration database.");
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options;
ApplicationDbContext Db() => new(options);
var checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
JsonElement Result(IActionResult result, int expected = 200)
{
    var obj = (ObjectResult)result;
    Check(obj.StatusCode == expected, $"Expected {expected}, got {obj.StatusCode}: {JsonSerializer.Serialize(obj.Value)}");
    return JsonSerializer.SerializeToElement(obj.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("data");
}
ControllerContext Context(int actor) => new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString()), new Claim(ClaimTypes.Role, "ADMIN")], "test")) } };
await using var setup = Db();
var actor = await setup.Users.Select(x => x.UserId).FirstAsync();
var warehouse = await setup.Warehouses.Where(x => x.IsActive).Select(x => x.WarehouseId).FirstAsync();
var location = new WarehouseStorageLocation { WarehouseId = warehouse, Code = "INTEGRATION-" + Guid.NewGuid().ToString("N") };
setup.WarehouseStorageLocations.Add(location); await setup.SaveChangesAsync();
await setup.Batches.Where(b => b.WarehouseId == warehouse).ExecuteUpdateAsync(s => s.SetProperty(b => b.StorageLocationId, location.Id));
var ids = await setup.HerbalPrescriptions.Where(p => p.Status == "SENT" && p.Record.Status == "COMPLETED" && p.Record.Registration.Status == "COMPLETED" && p.MedicineDispense == null && !setup.HerbRequisitionPrescriptions.Any(l => l.PrescriptionId == p.PrescriptionId && l.IsActive)).Select(p => p.PrescriptionId).Take(2).ToArrayAsync();
Check(ids.Length == 2, "Fixture needs two eligible prescriptions.");
// Isolated fixture exercises a real 0.001g issue/receipt/completion, not only integer stock.
await setup.HerbalPrescriptions.Where(p => p.PrescriptionId == ids[0]).ExecuteUpdateAsync(s => s.SetProperty(p => p.TotalDoses, 1));
await setup.HerbalPrescriptionDetails.Where(d => d.PrescriptionId == ids[0]).ExecuteUpdateAsync(s => s.SetProperty(d => d.DosagePerDose, 0.001m));
await using var db = Db();
var herbalist = new HerbalistController(db) { ControllerContext = Context(actor) };
Result(await herbalist.ListPrescriptions(new(), default));
Result(await herbalist.Preview(new(ids, warehouse), default));
var created = Result(await herbalist.Create(new(ids, "SQL integration", warehouse), default));
var requestId = created.GetProperty("id").GetInt32();
Result(await herbalist.Requests(new(), default));
Result(await herbalist.RequestDetail(requestId, default));
await using (var duplicateDb = Db())
{
    var duplicate = new HerbalistController(duplicateDb) { ControllerContext = Context(actor) };
    Result(await duplicate.Create(new(ids, "duplicate", warehouse), default), 409);
}
await using var stockDb = Db();
var stock = new HerbalistWarehouseController(stockDb) { ControllerContext = Context(actor) };
Result(await stock.List(ct: default));
Result(await stock.Detail(requestId, default));
Result(await stock.EligibleBatches(requestId, default));
var suggestions = Result(await stock.Fefo(requestId, default));
var allocations = suggestions.EnumerateArray().SelectMany(h => h.GetProperty("lines").EnumerateArray()).Select(l => new HerbalistWarehouseController.AllocationInput(l.GetProperty("batchId").GetInt32(), l.GetProperty("quantityGrams").GetDecimal())).ToArray();
var header = await stockDb.HerbRequisitions.AsNoTracking().SingleAsync(r => r.Id == requestId);
var version = Convert.ToBase64String(header.RowVersion);
var originalStock = await stockDb.Batches.SumAsync(b => b.CurrentQuantity);
Result(await stock.Issue(requestId, new(version, allocations), default));
var issuedStock = await setup.Batches.SumAsync(b => b.CurrentQuantity);
Check(issuedStock == originalStock - allocations.Sum(a => a.QuantityGrams), "Gram stock must be deducted exactly once at issue.");
await using (var staleDb = Db())
{
    var stale = new HerbalistWarehouseController(staleDb) { ControllerContext = Context(actor) };
    Result(await stale.Issue(requestId, new(version, allocations), default), 409);
}
// Clear tracked graphs before another actor's writes.
db.ChangeTracker.Clear();
var issued = await db.HerbRequisitions.AsNoTracking().SingleAsync(r => r.Id == requestId);
var lines = await db.HerbOutboundDetails.AsNoTracking().Where(l => l.RequestId == requestId).ToArrayAsync();
Result(await herbalist.Receive(requestId, new(Convert.ToBase64String(issued.RowVersion), lines.Select(l => new ReceiptLine(l.Id, null, true)).ToArray(), null), default));
db.ChangeTracker.Clear();
foreach (var id in ids)
{
    var current = await db.HerbRequisitions.AsNoTracking().SingleAsync(r => r.Id == requestId);
    var need = await db.HerbRequisitionDetails.Where(d => d.RequestId == requestId && d.PrescriptionId == id).GroupBy(d => d.HerbId).Select(g => new { HerbId = g.Key, Quantity = g.Sum(d => d.RequestedQuantity) }).ToListAsync();
    var physical = need.Select(n => new DispenseAllocation(lines.First(l => l.HerbId == n.HerbId).Id, n.Quantity)).ToArray();
    Result(await herbalist.Complete(id, new(Convert.ToBase64String(current.RowVersion), physical), default));
    Result(await herbalist.PaymentEligibility(id, default));
    db.ChangeTracker.Clear();
}
Check(await db.MedicineDispenses.CountAsync(d => ids.Contains(d.PrescriptionId) && d.Status == "COMPLETED") == 2, "Two completed per-order dispense headers.");
Check(await db.HerbRequisitions.Where(r => r.Id == requestId).Select(r => r.Status).SingleAsync() == "COMPLETED", "Group completed after both orders.");
Check(await db.Batches.SumAsync(b => b.CurrentQuantity) == issuedStock, "Receipt and completion must never deduct stock again.");
Result(await herbalist.History(new(), default));
// Race two creators for the remaining fixture prescriptions on separate connections.
var raceIds = await db.HerbalPrescriptions.Where(p => p.Status == "SENT" && p.Record.Status == "COMPLETED" && p.Record.Registration.Status == "COMPLETED" && p.MedicineDispense == null && !db.HerbRequisitionPrescriptions.Any(l => l.PrescriptionId == p.PrescriptionId && l.IsActive)).Select(p => p.PrescriptionId).Take(2).ToArrayAsync();
Check(raceIds.Length == 2, "Race fixture needs two remaining prescriptions.");
using var barrier = new Barrier(2);
async Task<IActionResult> Race()
{
    await using var raceDb = Db();
    var controller = new HerbalistController(raceDb) { ControllerContext = Context(actor) };
    barrier.SignalAndWait();
    try { return await controller.Create(new(raceIds, "race", warehouse), default); }
    catch (Exception ex)
    {
        var action = new Microsoft.AspNetCore.Mvc.ActionContext(new DefaultHttpContext(), new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var context = new Microsoft.AspNetCore.Mvc.Filters.ExceptionContext(action, []) { Exception = ex };
        new COSGeniusSystem.Services.Herbalist.HerbalistConflictAttribute().OnException(context);
        if (!context.ExceptionHandled) throw;
        return context.Result!;
    }
}
var raced = await Task.WhenAll(Task.Run(Race), Task.Run(Race));
Check(raced.Cast<ObjectResult>().Count(x => x.StatusCode == 200) == 1 && raced.Cast<ObjectResult>().Count(x => x.StatusCode == 409) == 1, "Exactly one concurrent creator must win.");
Check(await db.HerbRequisitionPrescriptions.CountAsync(l => raceIds.Contains(l.PrescriptionId) && l.IsActive) == 2, "No duplicate effective prescription links after concurrent creation.");
var raceId = await db.HerbRequisitionPrescriptions.Where(l => l.PrescriptionId == raceIds[0] && l.IsActive).Select(l => l.RequestId).SingleAsync();
stockDb.ChangeTracker.Clear();
var raceSuggestions = Result(await stock.Fefo(raceId, default));
var raceAllocations = raceSuggestions.EnumerateArray().SelectMany(h => h.GetProperty("lines").EnumerateArray()).Select(l => new HerbalistWarehouseController.AllocationInput(l.GetProperty("batchId").GetInt32(), l.GetProperty("quantityGrams").GetDecimal())).ToArray();
var raceHeader = await stockDb.HerbRequisitions.AsNoTracking().SingleAsync(r => r.Id == raceId);
Result(await stock.Issue(raceId, new(Convert.ToBase64String(raceHeader.RowVersion), raceAllocations), default));
db.ChangeTracker.Clear();
raceHeader = await db.HerbRequisitions.AsNoTracking().SingleAsync(r => r.Id == raceId);
var discrepancyLines = await db.HerbOutboundDetails.AsNoTracking().Where(l => l.RequestId == raceId).ToArrayAsync();
// NULL stays unchecked, checked zero is saved and locks the discrepant source.
Result(await herbalist.Receive(raceId, new(Convert.ToBase64String(raceHeader.RowVersion), [new(discrepancyLines[0].Id, null)], null), default));
db.ChangeTracker.Clear();
Check(await db.HerbOutboundDetails.Where(l => l.Id == discrepancyLines[0].Id).Select(l => l.ReceivedQuantity).SingleAsync() == null, "NULL receipt preserved in SQL.");
raceHeader = await db.HerbRequisitions.AsNoTracking().SingleAsync(r => r.Id == raceId);
Result(await herbalist.Receive(raceId, new(Convert.ToBase64String(raceHeader.RowVersion), discrepancyLines.Select(l => new ReceiptLine(l.Id, 0)).ToArray(), "test shortage"), default));
db.ChangeTracker.Clear();
Check(await db.HerbReceiptDiscrepancies.AnyAsync(r => r.OutboundId == discrepancyLines[0].OutboundId), "Discrepancy report persisted.");
raceHeader = await db.HerbRequisitions.AsNoTracking().SingleAsync(r => r.Id == raceId);
var raceNeed = await db.HerbRequisitionDetails.Where(d => d.RequestId == raceId && d.PrescriptionId == raceIds[0]).GroupBy(d => d.HerbId).Select(g => new { HerbId = g.Key, Quantity = g.Sum(d => d.RequestedQuantity) }).ToListAsync();
Result(await herbalist.Complete(raceIds[0], new(Convert.ToBase64String(raceHeader.RowVersion), raceNeed.Select(n => new DispenseAllocation(discrepancyLines.First(l => l.HerbId == n.HerbId).Id, n.Quantity)).ToArray()), default), 409);
Check(!await db.MedicineDispenses.AnyAsync(m => m.PrescriptionId == raceIds[0]), "Rejected discrepant completion leaves no dispense header.");
db.ChangeTracker.Clear();
await using (var budgetTx = await db.Database.BeginTransactionAsync())
{
    var bypass = new MedicineDispense { PrescriptionId = raceIds[0], WarehouseId = warehouse, DispensedBy = actor, DispenseDate = DateTime.UtcNow, Status = "COMPLETED", HerbOutboundId = discrepancyLines[0].OutboundId };
    db.MedicineDispenses.Add(bypass); await db.SaveChangesAsync();
    db.DispenseDetails.Add(new DispenseDetail { DispenseId = bypass.DispenseId, BatchId = discrepancyLines[0].BatchId, HerbId = discrepancyLines[0].HerbId, HerbOutboundDetailId = discrepancyLines[0].Id, QuantityTaken = 0.001m });
    try { await db.SaveChangesAsync(); throw new Exception("Receipt budget trigger allowed a discrepant source."); }
    catch (DbUpdateException ex) when (ex.GetBaseException() is SqlException sql && sql.Number == 51012) { checks++; }
    await budgetTx.RollbackAsync();
}
Console.WriteLine($"PASS: {checks} SQL Server controller, EF query, transaction and stale-version checks; database {cs.InitialCatalog}.");

}
catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
