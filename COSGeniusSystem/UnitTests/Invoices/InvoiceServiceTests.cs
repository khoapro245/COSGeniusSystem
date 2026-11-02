using COSGeniusSystem.Models;
using COSGeniusSystem.Services;

namespace COSGeniusSystem.UnitTests.Invoices;

public class InvoiceServiceTests
{
    [Fact]
    public async Task Detail_ReturnsSeparatePrescriptionAndPlanDetailReferences()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        var invoice = new Invoice
        {
            InvoiceId = 1, Record = record, Cashier = record.Doctor.User!,
            PaymentMethod = "CASH", PaymentStatus = "PAID",
            InvoiceDetails =
            [
                new InvoiceDetail { InvoiceDetailId = 1, ItemType = "MEDICINE", ItemName = "Prescription", PrescriptionId = 9, Quantity = 1 },
                new InvoiceDetail { InvoiceDetailId = 2, ItemType = "SERVICE", ItemName = "Service", PlanDetailId = 12, Quantity = 1 }
            ]
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        var items = (await new InvoiceService(db).GetInvoiceByIdAsync(1)).Items;
        var medicine = Assert.Single(items, i => i.ItemType == "MEDICINE");
        Assert.Equal(9, medicine.PrescriptionId);
        Assert.Null(medicine.PlanDetailId);
        var service = Assert.Single(items, i => i.ItemType == "SERVICE");
        Assert.Equal(12, service.PlanDetailId);
        Assert.Null(service.PrescriptionId);
    }
}
