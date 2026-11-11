using COSGeniusSystem.Services;
using COSGeniusSystem.Models;
using COSGeniusSystem.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace COSGeniusSystem.UnitTests;
public class InvoiceAccrualTests
{
    [Fact]
    public async Task CancelledSingleDebt_PreservesPriceWhenMoreUsageBecomesEligible()
    {
        using var db = SchemaFixture.Create(); var record = SchemaFixture.Record();
        var single = Service(record, 1, "SINGLE", 3);
        db.TreatmentPlanDetails.Add(single);
        db.Invoices.Add(new Invoice { Record = record, PaymentStatus = "CANCELLED", PaymentMethod = "",
            TotalAmount = 140000, FinalAmount = 140000,
            InvoiceDetails = [new InvoiceDetail { ItemType = "SERVICE", PlanDetail = single, SourceKey = "SERVICE:1:2",
                ItemName = "Giá cũ", Quantity = 2, UnitPrice = 70000, IsActive = false }] });
        db.UsageHistories.Add(new UsageHistory { PlanDetail = single, Quantity = 3, PerformedByNavigation = record.Doctor,
            Session = new TreatmentSession { Plan = single.Plan, Status = "COMPLETED", SessionNumber = 1 } });
        await db.SaveChangesAsync(); await InvoiceAccrual.GenerateAsync(db, record.RecordId);
        var invoice = await db.Invoices.Include(x => x.InvoiceDetails).SingleAsync(x => x.PaymentStatus == "UNPAID");
        Assert.Equal(240000, invoice.TotalAmount);
        Assert.Equal(2, invoice.InvoiceDetails.Count);
        await InvoiceAccrual.GenerateAsync(db, record.RecordId);
        Assert.Equal(2, await db.Invoices.CountAsync());
    }
    private static TreatmentPlanDetail Service(MedicalRecord record, int id, string type, int quantity = 3) => new() {
        PlanDetailId = id, ServiceNameSnapshot = "Châm cứu", UnitSnapshot = "lần", PlannedQuantity = quantity, UnitPrice = 100000,
        Plan = new TreatmentPlan { PlanId = id, Record = record, Doctor = record.Doctor, PlanName = type, TreatmentType = type,
            Status = "CONFIRMED", ConfirmedAt = DateTime.UtcNow, PlannedSessionCount = quantity }
    };
    [Fact]
    public async Task MixedRecord_AccruesAtDifferentTimes_RetryAndPaidInvoiceArePreserved()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record(); record.Status = "IN_PROGRESS";
        var course = Service(record, 1, "COURSE"); var single = Service(record, 2, "SINGLE", 1);
        var rx = new HerbalPrescription { PrescriptionId = 1, Record = record, CreatedByNavigation = record.Doctor.User!, Status = "SENT", TotalDoses = 5,
            HerbalPrescriptionDetails = [new HerbalPrescriptionDetail { HerbNameSnapshot = "Cam thảo", DosagePerDose = 10,
                Herb = new Herb { HerbId = 1, HerbCode = "CT", CommonName = "Cam thảo", Unit = "kg", StandardPrice = 200000 } }] };
        db.AddRange(course, single, rx); await db.SaveChangesAsync();
        await InvoiceAccrual.GenerateAsync(db, 1); Assert.Empty(await db.Invoices.ToListAsync());
        record.Status = "COMPLETED"; await db.SaveChangesAsync(); await InvoiceAccrual.GenerateAsync(db, 1);
        var first = Assert.Single(await db.Invoices.Include(i => i.InvoiceDetails).ToListAsync());
        Assert.Equal(300000, first.FinalAmount); Assert.Null(first.CashierId);
        Assert.Equal("COURSE", Assert.Single(first.InvoiceDetails).TreatmentTypeSnapshot);
        await InvoiceAccrual.GenerateAsync(db, 1); Assert.Equal(1, await db.Invoices.CountAsync());
        first.PaymentStatus = "PAID"; await db.SaveChangesAsync();
        rx.MedicineDispense = new MedicineDispense { DispensedByNavigation = record.Doctor.User!, Status = "COMPLETED", Warehouse = new Warehouse { WarehouseName = "Kho", WarehouseCode = "K" } };
        await db.SaveChangesAsync(); await InvoiceAccrual.GenerateAsync(db, 1);
        var medicine = Assert.Single(await db.InvoiceDetails.Where(i => i.PrescriptionId != null).ToListAsync());
        Assert.Equal(10000, medicine.UnitPrice); Assert.Equal(1, medicine.Quantity);
        var ingredient = Assert.Single(JsonSerializer.Deserialize<List<InvoiceIngredient>>(medicine.IngredientsJson!)!);
        Assert.Equal(50, ingredient.Quantity); Assert.Equal(200, ingredient.UnitPrice);
        rx.HerbalPrescriptionDetails.First().Herb.StandardPrice = 999999; await db.SaveChangesAsync();
        await InvoiceAccrual.GenerateAsync(db, 1); Assert.Equal(10000, medicine.UnitPrice);
        var session = new TreatmentSession { SessionId = 1, Plan = single.Plan, Status = "IN_PROGRESS", SessionNumber = 1 };
        db.UsageHistories.Add(new UsageHistory { Session = session, PlanDetail = single, PerformedByNavigation = record.Doctor, Quantity = 1 });
        await db.SaveChangesAsync(); await InvoiceAccrual.GenerateAsync(db, 1); Assert.Equal(2, await db.Invoices.CountAsync());
        session.Status = "COMPLETED"; await db.SaveChangesAsync(); await InvoiceAccrual.GenerateAsync(db, 1);
        Assert.Equal(3, await db.Invoices.CountAsync());
        await InvoiceAccrual.GenerateAsync(db, 1); Assert.Equal(3, await db.Invoices.CountAsync());
        Assert.Equal(300000, first.FinalAmount); Assert.Single(first.InvoiceDetails);
        Assert.Equal(3, await db.Invoices.CountAsync(i => i.Record.PatientId == record.PatientId));
    }
    [Fact]
    public async Task Single_OnlyBillsCompletedUsage_AndNeverMultipliesPlannedSessions()
    {
        using var db = SchemaFixture.Create(); var record = SchemaFixture.Record(); var single = Service(record, 1, "SINGLE", 5);
        var session = new TreatmentSession { SessionId = 1, Plan = single.Plan, Status = "COMPLETED", SessionNumber = 1 };
        db.UsageHistories.Add(new UsageHistory { Session = session, PlanDetail = single, PerformedByNavigation = record.Doctor, Quantity = 2 });
        await db.SaveChangesAsync(); await InvoiceAccrual.GenerateAsync(db, 1);
        var first = Assert.Single(await db.Invoices.Include(i => i.InvoiceDetails).ToListAsync());
        Assert.Equal(200000, first.TotalAmount); first.PaymentStatus = "PAID"; await db.SaveChangesAsync();
        db.UsageHistories.Add(new UsageHistory { Session = new TreatmentSession { Plan = single.Plan, Status = "COMPLETED", SessionNumber = 2 }, PlanDetail = single, PerformedByNavigation = record.Doctor, Quantity = 3 });
        await db.SaveChangesAsync(); await InvoiceAccrual.GenerateAsync(db, 1);
        Assert.Equal(500000, await db.Invoices.SumAsync(i => i.TotalAmount));
        Assert.Equal(2, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task CancelledDebt_ReissuesOnlyWhenEligible_UsingOriginalPrice()
    {
        using var db = SchemaFixture.Create(); var record = SchemaFixture.Record(); var single = Service(record, 1, "SINGLE", 1);
        db.TreatmentPlanDetails.Add(single);
        db.Invoices.Add(new Invoice { Record = record, PaymentStatus = "CANCELLED", PaymentMethod = "", TotalAmount = 70000, FinalAmount = 70000,
            InvoiceDetails = [new InvoiceDetail { ItemType = "SERVICE", PlanDetail = single, ItemName = "Giá cũ", UnitPrice = 70000, Quantity = 1, IsActive = false }] });
        await db.SaveChangesAsync(); await InvoiceAccrual.GenerateAsync(db, record.RecordId);
        Assert.Equal(1, await db.Invoices.CountAsync());
        db.UsageHistories.Add(new UsageHistory { PlanDetail = single, Quantity = 1, PerformedByNavigation = record.Doctor,
            Session = new TreatmentSession { Plan = single.Plan, Status = "COMPLETED", SessionNumber = 1 } });
        await db.SaveChangesAsync(); await InvoiceAccrual.GenerateAsync(db, record.RecordId);
        var reissued = await db.Invoices.Include(i => i.InvoiceDetails).SingleAsync(i => i.PaymentStatus == "UNPAID");
        Assert.Equal(70000, reissued.TotalAmount); Assert.Equal("Giá cũ", Assert.Single(reissued.InvoiceDetails).ItemName);
        await InvoiceAccrual.GenerateAsync(db, record.RecordId); Assert.Equal(2, await db.Invoices.CountAsync());
    }
}
