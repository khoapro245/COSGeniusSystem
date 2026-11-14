using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public sealed class ReceptionDashboardService(ApplicationDbContext db) : IReceptionDashboardService
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    public async Task<DashboardStatsDto> GetTodayStatsAsync(CancellationToken ct = default)
    {
        var date = Today;
        var exams = db.ExaminationRegistrations.AsNoTracking().Where(r => r.BusinessDate == date);
        var treatments = db.TreatmentSessions.AsNoTracking().Where(s => s.ScheduledDate == date);
        var totalExams = await exams.CountAsync(r => r.Status != "CANCELLED", ct);
        var totalTreatments = await treatments.CountAsync(s => s.Status == "REQUESTED" || s.Status == "IN_PROGRESS" || s.Status == "COMPLETED", ct);
        return new DashboardStatsDto
        {
            TodayTotalVisits = totalExams + totalTreatments,
            WaitingExaminationCount = await exams.CountAsync(r => r.Status == "WAITING", ct),
            WaitingTreatmentCount = await treatments.CountAsync(s => s.Status == "REQUESTED", ct),
            CompletedVisitsCount = await exams.CountAsync(r => r.Status == "COMPLETED", ct)
                + await treatments.CountAsync(s => s.Status == "COMPLETED", ct),
            NewExaminationCount = totalExams, TreatmentCount = totalTreatments
        };
    }

    public async Task<DashboardQueueDto> GetQuickQueuesAsync(int top = 3, CancellationToken ct = default)
    {
        top = Math.Clamp(top <= 0 ? 3 : top, 1, 100);
        var date = Today;
        var exams = await db.ExaminationRegistrations.AsNoTracking()
            .Where(r => r.BusinessDate == date && (r.Status == "WAITING" || r.Status == "Chờ khám"))
            .OrderByDescending(r => r.IsPriority)
            .ThenBy(r => r.RegisteredAt)
            .ThenBy(r => r.QueueNumber)
            .Take(top)
            .Select(r => new DashboardWaitingExamDto
            {
                RegistrationId = r.RegistrationId,
                KcbCode = r.RegistrationCode ?? "",
                PatientCode = r.Patient != null ? (r.Patient.PatientCode ?? "") : "",
                FullName = r.Patient != null ? r.Patient.FullName : "Không xác định",
                DoctorName = r.Doctor != null ? r.Doctor.FullName : "Chưa phân công",
                ReceptionTime = r.RegisteredAt,
                IsPriority = r.IsPriority
            }).ToListAsync(ct);

        var sessions = await db.TreatmentSessions.AsNoTracking()
            .Where(s => s.ScheduledDate == date && (s.Status == "REQUESTED" || s.Status == "Chờ điều trị"))
            .OrderBy(s => s.RequestedAt ?? s.CreatedAt)
            .ThenBy(s => s.SessionId)
            .Take(top)
            .Select(s => new
            {
                s.SessionId,
                s.SessionNumber,
                RequestedAt = s.RequestedAt ?? s.CreatedAt,
                PlannedSessionCount = s.Plan != null ? s.Plan.PlannedSessionCount : 0,
                PatientCode = s.Plan != null && s.Plan.Record != null && s.Plan.Record.Patient != null ? (s.Plan.Record.Patient.PatientCode ?? "") : "",
                FullName = s.Plan != null && s.Plan.Record != null && s.Plan.Record.Patient != null ? s.Plan.Record.Patient.FullName : "Không xác định",
                PlanName = s.Plan != null ? s.Plan.PlanName : "",
                DoctorName = s.AssignedDoctor != null ? s.AssignedDoctor.FullName : "Chưa phân công"
            }).ToListAsync(ct);

        return new DashboardQueueDto
        {
            WaitingExaminations = exams.Select((e, i) => new DashboardWaitingExamDto
            {
                Stt = i + 1,
                RegistrationId = e.RegistrationId,
                KcbCode = e.KcbCode,
                PatientCode = e.PatientCode,
                FullName = e.FullName,
                DoctorName = e.DoctorName,
                ReceptionTime = e.ReceptionTime,
                IsPriority = e.IsPriority
            }).ToList(),
            WaitingTreatments = sessions.Select((s, i) => new DashboardWaitingTreatmentDto
            {
                Stt = i + 1,
                SessionId = s.SessionId,
                YcdtCode = $"YCDT{s.SessionId:D5}",
                PatientCode = s.PatientCode,
                FullName = s.FullName,
                ProtocolOrServiceName = s.PlanName,
                DoctorName = s.DoctorName,
                SessionInfo = $"{s.SessionNumber:D2} / {s.PlannedSessionCount:D2}",
                ReceptionTime = s.RequestedAt
            }).ToList()
        };
    }
}
