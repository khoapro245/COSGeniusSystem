using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
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
        => await SearchAsync(null, pageNumber, pageSize, ct);

    public async Task<PagedResult<PatientResponse>> SearchAsync(string? search, int pageNumber, int pageSize, CancellationToken ct)
    {
        if (pageNumber < 1 || pageNumber > 1000000 || pageSize < 1 || pageSize > 100)
            throw new BadRequestException("Page number must be 1 to 1000000 and page size must be 1 to 100.");
        var query = db.Patients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(p => p.FullName.Contains(keyword) || (p.Phone != null && p.Phone.Contains(keyword))
                || (p.PatientCode != null && p.PatientCode.Contains(keyword)));
        }
        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.PatientId).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(PatientMapping.Projection).ToListAsync(ct);
        return new(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PatientResponse> GetByIdAsync(int patientId, CancellationToken ct) =>
        PatientMapping.ToResponse(await FindAsync(patientId, ct));

    public async Task<PatientResponse> CreateAsync(COSGeniusSystem.DTOs.CreatePatientRequest request, CancellationToken ct)
    {
        ValidateLegacy(request);
        var entity = new Patient();
        PatientMapping.Apply(request, entity);
        await db.Patients.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);
        return PatientMapping.ToResponse(entity);
    }

    public async Task UpdateAsync(int patientId, COSGeniusSystem.DTOs.UpdatePatientRequest request, CancellationToken ct)
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
            || await db.MedicalRecords.AnyAsync(p => p.PatientId == patientId, ct)
            || await db.ExaminationRegistrations.AnyAsync(p => p.PatientId == patientId, ct))
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

    public async Task<PatientDetailDto> CreatePatientV1Async(COSGeniusSystem.DTOs.Compatibility.V1.CreatePatientRequest request, CancellationToken ct = default)
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

        await db.Patients.AddAsync(patient, ct);
        await db.SaveChangesAsync(ct);

        return await GetPatientDetailAsync(patient.PatientId, ct);
    }

    public async Task<PatientDetailDto> UpdatePatientV1Async(int id, COSGeniusSystem.DTOs.Compatibility.V1.UpdatePatientRequest request, CancellationToken ct = default)
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
        if (!await db.Patients.AnyAsync(p => p.PatientId == patientId, ct))
            throw new NotFoundException($"Patient with id {patientId} was not found.");
        return await db.MedicalRecords.AsNoTracking().Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.ExamDate).Select(r => new PatientVisitHistoryDto
            {
                RecordId = r.RecordId, RegistrationId = r.RegistrationId, ExamDate = r.ExamDate,
                KcbCode = r.Registration.RegistrationCode ?? "", MrCode = $"MR{r.RecordId:D5}",
                DoctorName = r.Doctor.FullName, Symptoms = r.Symptoms, Status = r.Status
            }).ToListAsync(ct);
    }

    public async Task<MedicalRecordDetailDto> GetMedicalRecordDetailAsync(int recordId, CancellationToken ct = default)
    {
        var record = await db.MedicalRecords.AsNoTracking().Include(r => r.Doctor).Include(r => r.Registration)
            .Include(r => r.HerbalPrescriptions).ThenInclude(p => p.HerbalPrescriptionDetails)
            .SingleOrDefaultAsync(r => r.RecordId == recordId, ct)
            ?? throw new NotFoundException($"Medical record with id {recordId} was not found.");
        var services = await db.UsageHistories.AsNoTracking().Where(u => u.Session.Plan.RecordId == recordId)
            .OrderBy(u => u.ExecutionTime).Select(u => new UsageHistoryItemDto
            {
                UsageId = u.UsageId, SessionId = u.SessionId, PlanDetailId = u.PlanDetailId,
                YcdtCode = $"YCDT{u.SessionId:D5}",
                ServiceName = u.PlanDetail.ServiceNameSnapshot, Quantity = u.Quantity,
                ExecutionTime = u.ExecutionTime, PerformedByDoctor = u.PerformedByNavigation!.FullName,
                ResultNote = u.ResultNote
            }).ToListAsync(ct);
        return new MedicalRecordDetailDto
        {
            RecordId = record.RecordId, RegistrationId = record.RegistrationId,
            KcbCode = record.Registration.RegistrationCode ?? "", ExamDate = record.ExamDate,
            DoctorName = record.Doctor.FullName, Symptoms = record.Symptoms, Status = record.Status,
            FinalDiagnosis = record.FinalDiagnosis, Palpation = record.Palpation, Inspection = record.Inspection,
            DoctorAdvice = record.DoctorAdvice, Services = services,
            Prescriptions = record.HerbalPrescriptions.OrderByDescending(p => p.CreatedAt).Select(p => new PrescriptionDto
            {
                PrescriptionId = p.PrescriptionId, PrescriptionName = p.PrescriptionName,
                UsageInstructions = p.UsageInstructions, DosageDays = p.DosageDays,
                TotalDoses = p.TotalDoses, Status = p.Status,
                Details = p.HerbalPrescriptionDetails.Select(d => new FormulaDetailItemDto
                {
                    HerbId = d.HerbId, HerbName = d.HerbNameSnapshot, DosagePerDose = d.DosagePerDose,
                    RoleInFormula = d.RoleInFormula, Note = d.Note
                }).ToList()
            }).ToList()
        };
    }

    public async Task<List<TreatmentProtocolDto>> GetTreatmentProtocolsAsync(int patientId, CancellationToken ct = default)
    {
        if (!await db.Patients.AnyAsync(p => p.PatientId == patientId, ct))
            throw new NotFoundException($"Patient with id {patientId} was not found.");
        return await db.TreatmentPlans.AsNoTracking().Where(p => p.Record.PatientId == patientId)
            .OrderByDescending(p => p.CreatedAt).Select(p => new TreatmentProtocolDto
            {
                PlanId = p.PlanId, PlanCode = p.PlanCode, PlanName = p.PlanName, DoctorName = p.Doctor.FullName,
                StartDate = p.StartDate, EndDate = p.EndDate, TotalSessions = p.PlannedSessionCount,
                CompletedSessions = p.TreatmentSessions.Count(s => s.Status == "COMPLETED"), Status = p.Status
            }).ToListAsync(ct);
    }

    public async Task<List<TreatmentSessionDto>> GetTreatmentSessionsAsync(int planId, CancellationToken ct = default)
    {
        if (!await db.TreatmentPlans.AnyAsync(p => p.PlanId == planId, ct))
            throw new NotFoundException($"Treatment plan with id {planId} was not found.");
        return await db.TreatmentSessions.AsNoTracking().Where(s => s.PlanId == planId)
            .OrderBy(s => s.SessionNumber).Select(s => new TreatmentSessionDto
            {
                SessionId = s.SessionId, SessionNo = s.SessionNumber, ExecutionTime = s.StartedAt,
                DoctorName = s.PerformedByNavigation != null ? s.PerformedByNavigation.FullName
                    : s.AssignedDoctor != null ? s.AssignedDoctor.FullName : null,
                ResultNote = s.ResultNote, Status = s.Status
            }).ToListAsync(ct);
    }
    #endregion
}
