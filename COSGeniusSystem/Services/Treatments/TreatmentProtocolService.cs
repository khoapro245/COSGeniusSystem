using System.Text.RegularExpressions;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Treatments;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Treatments;

public sealed class TreatmentProtocolService(ApplicationDbContext db) : ITreatmentProtocolService, ITreatmentQueueService
{
    public async Task<List<TreatmentQueueDto>> GetQueueAsync(TreatmentQueueQuery query, CancellationToken ct = default)
    {
        var targetDate = query.Date?.Date ?? DateTime.Today;
        var startOfDay = targetDate;
        var endOfDay = targetDate.AddDays(1).AddTicks(-1);

        var dbQuery = db.UsageHistories.AsNoTracking()
            .Include(u => u.Service)
            .Include(u => u.PerformedByNavigation)
            .Include(u => u.ExamSheet).ThenInclude(es => es.Doctor)
            .Include(u => u.ExamSheet).ThenInclude(es => es.Record).ThenInclude(r => r.Patient)
            .Include(u => u.ExamSheet).ThenInclude(es => es.Record).ThenInclude(r => r.TreatmentProtocols)
            .Where(u => u.ExecutionTime >= startOfDay && u.ExecutionTime <= endOfDay)
            .Where(u => string.IsNullOrWhiteSpace(u.ResultNote) || u.ResultNote == "Chờ điều trị");

        if (query.DoctorId.HasValue && query.DoctorId.Value > 0)
        {
            dbQuery = dbQuery.Where(u => u.PerformedBy == query.DoctorId.Value || u.ExamSheet.DoctorId == query.DoctorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            dbQuery = dbQuery.Where(u =>
                u.ExamSheet.Record.Patient.FullName.Contains(kw) ||
                (u.ExamSheet.Record.Patient.PatientCode != null && u.ExamSheet.Record.Patient.PatientCode.Contains(kw)) ||
                (u.ExamSheet.Record.Patient.Phone != null && u.ExamSheet.Record.Patient.Phone.Contains(kw)));
        }

        var usages = await dbQuery
            .OrderBy(u => u.ExecutionTime)
            .ToListAsync(ct);

        var result = new List<TreatmentQueueDto>();
        for (int i = 0; i < usages.Count; i++)
        {
            var u = usages[i];
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

            var sessionInfo = $"Buổi {sessionCount:D2} / {totalSessions:D2}";
            var doctorName = u.PerformedByNavigation != null ? u.PerformedByNavigation.FullName : u.ExamSheet.Doctor.FullName;

            result.Add(new TreatmentQueueDto
            {
                Stt = i + 1,
                UsageId = u.UsageId,
                PatientName = patient.FullName,
                PatientCode = patient.PatientCode ?? $"BN{patient.PatientId:D5}",
                Phone = patient.Phone,
                YcdtCode = $"YCDT{u.UsageId:D5}",
                ReceptionTime = u.ExecutionTime,
                ProtocolName = protocol != null ? protocol.ProtocolName : u.Service.ServiceName,
                SessionInfo = sessionInfo,
                DoctorName = doctorName,
                Status = "Chờ điều trị"
            });
        }

        return result;
    }

    public async Task<TreatmentQueueDto> CreateTreatmentRequestAsync(CreateTreatmentRequest request, CancellationToken ct = default)
    {
        var patient = await db.Patients
            .Include(p => p.MedicalRecords)
            .SingleOrDefaultAsync(p => p.PatientId == request.PatientId, ct);

        if (patient is null)
            throw new NotFoundException($"Patient with id {request.PatientId} was not found.");

        var service = await db.Services.FindAsync(new object[] { request.ServiceId }, ct);
        if (service is null)
            throw new NotFoundException($"Service with id {request.ServiceId} was not found.");

        var doctor = await db.Doctors.FindAsync(new object[] { request.DoctorId }, ct);
        if (doctor is null)
            throw new NotFoundException($"Doctor with id {request.DoctorId} was not found.");

        int examSheetId;
        if (request.ExamSheetId.HasValue && request.ExamSheetId.Value > 0)
        {
            var es = await db.ExamSheets.Include(e => e.Record)
                .SingleOrDefaultAsync(e => e.ExamSheetId == request.ExamSheetId.Value && e.Record.PatientId == request.PatientId, ct);

            if (es is null)
                throw new BadRequestException("Phiếu khám chỉ định không thuộc về bệnh nhân này.");

            examSheetId = es.ExamSheetId;
        }
        else
        {
            var recentExamSheet = await db.ExamSheets
                .Include(e => e.Record)
                .Where(e => e.Record.PatientId == request.PatientId)
                .OrderByDescending(e => e.ExamDate)
                .FirstOrDefaultAsync(ct);

            if (recentExamSheet != null)
            {
                examSheetId = recentExamSheet.ExamSheetId;
            }
            else
            {
                var record = patient.MedicalRecords.FirstOrDefault(r => r.Status == "Đang theo dõi");
                if (record is null)
                {
                    record = new MedicalRecord
                    {
                        PatientId = request.PatientId,
                        ExamDate = DateTime.Now,
                        Status = "Đang theo dõi",
                        PreliminaryDiagnosis = "Tiếp nhận điều trị theo liệu trình"
                    };
                    await db.MedicalRecords.AddAsync(record, ct);
                    await db.SaveChangesAsync(ct);
                }

                var newExamSheet = new ExamSheet
                {
                    RecordId = record.RecordId,
                    DoctorId = request.DoctorId,
                    ExamDate = DateTime.Now,
                    FinalDiagnosis = "Chỉ định điều trị",
                    Symptoms = "Tiếp nhận điều trị theo liệu trình"
                };
                await db.ExamSheets.AddAsync(newExamSheet, ct);
                await db.SaveChangesAsync(ct);
                examSheetId = newExamSheet.ExamSheetId;
            }
        }

        var usageHistory = new UsageHistory
        {
            ExamSheetId = examSheetId,
            ServiceId = request.ServiceId,
            PerformedBy = request.DoctorId,
            ExecutionTime = DateTime.Now,
            Quantity = 1,
            ResultNote = request.Note
        };

        await db.UsageHistories.AddAsync(usageHistory, ct);
        await db.SaveChangesAsync(ct);

        var activeExamSheet = await db.ExamSheets
            .Include(e => e.Record).ThenInclude(r => r.TreatmentProtocols)
            .SingleOrDefaultAsync(e => e.ExamSheetId == examSheetId, ct);

        var protocol = activeExamSheet?.Record?.TreatmentProtocols.OrderByDescending(p => p.StartDate).FirstOrDefault();
        var sessionCount = await db.UsageHistories.AsNoTracking()
            .CountAsync(x => x.ExamSheet.RecordId == activeExamSheet!.RecordId && x.ExecutionTime <= usageHistory.ExecutionTime, ct);

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

        return new TreatmentQueueDto
        {
            Stt = 1,
            UsageId = usageHistory.UsageId,
            PatientName = patient.FullName,
            PatientCode = patient.PatientCode ?? $"BN{patient.PatientId:D5}",
            Phone = patient.Phone,
            YcdtCode = $"YCDT{usageHistory.UsageId:D5}",
            ReceptionTime = usageHistory.ExecutionTime,
            ProtocolName = protocol != null ? protocol.ProtocolName : service.ServiceName,
            SessionInfo = $"Buổi {sessionCount:D2} / {totalSessions:D2}",
            DoctorName = doctor.FullName,
            Status = "Chờ điều trị"
        };
    }
}

