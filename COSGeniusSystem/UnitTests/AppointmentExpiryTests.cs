using COSGeniusSystem.Models;
using COSGeniusSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.UnitTests;

public class AppointmentExpiryTests
{
    [Theory]
    [InlineData("WAITING", "REQUESTED", -1, "EXPIRED", "EXPIRED")]
    [InlineData("WAITING", "REQUESTED", 0, "WAITING", "REQUESTED")]
    [InlineData("WAITING", "REQUESTED", 1, "WAITING", "REQUESTED")]
    [InlineData("IN_PROGRESS", "IN_PROGRESS", -1, "IN_PROGRESS", "IN_PROGRESS")]
    [InlineData("COMPLETED", "COMPLETED", -1, "COMPLETED", "COMPLETED")]
    [InlineData("CANCELLED", "PLANNED", -1, "CANCELLED", "PLANNED")]
    public async Task OnlyPastUnstartedRequestsExpire(string examStatus, string sessionStatus, int days,
        string expectedExam, string expectedSession)
    {
        await using var db = SchemaFixture.Create();
        var now = new DateTime(2026, 11, 16, 17, 0, 0, DateTimeKind.Utc); // Midnight Nov 17 in Vietnam.
        var record = SchemaFixture.Record();
        var registration = SchemaFixture.Registration(2);
        registration.Status = examStatus;
        registration.BusinessDate = new DateOnly(2026, 11, 17).AddDays(days);
        var plan = SchemaFixture.Plan(record);
        var session = new TreatmentSession { Plan = plan, SessionNumber = 1, Status = sessionStatus,
            ScheduledDate = registration.BusinessDate };
        db.ExaminationRegistrations.Add(registration);
        db.TreatmentSessions.Add(session);
        await db.SaveChangesAsync();
        await AppointmentExpiry.RefreshAsync(db, default, now);
        Assert.Equal(expectedExam, (await db.ExaminationRegistrations.SingleAsync(r => r.RegistrationId == 2)).Status);
        Assert.Equal(expectedSession, (await db.TreatmentSessions.SingleAsync()).Status);
        await AppointmentExpiry.RefreshAsync(db, default, now);
        Assert.Equal(expectedSession, session.Status);
        if (expectedExam == "EXPIRED")
        {
            await PatientRequestGuard.EnsureAvailableAsync(db, 2, default);
            await PatientRequestGuard.EnsureAvailableAsync(db, 1, default);
        }
    }

    [Fact]
    public async Task ExpiryBeginsAtVietnamMidnightAndPreservesStartedRequests()
    {
        await using var db = SchemaFixture.Create();
        var registration = SchemaFixture.Registration();
        registration.BusinessDate = new DateOnly(2026, 11, 16);
        db.ExaminationRegistrations.Add(registration);
        await db.SaveChangesAsync();
        await AppointmentExpiry.RefreshAsync(db, default, new DateTime(2026, 11, 16, 16, 59, 59, DateTimeKind.Utc));
        Assert.Equal("WAITING", registration.Status);
        registration.Status = "IN_PROGRESS";
        registration.StartedAt = new DateTime(2026, 11, 16, 16, 59, 59, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        await AppointmentExpiry.RefreshAsync(db, default, new DateTime(2026, 11, 16, 17, 0, 0, DateTimeKind.Utc));
        Assert.Equal("IN_PROGRESS", registration.Status);
    }
}
