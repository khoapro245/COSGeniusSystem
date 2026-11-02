using COSGeniusSystem.Models;
using COSGeniusSystem.Services;

namespace COSGeniusSystem.UnitTests;

public class ReceptionDashboardServiceTests
{
    [Fact]
    public async Task Stats_UseRegistrationAndSessionStatus_InsteadOfDiagnosisOrNotes()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        record.Registration.Status = "WAITING";
        record.FinalDiagnosis = "Chờ khám";
        var plan = SchemaFixture.Plan(record);
        var date = record.Registration.BusinessDate;
        db.TreatmentPlans.Add(plan);
        db.TreatmentSessions.AddRange(
            new TreatmentSession { SessionId = 1, Plan = plan, SessionNumber = 1, ScheduledDate = date, Status = "REQUESTED", ResultNote = "Has a note" },
            new TreatmentSession { SessionId = 2, Plan = plan, SessionNumber = 2, ScheduledDate = date, Status = "COMPLETED" },
            new TreatmentSession { SessionId = 3, Plan = plan, SessionNumber = 3, ScheduledDate = date, Status = "CANCELLED" });
        await db.SaveChangesAsync();
        var result = await new ReceptionDashboardService(db).GetTodayStatsAsync();
        Assert.Equal(1, result.WaitingExaminationCount);
        Assert.Equal(1, result.WaitingTreatmentCount);
        Assert.Equal(1, result.CompletedVisitsCount);
        Assert.Equal(3, result.TodayTotalVisits);
    }

    [Fact]
    public async Task Queues_IncludeWaitingRegistrationWithoutMedicalRecord_AndRequestedSessionWithoutUsage()
    {
        using var db = SchemaFixture.Create();
        var record = SchemaFixture.Record();
        var plan = SchemaFixture.Plan(record);
        db.TreatmentPlans.Add(plan);
        var waiting = new ExaminationRegistration
        {
            RegistrationId = 2, Patient = record.Patient, Doctor = record.Doctor,
            RegisteredByNavigation = record.Doctor.User!, RegistrationCode = "KCB00002",
            BusinessDate = record.Registration.BusinessDate, QueueNumber = 2,
            RegisteredAt = DateTime.UtcNow, Status = "WAITING", IsPriority = true
        };
        db.ExaminationRegistrations.Add(waiting);
        db.TreatmentSessions.Add(new TreatmentSession
        {
            SessionId = 1, Plan = plan, SessionNumber = 1, ScheduledDate = waiting.BusinessDate,
            Status = "REQUESTED", AssignedDoctor = record.Doctor, RequestedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var result = await new ReceptionDashboardService(db).GetQuickQueuesAsync();
        Assert.Equal(2, Assert.Single(result.WaitingExaminations).RegistrationId);
        Assert.Equal(1, Assert.Single(result.WaitingTreatments).SessionId);
    }
}
