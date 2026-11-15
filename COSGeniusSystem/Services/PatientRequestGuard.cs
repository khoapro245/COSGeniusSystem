using COSGeniusSystem.Data;
using COSGeniusSystem.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public static class PatientRequestGuard
{
    // Both creation paths hold this transaction-owned lock until their insert commits.
    // The patient, rather than the date/doctor/plan, is the unit of exclusivity.
    public static async Task EnsureAvailableAsync(ApplicationDbContext db, int patientId, CancellationToken ct)
    {
        if (db.Database.IsRelational())
        {
            var resource = $"PatientRequest:{patientId}";
            await db.Database.ExecuteSqlInterpolatedAsync($@"DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
                IF @result < 0 THROW 51034, 'Cannot lock patient request. Please retry.', 1;", ct);
        }

        if (await HasActiveExaminationAsync(db, patientId, ct))
            throw new ConflictException("The patient already has a pending or ongoing examination request. Complete it or cancel the pending request before creating a new one.");
        if (await HasActiveTreatmentAsync(db, patientId, ct))
            throw new ConflictException("The patient already has a pending or ongoing treatment request. Complete it or cancel the pending request before creating a new one.");
    }

    public static Task<bool> HasActiveExaminationAsync(ApplicationDbContext db, int patientId, CancellationToken ct) =>
        db.ExaminationRegistrations.AnyAsync(r => r.PatientId == patientId
            && (r.Status == "WAITING" || r.Status == "IN_PROGRESS"), ct);

    public static Task<bool> HasActiveTreatmentAsync(ApplicationDbContext db, int patientId, CancellationToken ct) =>
        db.TreatmentSessions.AnyAsync(s => s.Plan.Record.PatientId == patientId
            && (s.Status == "REQUESTED" || s.Status == "IN_PROGRESS"), ct);
}
