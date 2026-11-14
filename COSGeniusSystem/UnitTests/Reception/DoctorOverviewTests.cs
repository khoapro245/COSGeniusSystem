using COSGeniusSystem.DTOs;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services;

namespace COSGeniusSystem.UnitTests;

public class DoctorOverviewTests
{
    [Fact]
    public async Task Overview_SeparatesWaitingAndActiveTreatments_AndExcludesOtherDates()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Registration.Status = "WAITING";
        var plan = SchemaFixture.Plan(record);
        var date = record.Registration.BusinessDate;
        db.TreatmentPlans.Add(plan);
        db.TreatmentSessions.AddRange(
            new TreatmentSession { SessionId = 1, Plan = plan, SessionNumber = 1, AssignedDoctorId = 1, ScheduledDate = date, Status = "REQUESTED" },
            new TreatmentSession { SessionId = 2, Plan = plan, SessionNumber = 2, AssignedDoctorId = 1, PerformedBy = 2, ScheduledDate = date, Status = "IN_PROGRESS" },
            new TreatmentSession { SessionId = 3, Plan = plan, SessionNumber = 3, AssignedDoctorId = 1, ScheduledDate = date.AddDays(1), Status = "REQUESTED" },
            new TreatmentSession { SessionId = 4, Plan = plan, SessionNumber = 4, AssignedDoctorId = 1, ScheduledDate = date, Status = "COMPLETED" });
        db.Doctors.Add(new Doctor { DoctorId = 2, FullName = "Treating doctor", IsActive = true });
        await db.SaveChangesAsync();

        var overview = await new DoctorService(db).GetDoctorClinicOverviewAsync(new DoctorClinicFilterQuery { BusinessDate = date });

        Assert.Equal(1, overview.WaitingExaminationCount);
        Assert.Equal(1, overview.WaitingTreatmentCount);
        var assigned = Assert.Single(overview.Doctors, d => d.DoctorId == 1);
        Assert.Equal(1, assigned.WaitingCount);
        Assert.Equal(1, assigned.WaitingTreatmentCount);
        Assert.Empty(assigned.CurrentTreatingPatients);
        var performer = Assert.Single(overview.Doctors, d => d.DoctorId == 2);
        Assert.Equal(0, performer.WaitingTreatmentCount);
        Assert.Equal("Patient (BN00001)", Assert.Single(performer.CurrentTreatingPatients));
    }
}
