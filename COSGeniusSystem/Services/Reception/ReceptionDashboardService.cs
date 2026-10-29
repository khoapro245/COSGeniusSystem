using System.Text.RegularExpressions;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Reception;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Reception;

public sealed class ReceptionDashboardService(ApplicationDbContext db) : IReceptionDashboardService
{
    public async Task<DashboardStatsDto> GetTodayStatsAsync(CancellationToken ct = default)
    {
        var startOfDay = DateTime.Today;
        var endOfDay = DateTime.Today.AddDays(1).AddTicks(-1);

        // Thống kê lượt khám
        var todayExams = db.ExamSheets.AsNoTracking()
            .Where(e => e.ExamDate >= startOfDay && e.ExamDate <= endOfDay);

        var waitingExams = await todayExams.CountAsync(e => e.FinalDiagnosis == "Chờ khám", ct);
        var completedExams = await todayExams.CountAsync(e => e.FinalDiagnosis != "Chờ khám" && e.FinalDiagnosis != "Đã hủy", ct);
        var totalExams = await todayExams.CountAsync(e => e.FinalDiagnosis != "Đã hủy", ct);

        // Thống kê lượt điều trị
        var todayUsages = db.UsageHistories.AsNoTracking()
            .Where(u => u.ExecutionTime >= startOfDay && u.ExecutionTime <= endOfDay);

        var waitingTreatments = await todayUsages.CountAsync(u => string.IsNullOrWhiteSpace(u.ResultNote) || u.ResultNote == "Chờ điều trị", ct);
        var completedTreatments = await todayUsages.CountAsync(u => !string.IsNullOrWhiteSpace(u.ResultNote) && u.ResultNote != "Chờ điều trị", ct);
        var totalTreatments = await todayUsages.CountAsync(ct);

        return new DashboardStatsDto
        {
            TodayTotalVisits = totalExams + totalTreatments,
            WaitingExaminationCount = waitingExams,
            WaitingTreatmentCount = waitingTreatments,
            CompletedVisitsCount = completedExams + completedTreatments,
            NewExaminationCount = totalExams,
            TreatmentCount = totalTreatments
        };
    }

    public async Task<DashboardQueueDto> GetQuickQueuesAsync(int top = 3, CancellationToken ct = default)
    {
        if (top <= 0) top = 3;

        var startOfDay = DateTime.Today;
        var endOfDay = DateTime.Today.AddDays(1).AddTicks(-1);

        // 1. Top ca chờ khám
        var waitingExamSheets = await db.ExamSheets.AsNoTracking()
            .Include(e => e.Doctor)
            .Include(e => e.Record).ThenInclude(r => r.Patient)
            .Where(e => e.ExamDate >= startOfDay && e.ExamDate <= endOfDay && e.FinalDiagnosis == "Chờ khám")
            .OrderByDescending(e => (e.Symptoms != null && e.Symptoms.Contains("[Ưu tiên]")) || (e.DoctorAdvice != null && e.DoctorAdvice.Contains("[Ưu tiên]")))
            .ThenBy(e => e.ExamDate)
            .Take(top)
            .ToListAsync(ct);

        var waitingExamList = waitingExamSheets.Select((es, index) =>
        {
            var isPriority = (es.Symptoms != null && es.Symptoms.Contains("[Ưu tiên]")) ||
                             (es.DoctorAdvice != null && es.DoctorAdvice.Contains("[Ưu tiên]"));

            return new DashboardWaitingExamDto
            {
                Stt = index + 1,
                ExamSheetId = es.ExamSheetId,
                KcbCode = $"KCB{es.ExamSheetId:D5}",
                PatientCode = es.Record.Patient.PatientCode ?? $"BN{es.Record.Patient.PatientId:D5}",
                FullName = es.Record.Patient.FullName,
                DoctorName = es.Doctor.FullName,
                ReceptionTime = es.ExamDate,
                IsPriority = isPriority
            };
        }).ToList();

        // 2. Top ca chờ điều trị
        var waitingUsages = await db.UsageHistories.AsNoTracking()
            .Include(u => u.Service)
            .Include(u => u.PerformedByNavigation)
            .Include(u => u.ExamSheet).ThenInclude(es => es.Doctor)
            .Include(u => u.ExamSheet).ThenInclude(es => es.Record).ThenInclude(r => r.Patient)
            .Include(u => u.ExamSheet).ThenInclude(es => es.Record).ThenInclude(r => r.TreatmentProtocols)
            .Where(u => u.ExecutionTime >= startOfDay && u.ExecutionTime <= endOfDay &&
                        (string.IsNullOrWhiteSpace(u.ResultNote) || u.ResultNote == "Chờ điều trị"))
            .OrderBy(u => u.ExecutionTime)
            .Take(top)
            .ToListAsync(ct);

        var waitingTreatmentList = new List<DashboardWaitingTreatmentDto>();
        for (int i = 0; i < waitingUsages.Count; i++)
        {
            var u = waitingUsages[i];
            var patient = u.ExamSheet.Record.Patient;
            var protocol = u.ExamSheet.Record.TreatmentProtocols.OrderByDescending(p => p.StartDate).FirstOrDefault();

            var recordId = u.ExamSheet.RecordId;
            var sessionCount = await db.UsageHistories.AsNoTracking()
                .CountAsync(x => x.ExamSheet.RecordId == recordId && x.ExecutionTime <= u.ExecutionTime, ct);

            int totalSessions = 5;
            if (protocol != null && !string.IsNullOrWhiteSpace(protocol.Description))
            {
                var match = Regex.Match(protocol.Description, @"\b(\d+)\s*(buổi|buoi|sessions?|lần|lan)?\b", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedTotal) && parsedTotal > 0)
                {
                    totalSessions = parsedTotal;
                }
            }

            if (sessionCount > totalSessions)
            {
                totalSessions = sessionCount;
            }

            var doctorName = u.PerformedByNavigation != null ? u.PerformedByNavigation.FullName : u.ExamSheet.Doctor.FullName;
            var protocolOrServiceName = protocol != null ? protocol.ProtocolName : u.Service.ServiceName;

            waitingTreatmentList.Add(new DashboardWaitingTreatmentDto
            {
                Stt = i + 1,
                UsageId = u.UsageId,
                YcdtCode = $"YCDT{u.UsageId:D5}",
                PatientCode = patient.PatientCode ?? $"BN{patient.PatientId:D5}",
                FullName = patient.FullName,
                ProtocolOrServiceName = protocolOrServiceName,
                DoctorName = doctorName,
                SessionInfo = $"{sessionCount:D2} / {totalSessions:D2}",
                ReceptionTime = u.ExecutionTime
            });
        }

        return new DashboardQueueDto
        {
            WaitingExaminations = waitingExamList,
            WaitingTreatments = waitingTreatmentList
        };
    }
}

