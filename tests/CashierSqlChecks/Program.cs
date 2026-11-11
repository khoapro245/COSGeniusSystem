using System.Security.Claims;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
// Consumes one explicitly prepared eligible UNPAID test invoice. No seed/reset/migrations.
var connection = Environment.GetEnvironmentVariable("GENIUS_CASHIER_TEST_CONNECTION");
if (string.IsNullOrWhiteSpace(connection)) { Console.WriteLine("SKIPPED: dedicated Cashier SQL test environment is absent."); return 2; }
var builder = new SqlConnectionStringBuilder(connection);
if (!builder.InitialCatalog.StartsWith("GeniusCashierTest_", StringComparison.Ordinal)
    || Environment.GetEnvironmentVariable("GENIUS_CASHIER_ALLOW_TEST_WRITES") != builder.InitialCatalog)
    throw new InvalidOperationException("Refusing writes outside explicitly allowed GeniusCashierTest_ database.");
var invoiceId = int.Parse(Environment.GetEnvironmentVariable("GENIUS_CASHIER_TEST_INVOICE_ID") ?? throw new Exception("Prepare an eligible UNPAID invoice first."));
var cashierId = int.Parse(Environment.GetEnvironmentVariable("GENIUS_CASHIER_TEST_USER_ID") ?? throw new Exception("Supply an active CASHIER user ID."));
var secondCashierId = int.Parse(Environment.GetEnvironmentVariable("GENIUS_CASHIER_TEST_SECOND_USER_ID") ?? throw new Exception("Supply a second active CASHIER user ID."));
if (secondCashierId == cashierId) throw new Exception("Use two distinct cashier fixtures.");
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options;
ClaimsPrincipal Actor(string role, int? userId = null) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, (userId ?? cashierId).ToString()), new Claim(ClaimTypes.Role, role)], "Test"));
var count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; Console.WriteLine("PASS: " + name); }
async Task<InvoiceDetailDto> Read() { await using var db = new ApplicationDbContext(options); return await new InvoiceService(db).GetInvoiceByIdAsync(invoiceId); }
List<(int Id, decimal Quantity)> stockBefore;
await using (var db = new ApplicationDbContext(options)) stockBefore = (await db.Batches.AsNoTracking().OrderBy(b => b.BatchId).Select(b => new { b.BatchId, b.CurrentQuantity }).ToListAsync()).Select(b => (b.BatchId,b.CurrentQuantity)).ToList();
var before = await Read();
Check(before.PaymentStatus == "UNPAID" && before.Items.Count > 0, "Prepared invoice is unpaid and nonempty");
await using (var db = new ApplicationDbContext(options))
{
    Check(await db.Users.AnyAsync(u => u.UserId == cashierId && u.IsActive && u.UserRoles.Any(r => r.Role.RoleCode == "CASHIER")), "Active cashier fixture");
    Check(await db.Users.AnyAsync(u => u.UserId == secondCashierId && u.IsActive && u.UserRoles.Any(r => r.Role.RoleCode == "CASHIER")), "Second active cashier fixture");
    try { await new InvoiceService(db).PayAsync(Actor("DOCTOR"), invoiceId, new() { RowVersion = before.RowVersion, PaymentMethod = "Tiền mặt", ReceivedInFull = true }); throw new Exception("Doctor collected payment"); }
    catch (ForbiddenException) { Check(true, "Doctor cannot collect payment"); }
    try { await new InvoiceService(db).PayAsync(Actor("CASHIER"), invoiceId, new() { RowVersion = before.RowVersion, PaymentMethod = "Chuyển khoản", ReceivedInFull = false }); throw new Exception("Unconfirmed transfer accepted"); }
    catch (BadRequestException) { Check(true, "Transfer selection does not settle invoice"); }
    await new InvoiceService(db).CancelAsync(Actor("CASHIER"), invoiceId, new() { RowVersion = before.RowVersion, Reason = "Cashier SQL regression test" });
    var replacement = await db.Invoices.AsNoTracking().Where(i => i.RecordId == before.RecordId && i.PaymentStatus == "UNPAID").OrderByDescending(i => i.InvoiceId).FirstAsync();
    invoiceId = replacement.InvoiceId;
}
var snapshot = await Read();
Check(snapshot.FinalAmount == before.FinalAmount && snapshot.Items.Sum(i => i.SubTotal) == before.TotalAmount, "Cancellation preserves debt and snapshot totals");
async Task<int> Pay(int userId)
{
    await using var db = new ApplicationDbContext(options);
    try { await new InvoiceService(db).PayAsync(Actor("CASHIER", userId), invoiceId, new() { RowVersion = snapshot.RowVersion, PaymentMethod = "Chuyển khoản", ReceivedInFull = true }); return 200; }
    catch (ConflictException) { return 409; }
    catch (DbUpdateConcurrencyException) { return 409; }
    catch (SqlException e) when (e.Number is 1205 or 1222) { return 409; }
}
var results = await Task.WhenAll(Pay(cashierId), Pay(secondCashierId));
Check(results.Count(x => x == 200) == 1 && results.Count(x => x == 409) == 1, "Two cashiers settle exactly once");
Check(await Pay(cashierId) == 409, "Retry cannot collect twice");
var after = await Read();
Check(after.PaymentStatus == "PAID" && after.PaidAt.HasValue && after.CashierName.Length > 0, "Settlement records cashier, time and method");
Check(after.TotalAmount == snapshot.TotalAmount && after.FinalAmount == snapshot.FinalAmount, "Payment cannot reprice invoice");
Check(after.Items.Select(i => (i.ItemName,i.Quantity,i.UnitPrice,i.SubTotal)).SequenceEqual(snapshot.Items.Select(i => (i.ItemName,i.Quantity,i.UnitPrice,i.SubTotal))), "Reprint retains line snapshots");
await using (var db = new ApplicationDbContext(options))
{
    var stockAfter = (await db.Batches.AsNoTracking().OrderBy(b => b.BatchId).Select(b => new { b.BatchId, b.CurrentQuantity }).ToListAsync()).Select(b => (b.BatchId,b.CurrentQuantity)).ToList();
    Check(stockBefore.SequenceEqual(stockAfter), "Cancellation and payment do not deduct inventory");
}
Console.WriteLine($"{count} checks passed. SQL service tests only; HTTP/JWT/browser checks are separate.");
return 0;
