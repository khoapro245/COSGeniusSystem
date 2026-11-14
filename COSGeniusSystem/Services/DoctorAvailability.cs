using COSGeniusSystem.Data;
using COSGeniusSystem.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public static class DoctorAvailability
{
    // Acquire before locking a registration/plan, and hold until the start transaction commits.
    public static async Task LockAsync(ApplicationDbContext db, int doctorId, CancellationToken ct)
    {
        if (db.Database.IsRelational())
            _ = await db.Doctors.FromSqlInterpolated($"SELECT * FROM dbo.Doctor WITH (UPDLOCK, HOLDLOCK) WHERE DoctorID = {doctorId}")
                .AsNoTracking().SingleAsync(ct);
    }

    public static async Task EnsureAvailableAsync(ApplicationDbContext db, int doctorId, CancellationToken ct)
    {
        if (await db.ExaminationRegistrations.AnyAsync(r => r.DoctorId == doctorId
                && (r.Status == "IN_PROGRESS" || r.Status == "Đang khám"), ct)
            || await db.MedicalRecords.AnyAsync(r => r.DoctorId == doctorId
                && (r.Status == "IN_PROGRESS" || r.Status == "Đang khám"), ct)
            || await db.TreatmentSessions.AnyAsync(s => (s.PerformedBy ?? s.AssignedDoctorId) == doctorId
                && (s.Status == "IN_PROGRESS" || s.Status == "Đang điều trị"), ct))
            throw new ConflictException("Bác sĩ đang khám hoặc đang điều trị cho bệnh nhân khác. Vui lòng hoàn tất ca hiện tại trước khi tiếp nhận bệnh nhân tiếp theo.");
    }
}
