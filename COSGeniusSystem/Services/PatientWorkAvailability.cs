using COSGeniusSystem.Data;
using COSGeniusSystem.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public static class PatientWorkAvailability
{
    // Every examination/treatment start: doctor -> patient -> registration/plan -> session.
    public static async Task LockAsync(ApplicationDbContext db, int patientId, CancellationToken ct)
    {
        if (db.Database.IsRelational())
            _ = await db.Patients.FromSqlInterpolated($"SELECT * FROM dbo.Patient WITH (UPDLOCK, HOLDLOCK) WHERE PatientID = {patientId}")
                .AsNoTracking().SingleAsync(ct);
    }

    public static async Task EnsureAvailableAsync(ApplicationDbContext db, int patientId, CancellationToken ct)
    {
        if (await db.TreatmentSessions.AnyAsync(s => s.Plan.Record.PatientId == patientId && s.Status == "IN_PROGRESS", ct)
            || await db.ExaminationRegistrations.AnyAsync(r => r.PatientId == patientId && (r.Status == "IN_PROGRESS" || r.Status == "Đang khám"), ct)
            || await db.MedicalRecords.AnyAsync(r => r.PatientId == patientId && (r.Status == "IN_PROGRESS" || r.Status == "Đang khám"), ct))
            throw new ConflictException("Bệnh nhân đang được khám hoặc điều trị. Hãy hoàn tất công việc hiện tại trước.");
    }
}
