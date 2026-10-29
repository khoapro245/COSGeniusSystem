using System.Text.RegularExpressions;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Patients;
using COSGeniusSystem.DTOs.Requests;
using COSGeniusSystem.DTOs.Responses;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public sealed class PatientService(ApplicationDbContext db) : IPatientService
{
    private async Task<Patient> FindAsync(int patientId, CancellationToken ct)
    {
        return await db.Patients.SingleOrDefaultAsync(p => p.PatientId == patientId, ct)
               ?? throw new NotFoundException($"Patient with id {patientId} was not found.");
    }

    #region Legacy Methods
    public async Task<PagedResult<PatientResponse>> GetAllAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        if (pageNumber < 1 || pageNumber > 1000000 || pageSize < 1 || pageSize > 100)
            throw new BadRequestException("Page number must be 1 to 1000000 and page size must be 1 to 100.");

        var totalCount = await db.Patients.CountAsync(ct);
        var items = (await db.Patients.AsNoTracking().OrderBy(p => p.PatientId)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct))
            .Select(PatientMapping.ToResponse).ToList();

        return new(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PatientResponse> GetByIdAsync(int patientId, CancellationToken ct) =>
        PatientMapping.ToResponse(await FindAsync(patientId, ct));

    public async Task<PatientResponse> CreateAsync(COSGeniusSystem.DTOs.Requests.CreatePatientRequest request, CancellationToken ct)
    {
        ValidateLegacy(request);
        var entity = new Patient();
        PatientMapping.Apply(request, entity);
        await db.Patients.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);
        return PatientMapping.ToResponse(entity);
    }

    public async Task UpdateAsync(int patientId, COSGeniusSystem.DTOs.Requests.UpdatePatientRequest request, CancellationToken ct)
    {
        ValidateLegacy(request);
        var entity = await FindAsync(patientId, ct);
        PatientMapping.Apply(request, entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int patientId, CancellationToken ct)
    {
        var entity = await FindAsync(patientId, ct);
        if (await db.PatientContacts.AnyAsync(p => p.PatientId == patientId, ct)
            || await db.MedicalRecords.AnyAsync(p => p.PatientId == patientId, ct))
            throw new ConflictException("Patient has contacts or medical records. Remove related records before deleting the patient.");

        db.Patients.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private static void ValidateLegacy(PatientRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.DateOfBirth > today || request.IdentityCardIssuedDate > today)
            throw new BadRequestException("Birth date and identity card issue date cannot be in the future.");
        if (request.IdentityCardIssuedDate < request.DateOfBirth)
            throw new BadRequestException("Identity card issue date cannot precede birth date.");
    }
    #endregion

    #region Module 1: Hồ sơ & Thông tin Bệnh nhân (Phân hệ Tiếp tân)
    public async Task<List<PatientResponseDto>> SearchPatientsAsync(PatientSearchQuery query, CancellationToken ct = default)
    {
        var dbQuery = db.Patients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(p =>
                (p.PatientCode != null && p.PatientCode.Contains(keyword)) ||
                p.FullName.Contains(keyword) ||
                (p.Phone != null && p.Phone.Contains(keyword)));
        }

        var patients = await dbQuery
            .OrderByDescending(p => p.PatientId)
            .Select(p => new PatientResponseDto
            {
                PatientId = p.PatientId,
                PatientCode = p.PatientCode ?? $"BN{p.PatientId:D5}",
                FullName = p.FullName,
                DateOfBirth = p.DateOfBirth,
                Gender = p.Gender,
                Phone = p.Phone,
                Address = p.Address
            })
            .ToListAsync(ct);

        return patients;
    }

    public async Task<PatientDetailDto> GetPatientDetailAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Patients.AsNoTracking()
            .Include(p => p.PatientContacts)
            .SingleOrDefaultAsync(p => p.PatientId == id, ct);

        if (p is null)
            throw new NotFoundException($"Patient with id {id} was not found.");

        return new PatientDetailDto
        {
            PatientId = p.PatientId,
            PatientCode = p.PatientCode ?? $"BN{p.PatientId:D5}",
            FullName = p.FullName,
            DateOfBirth = p.DateOfBirth,
            Gender = p.Gender,
            Phone = p.Phone,
            Address = p.Address,
            IdentityCard = p.IdentityCard,
            IdentityCardIssuedDate = p.IdentityCardIssuedDate,
            IdentityCardIssuedPlace = p.IdentityCardIssuedPlace,
            Email = p.Email,
            Province = p.Province,
            Ward = p.Ward,
            Occupation = p.Occupation,
            Workplace = p.Workplace,
            Ethnicity = p.Ethnicity,
            Nationality = p.Nationality,
            PhotoUrl = p.PhotoUrl,
            CreatedAt = p.CreatedAt,
            Contacts = p.PatientContacts.Select(c => new PatientContactDto
            {
                ContactId = c.ContactId,
                FullName = c.FullName,
                Relationship = c.Relationship,
                Phone = c.Phone
            }).ToList()
        };
    }

    public async Task<PatientDetailDto> CreatePatientV1Async(COSGeniusSystem.DTOs.Patients.CreatePatientRequest request, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.DateOfBirth > today || request.IdentityCardIssuedDate > today)
            throw new BadRequestException("Ngày sinh và ngày cấp CCCD không được lớn hơn ngày hiện tại.");
        if (request.IdentityCardIssuedDate < request.DateOfBirth)
            throw new BadRequestException("Ngày cấp CCCD không thể trước ngày sinh.");

        var patient = new Patient
        {
            FullName = request.FullName.Trim(),
            Gender = request.Gender,
            DateOfBirth = request.DateOfBirth,
            Phone = request.Phone,
            Address = request.Address,
            IdentityCard = request.IdentityCard,
            Email = request.Email,
            IdentityCardIssuedDate = request.IdentityCardIssuedDate,
            IdentityCardIssuedPlace = request.IdentityCardIssuedPlace,
            PhotoUrl = request.PhotoUrl,
            Province = request.Province,
            Ward = request.Ward,
            Occupation = request.Occupation,
            Workplace = request.Workplace,
            Nationality = request.Nationality,
            Ethnicity = request.Ethnicity,
            CreatedAt = DateTime.Now
        };

        if (!string.IsNullOrWhiteSpace(request.ContactFullName))
        {
            patient.PatientContacts.Add(new PatientContact
            {
                FullName = request.ContactFullName.Trim(),
                Relationship = request.ContactRelationship,
                Phone = request.ContactPhone
            });
        }

        var defaultRecord = new MedicalRecord
        {
            CreatedDate = DateTime.Now,
            Status = "Đang theo dõi",
            InitialDiagnosis = "Tiếp nhận hồ sơ ban đầu"
        };
        patient.MedicalRecords.Add(defaultRecord);

        await db.Patients.AddAsync(patient, ct);
        await db.SaveChangesAsync(ct);

        return await GetPatientDetailAsync(patient.PatientId, ct);
    }

    public async Task<PatientDetailDto> UpdatePatientV1Async(int id, COSGeniusSystem.DTOs.Patients.UpdatePatientRequest request, CancellationToken ct = default)
    {
        var patient = await db.Patients
            .Include(p => p.PatientContacts)
            .SingleOrDefaultAsync(p => p.PatientId == id, ct);

        if (patient is null)
            throw new NotFoundException($"Patient with id {id} was not found.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.DateOfBirth > today || request.IdentityCardIssuedDate > today)
            throw new BadRequestException("Ngày sinh và ngày cấp CCCD không được lớn hơn ngày hiện tại.");
        if (request.IdentityCardIssuedDate < request.DateOfBirth)
            throw new BadRequestException("Ngày cấp CCCD không thể trước ngày sinh.");

        patient.FullName = request.FullName.Trim();
        patient.Gender = request.Gender;
        patient.DateOfBirth = request.DateOfBirth;
        patient.Phone = request.Phone;
        patient.Address = request.Address;
        patient.IdentityCard = request.IdentityCard;
        patient.Email = request.Email;
        patient.IdentityCardIssuedDate = request.IdentityCardIssuedDate;
        patient.IdentityCardIssuedPlace = request.IdentityCardIssuedPlace;
        patient.PhotoUrl = request.PhotoUrl;
        patient.Province = request.Province;
        patient.Ward = request.Ward;
        patient.Occupation = request.Occupation;
        patient.Workplace = request.Workplace;
        patient.Nationality = request.Nationality;
        patient.Ethnicity = request.Ethnicity;

        if (request.Contacts != null)
        {
            foreach (var c in request.Contacts)
            {
                if (c.ContactId.HasValue && c.ContactId.Value > 0)
                {
                    var existingContact = patient.PatientContacts.FirstOrDefault(x => x.ContactId == c.ContactId.Value);
                    if (existingContact != null)
                    {
                        existingContact.FullName = c.FullName.Trim();
                        existingContact.Relationship = c.Relationship;
                        existingContact.Phone = c.Phone;
                    }
                }
                else
                {
                    patient.PatientContacts.Add(new PatientContact
                    {
                        PatientId = id,
                        FullName = c.FullName.Trim(),
                        Relationship = c.Relationship,
                        Phone = c.Phone
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return await GetPatientDetailAsync(id, ct);
    }

    public async Task<List<PatientVisitHistoryDto>> GetVisitHistoryAsync(int patientId, CancellationToken ct = default)
    {
        var patientExists = await db.Patients.AnyAsync(p => p.PatientId == patientId, ct);
        if (!patientExists)
            throw new NotFoundException($"Patient with id {patientId} was not found.");

        var visitHistory = await db.ExamSheets.AsNoTracking()
            .Include(es => es.Doctor)
            .Include(es => es.Record)
            .Where(es => es.Record.PatientId == patientId)
            .OrderByDescending(es => es.ExamDate)
            .Select(es => new PatientVisitHistoryDto
            {
                ExamSheetId = es.ExamSheetId,
                ExamDate = es.ExamDate,
                KcbCode = $"KCB{es.ExamSheetId:D5}",
                MrCode = $"MR{es.RecordId:D5}",
                DoctorName = es.Doctor.FullName,
                Symptoms = es.Symptoms,
                Status = es.FinalDiagnosis == "Chờ khám" ? "Chờ khám" : "Đã hoàn tất"
            })
            .ToListAsync(ct);

        return visitHistory;
    }

    public async Task<MedicalRecordDetailDto> GetExamSheetDetailAsync(int examSheetId, CancellationToken ct = default)
    {
        var es = await db.ExamSheets.AsNoTracking()
            .Include(e => e.Doctor)
            .Include(e => e.UsageHistories)
                .ThenInclude(u => u.Service)
            .Include(e => e.UsageHistories)
                .ThenInclude(u => u.PerformedByNavigation)
            .Include(e => e.RemedyFormulas)
                .ThenInclude(r => r.FormulaDetails)
                    .ThenInclude(fd => fd.Herb)
            .SingleOrDefaultAsync(e => e.ExamSheetId == examSheetId, ct);

        if (es is null)
            throw new NotFoundException($"Exam sheet with id {examSheetId} was not found.");

        return new MedicalRecordDetailDto
        {
            ExamSheetId = es.ExamSheetId,
            KcbCode = $"KCB{es.ExamSheetId:D5}",
            ExamDate = es.ExamDate,
            DoctorName = es.Doctor.FullName,
            Symptoms = es.Symptoms,
            FinalDiagnosis = es.FinalDiagnosis,
            PulseDiagnosis = es.PulseDiagnosis,
            TongueDiagnosis = es.TongueDiagnosis,
            DoctorAdvice = es.DoctorAdvice,
            Services = es.UsageHistories.OrderBy(u => u.ExecutionTime).Select(u => new UsageHistoryItemDto
            {
                UsageId = u.UsageId,
                YcdtCode = $"YCDT{u.UsageId:D5}",
                ServiceName = u.Service.ServiceName,
                Quantity = u.Quantity,
                ExecutionTime = u.ExecutionTime,
                PerformedByDoctor = u.PerformedByNavigation != null ? u.PerformedByNavigation.FullName : null,
                ResultNote = u.ResultNote
            }).ToList(),
            Prescriptions = es.RemedyFormulas.OrderByDescending(r => r.CreatedAt).Select(r => new PrescriptionDto
            {
                FormulaId = r.FormulaId,
                FormulaName = r.FormulaName,
                UsageInstructions = r.UsageInstructions,
                DosageDays = r.DosageDays,
                TotalDoses = r.TotalDoses,
                Details = r.FormulaDetails.Select(fd => new FormulaDetailItemDto
                {
                    HerbId = fd.HerbId,
                    HerbName = fd.Herb.CommonName,
                    DosagePerDose = fd.DosagePerDose,
                    RoleInFormula = fd.RoleInFormula,
                    Note = fd.Note
                }).ToList()
            }).ToList()
        };
    }

    public async Task<List<TreatmentProtocolDto>> GetTreatmentProtocolsAsync(int patientId, CancellationToken ct = default)
    {
        var patientExists = await db.Patients.AnyAsync(p => p.PatientId == patientId, ct);
        if (!patientExists)
            throw new NotFoundException($"Patient with id {patientId} was not found.");

        var protocols = await db.TreatmentProtocols.AsNoTracking()
            .Include(tp => tp.Doctor)
            .Include(tp => tp.Record)
            .Where(tp => tp.Record.PatientId == patientId)
            .OrderByDescending(tp => tp.StartDate)
            .ToListAsync(ct);

        var result = new List<TreatmentProtocolDto>();
        foreach (var tp in protocols)
        {
            var completedSessions = await db.UsageHistories.AsNoTracking()
                .CountAsync(u => u.ExamSheet.RecordId == tp.RecordId, ct);

            int totalSessions = 10;
            if (!string.IsNullOrWhiteSpace(tp.Description))
            {
                var match = Regex.Match(tp.Description, @"\b(\d+)\s*(buổi|buoi|sessions?|lần|lan)?\b", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedTotal) && parsedTotal > 0)
                {
                    totalSessions = parsedTotal;
                }
            }

            if (completedSessions > totalSessions)
            {
                totalSessions = completedSessions;
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            string status;
            if (tp.EndDate.HasValue && tp.EndDate.Value < today || completedSessions >= totalSessions)
            {
                status = "Đã hoàn thành";
            }
            else if (completedSessions > 0)
            {
                status = "Đang điều trị";
            }
            else
            {
                status = "Mới tiếp nhận";
            }

            result.Add(new TreatmentProtocolDto
            {
                ProtocolId = tp.ProtocolId,
                ProtocolCode = $"LT{tp.ProtocolId:D5}",
                ProtocolName = tp.ProtocolName,
                DoctorName = tp.Doctor.FullName,
                StartDate = tp.StartDate,
                EndDate = tp.EndDate,
                CompletedSessions = completedSessions,
                TotalSessions = totalSessions,
                Status = status
            });
        }

        return result;
    }

    public async Task<List<TreatmentSessionDto>> GetTreatmentSessionsAsync(int protocolId, CancellationToken ct = default)
    {
        var protocol = await db.TreatmentProtocols.AsNoTracking()
            .SingleOrDefaultAsync(tp => tp.ProtocolId == protocolId, ct);

        if (protocol is null)
            throw new NotFoundException($"Treatment protocol with id {protocolId} was not found.");

        var usageHistories = await db.UsageHistories.AsNoTracking()
            .Include(u => u.Service)
            .Include(u => u.PerformedByNavigation)
            .Include(u => u.ExamSheet).ThenInclude(es => es.Doctor)
            .Where(u => u.ExamSheet.RecordId == protocol.RecordId)
            .OrderBy(u => u.ExecutionTime)
            .ToListAsync(ct);

        var sessions = usageHistories.Select((uh, index) => new TreatmentSessionDto
        {
            SessionNo = index + 1,
            ExecutionTime = uh.ExecutionTime,
            ServiceName = uh.Service.ServiceName,
            DoctorName = uh.PerformedByNavigation != null ? uh.PerformedByNavigation.FullName : uh.ExamSheet.Doctor.FullName,
            ResultNote = uh.ResultNote,
            Status = string.IsNullOrWhiteSpace(uh.ResultNote) ? "Chờ điều trị" : "Đã hoàn thành"
        }).ToList();

        return sessions;
    }
    #endregion
}
