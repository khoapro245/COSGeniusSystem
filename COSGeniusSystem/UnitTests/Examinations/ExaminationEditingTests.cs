using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;

namespace COSGeniusSystem.UnitTests;

public class ExaminationEditingTests
{
    private static ClaimsPrincipal Actor(int id = 1) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, RoleConstants.Doctor)
    }, "test"));

    [Theory]
    [InlineData("DRAFT", "DRAFT")]
    [InlineData("SENT", "CONFIRMED")]
    public async Task SavedContentCanBeEditedUntilCompletion(string prescriptionStatus, string planStatus)
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Doctor.User!.IsActive = true;
        record.Status = record.Registration.Status = "IN_PROGRESS";
        record.Symptoms = "Symptoms";
        var herb = new Herb { HerbId = 1, HerbCode = "H1", CommonName = "Herb", Unit = "g", IsActive = true };
        var service = new COSGeniusSystem.Models.Service { ServiceId = 1, ServiceCode = "S1", ServiceName = "Service", Unit = "session", IsActive = true, Price = 10 };
        var prescription = new HerbalPrescription { PrescriptionId = 1, Record = record, CreatedByNavigation = record.Doctor.User, Status = prescriptionStatus, TotalDoses = 1, UsageInstructions = "Use" };
        var plan = SchemaFixture.Plan(record); plan.Status = planStatus;
        db.MedicalRecords.Add(record); db.Herbs.Add(herb); db.Services.Add(service); db.HerbalPrescriptions.Add(prescription); db.TreatmentPlans.Add(plan);
        await db.SaveChangesAsync();
        var api = new ExaminationService(db);
        var medicine = new SavePrescriptionRequest { RowVersion = prescription.RowVersion, TotalDoses = 2, UsageInstructions = "Use twice", Items = [new() { HerbId = 1, DosagePerDose = 3 }] };
        var treatment = new SaveTreatmentPlanRequest { RowVersion = plan.RowVersion, PlanName = "Edited", TreatmentType = "SINGLE", PlannedSessionCount = 1, Items = [new() { ServiceId = 1, PlannedQuantity = 1 }] };
        await api.SavePrescriptionAsync(Actor(), 1, 1, medicine, default);
        await api.SavePlanAsync(Actor(), 1, 1, treatment, default);
        medicine.TotalDoses = 4;
        await api.SavePrescriptionAsync(Actor(), 1, 1, medicine, default);
        Assert.Equal(4, prescription.TotalDoses);
        Assert.Equal("Edited", plan.PlanName);
        await Assert.ThrowsAsync<ForbiddenException>(() => api.SavePlanAsync(Actor(99), 1, 1, treatment, default));
        var stale = new SavePrescriptionRequest { RowVersion = [1,1,1,1,1,1,1,1], TotalDoses = 1 };
        await Assert.ThrowsAsync<ConflictException>(() => api.SavePrescriptionAsync(Actor(), 1, 1, stale, default));
        await api.CompleteAsync(Actor(), 1, new VersionRequest { RowVersion = record.RowVersion }, default);
        Assert.Equal("SENT", prescription.Status);
        Assert.Equal("CONFIRMED", plan.Status);
        Assert.Equal("COMPLETED", record.Status);
        await Assert.ThrowsAsync<ConflictException>(() => api.SavePrescriptionAsync(Actor(), 1, 1, medicine, default));
        await Assert.ThrowsAsync<ConflictException>(() => api.SavePlanAsync(Actor(), 1, 1, treatment, default));
    }
}
