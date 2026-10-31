using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Examinations;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Examinations;

public sealed partial class ExaminationService
{
    public ExaminationService(ApplicationDbContext db) : this(db, null) { }

    public async Task<List<ExaminationVisitDto>> GetVisitsAsync(ExaminationFilterQuery query, CancellationToken ct = default)
    {
        var fromDate = query.FromDate ?? DateTime.Today;
        var toDate = query.ToDate ?? DateTime.Today.AddDays(1).AddTicks(-1);

        var dbQuery = db.ExamSheets.AsNoTracking()
            .Include(e => e.Doctor)
            .Include(e => e.Record).ThenInclude(r => r.Patient)
            .Where(e => e.ExamDate >= fromDate && e.ExamDate <= toDate);

        if (query.DoctorId.HasValue && query.DoctorId.Value > 0)
        {
            dbQuery = dbQuery.Where(e => e.DoctorId == query.DoctorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            dbQuery = dbQuery.Where(e =>
                e.Record.Patient.FullName.Contains(kw) ||
                (e.Record.Patient.PatientCode != null && e.Record.Patient.PatientCode.Contains(kw)) ||
                (e.Record.Patient.Phone != null && e.Record.Patient.Phone.Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(e => e.FinalDiagnosis == status);
        }

        var examSheets = await dbQuery
            .OrderByDescending(e => (e.Symptoms != null && e.Symptoms.Contains("[Ưu tiên]")) || (e.DoctorAdvice != null && e.DoctorAdvice.Contains("[Ưu tiên]")))
            .ThenBy(e => e.ExamDate)
            .ToListAsync(ct);

        var result = examSheets.Select((es, index) =>
        {
            var isPriority = (es.Symptoms != null && es.Symptoms.Contains("[Ưu tiên]")) ||
                             (es.DoctorAdvice != null && es.DoctorAdvice.Contains("[Ưu tiên]"));
            return new ExaminationVisitDto
            {
                Stt = index + 1,
                ExamSheetId = es.ExamSheetId,
                KcbCode = $"KCB{es.ExamSheetId:D5}",
                PatientCode = es.Record.Patient.PatientCode ?? $"BN{es.Record.Patient.PatientId:D5}",
                PatientName = es.Record.Patient.FullName,
                RegisteredDate = es.ExamDate,
                DoctorName = es.Doctor.FullName,
                IsPriority = isPriority,
                Phone = es.Record.Patient.Phone,
                Status = es.FinalDiagnosis
            };
        }).ToList();

        return result;
    }

    public async Task<ExaminationVisitDto> CreateVisitAsync(CreateExaminationVisitRequest request, CancellationToken ct = default)
    {
        var patient = await db.Patients.Include(p => p.MedicalRecords)
            .SingleOrDefaultAsync(p => p.PatientId == request.PatientId, ct);

        if (patient is null)
            throw new NotFoundException($"Patient with id {request.PatientId} was not found.");

        var doctor = await db.Doctors.SingleOrDefaultAsync(d => d.DoctorId == request.DoctorId && d.IsActive, ct);
        if (doctor is null)
            throw new NotFoundException($"Active doctor with id {request.DoctorId} was not found.");

        var activeRecord = patient.MedicalRecords.FirstOrDefault(r => r.Status == "Đang theo dõi");
        if (activeRecord is null)
        {
            activeRecord = new MedicalRecord
            {
                PatientId = request.PatientId,
                ExamDate = DateTime.Now,
                Status = "Đang theo dõi",
                PreliminaryDiagnosis = "Đăng ký khám bệnh"
            };
            await db.MedicalRecords.AddAsync(activeRecord, ct);
            await db.SaveChangesAsync(ct);
        }

        var symptoms = request.Symptoms?.Trim();
        if (request.IsPriority)
        {
            symptoms = string.IsNullOrWhiteSpace(symptoms) ? "[Ưu tiên]" : $"[Ưu tiên] {symptoms}";
        }

        var examSheet = new ExamSheet
        {
            RecordId = activeRecord.RecordId,
            DoctorId = request.DoctorId,
            ExamDate = DateTime.Now,
            Symptoms = symptoms,
            DoctorAdvice = request.DoctorAdvice,
            FinalDiagnosis = "Chờ khám"
        };

        await db.ExamSheets.AddAsync(examSheet, ct);
        await db.SaveChangesAsync(ct);

        return new ExaminationVisitDto
        {
            Stt = 1,
            ExamSheetId = examSheet.ExamSheetId,
            KcbCode = $"KCB{examSheet.ExamSheetId:D5}",
            PatientCode = patient.PatientCode ?? $"BN{patient.PatientId:D5}",
            PatientName = patient.FullName,
            RegisteredDate = examSheet.ExamDate,
            DoctorName = doctor.FullName,
            IsPriority = request.IsPriority,
            Phone = patient.Phone,
            Status = examSheet.FinalDiagnosis
        };
    }

    public async Task<ExaminationVisitDto> GetVisitByIdAsync(int examSheetId, CancellationToken ct = default)
    {
        var es = await db.ExamSheets.AsNoTracking()
            .Include(e => e.Doctor)
            .Include(e => e.Record).ThenInclude(r => r.Patient)
            .SingleOrDefaultAsync(e => e.ExamSheetId == examSheetId, ct);

        if (es is null)
            throw new NotFoundException($"Examination visit with id {examSheetId} was not found.");

        var isPriority = (es.Symptoms != null && es.Symptoms.Contains("[Ưu tiên]")) ||
                         (es.DoctorAdvice != null && es.DoctorAdvice.Contains("[Ưu tiên]"));

        return new ExaminationVisitDto
        {
            Stt = 1,
            ExamSheetId = es.ExamSheetId,
            KcbCode = $"KCB{es.ExamSheetId:D5}",
            PatientCode = es.Record.Patient.PatientCode ?? $"BN{es.Record.Patient.PatientId:D5}",
            PatientName = es.Record.Patient.FullName,
            RegisteredDate = es.ExamDate,
            DoctorName = es.Doctor.FullName,
            IsPriority = isPriority,
            Phone = es.Record.Patient.Phone,
            Status = es.FinalDiagnosis
        };
    }

    public async Task CancelVisitAsync(int examSheetId, CancellationToken ct = default)
    {
        var es = await db.ExamSheets.SingleOrDefaultAsync(e => e.ExamSheetId == examSheetId, ct);
        if (es is null)
            throw new NotFoundException($"Examination visit with id {examSheetId} was not found.");

        if (es.FinalDiagnosis != "Chờ khám")
            throw new BadRequestException("Chỉ có thể hủy lượt khám đang ở trạng thái 'Chờ khám'.");

        var hasInvoices = await db.Invoices.AnyAsync(i => i.RecordId == es.RecordId, ct);
        var hasUsages = await db.UsageHistories.AnyAsync(u => u.ExamSheetId == examSheetId, ct);
        var hasFormulas = await db.RemedyFormulas.AnyAsync(r => r.ExamSheetId == examSheetId, ct);

        if (!hasInvoices && !hasUsages && !hasFormulas)
        {
            db.ExamSheets.Remove(es);
        }
        else
        {
            es.FinalDiagnosis = "Đã hủy";
        }

        await db.SaveChangesAsync(ct);
    }
}
