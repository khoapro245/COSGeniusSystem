using COSGeniusSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public static class AppointmentExpiry
{
    // Expire only unstarted requests, at midnight in Vietnam (UTC+7).
    public static async Task RefreshAsync(ApplicationDbContext db, CancellationToken ct, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.AddHours(7));
        var exams = db.ExaminationRegistrations.Where(r => r.Status == "WAITING"
            && r.BusinessDate < today && r.StartedAt == null && r.CompletedAt == null
            && !db.MedicalRecords.Any(m => m.RegistrationId == r.RegistrationId));
        var sessions = db.TreatmentSessions.Where(s => s.Status == "REQUESTED"
            && s.ScheduledDate < today && s.StartedAt == null && s.CompletedAt == null);
        if (db.Database.IsRelational())
        {
            // Conditional updates preserve a concurrent start and advance SQL rowVersion.
            await exams.ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, "EXPIRED")
                .SetProperty(r => r.UpdatedAt, now), ct);
            await sessions.ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, "EXPIRED"), ct);
        }
        else
        {
            foreach (var exam in await exams.ToListAsync(ct))
            {
                exam.Status = "EXPIRED";
                exam.UpdatedAt = now;
            }
            foreach (var session in await sessions.ToListAsync(ct)) session.Status = "EXPIRED";
            await db.SaveChangesAsync(ct);
        }
    }
}

public sealed class AppointmentExpiryWorker(IServiceScopeFactory scopes, ILogger<AppointmentExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await AppointmentExpiry.RefreshAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogError(error, "Could not expire pending appointments."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
