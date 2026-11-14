using COSGeniusSystem.Services;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Security.Claims;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services;

public sealed partial class ExaminationService(ApplicationDbContext db, IPatientService? patients = null) : IExaminationService
{
    private static int UserId(ClaimsPrincipal actor) =>
        int.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0
            ? id : throw new UnauthorizedException();

    private static void Reception(ClaimsPrincipal actor)
    {
        if (!actor.IsInRole(RoleConstants.ReceptionStaff)) throw new ForbiddenException();
        _ = UserId(actor);
    }

    private async Task<int> DoctorIdAsync(ClaimsPrincipal actor, CancellationToken ct)
    {
        if (!actor.IsInRole(RoleConstants.Doctor)) throw new ForbiddenException();
        var userId = UserId(actor);
        return await db.Doctors.Where(d => d.UserId == userId && d.IsActive && d.User != null && d.User.IsActive)
            .Select(d => (int?)d.DoctorId).SingleOrDefaultAsync(ct)
            ?? throw new ForbiddenException("The user has no active doctor account.");
    }

    private async Task AccessAsync(ClaimsPrincipal actor, ExaminationRegistration registration, bool clinical, CancellationToken ct)
    {
        if (!clinical && actor.IsInRole(RoleConstants.ReceptionStaff)) return;
        if (registration.DoctorId != await DoctorIdAsync(actor, ct))
            throw new ForbiddenException("This examination is assigned to another doctor.");
    }

    private static void Validate(object request)
    {
        if (request is null) throw new BadRequestException("Request and list items cannot be null.");
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new BadRequestException(string.Join(" ", errors.Select(e => e.ErrorMessage)));
    }

    private static void Version(byte[] actual, byte[] expected)
    {
        if (expected is null || expected.Length != 8) throw new BadRequestException("An 8-byte RowVersion is required.");
        if (!actual.SequenceEqual(expected)) throw new ConflictException("Data changed. Reload the examination and retry with the latest RowVersion.");
    }

    // Every write to an examination, including its child drafts, takes this same lock.
    // This serializes completion against creating/editing/sending either parallel branch.
    private async Task<T> WriteAsync<T>(ClaimsPrincipal actor, int registrationId, bool clinical,
        Func<ExaminationRegistration, Task<T>> action, CancellationToken ct, bool starting = false)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (starting) await DoctorAvailability.LockAsync(db, await DoctorIdAsync(actor, ct), ct);
        var registration = db.Database.IsRelational()
            ? await db.ExaminationRegistrations
                .FromSqlInterpolated($"SELECT * FROM dbo.ExaminationRegistration WITH (UPDLOCK, HOLDLOCK) WHERE RegistrationID = {registrationId}")
                .SingleOrDefaultAsync(ct)
            : await db.ExaminationRegistrations.SingleOrDefaultAsync(r => r.RegistrationId == registrationId, ct);
        if (registration == null) throw new NotFoundException("Registration not found.");
        await AccessAsync(actor, registration, clinical, ct);
        var result = await action(registration);
        await db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return result;
    }

    private async Task<MedicalRecord> RecordAsync(ExaminationRegistration registration, bool editable, CancellationToken ct)
    {
        var record = await db.MedicalRecords.SingleOrDefaultAsync(r => r.RegistrationId == registration.RegistrationId, ct)
            ?? throw new ConflictException("Start the examination first.");
        if (record.PatientId != registration.PatientId || record.DoctorId != registration.DoctorId)
            throw new ConflictException("Registration and medical record are inconsistent.");
        if (editable && (registration.Status != "IN_PROGRESS" || record.Status != "IN_PROGRESS"))
            throw new ConflictException("Clinical content is editable only while the examination is IN_PROGRESS.");
        return record;
    }

    private async Task ValidateAssignmentAsync(int patientId, RegistrationInput request, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.PatientId == patientId, ct)) throw new NotFoundException("Patient not found.");
        if (!await db.Doctors.AnyAsync(d => d.DoctorId == request.DoctorId && d.IsActive && d.User != null && d.User.IsActive
            && d.User.UserRoles.Any(r => r.Role.RoleCode == RoleConstants.Doctor), ct))
            throw new BadRequestException("The assigned doctor must have an active linked user account.");
        if (request.ContactId.HasValue && !await db.PatientContacts.AnyAsync(c => c.ContactId == request.ContactId && c.PatientId == patientId, ct))
            throw new BadRequestException("Contact does not belong to this patient.");
    }

    public async Task<RegistrationResponse> CreateAsync(ClaimsPrincipal actor, CreateRegistrationRequest request, CancellationToken ct)
    {
        Reception(actor);
        Validate(request);
        await ValidateAssignmentAsync(request.PatientId, request, ct);
        var now = DateTime.UtcNow;
        var businessDate = request.BusinessDate ?? DateOnly.FromDateTime(now.AddHours(7));
        ValidateBusinessDate(businessDate);
        var session = string.IsNullOrWhiteSpace(request.Session)
            ? (now.AddHours(7).Hour < 12 ? "MORNING" : "AFTERNOON")
            : request.Session.Trim().ToUpperInvariant();
        if (session is not ("MORNING" or "AFTERNOON"))
            session = now.AddHours(7).Hour < 12 ? "MORNING" : "AFTERNOON";
        var symptoms = request.Symptoms ?? request.DoctorAdvice;

        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        // HOLDLOCK protects the missing-key range on the first registration of the day.
        var counter = db.Database.IsRelational()
            ? await db.ExaminationQueueCounters
                .FromSqlInterpolated($"SELECT * FROM dbo.ExaminationQueueCounter WITH (UPDLOCK, HOLDLOCK) WHERE BusinessDate = {businessDate}")
                .SingleOrDefaultAsync(ct)
            : await db.ExaminationQueueCounters.SingleOrDefaultAsync(c => c.BusinessDate == businessDate, ct);
        if (counter is null)
        {
            counter = new ExaminationQueueCounter { BusinessDate = businessDate };
            db.ExaminationQueueCounters.Add(counter);
        }
        counter.LastNumber = checked(counter.LastNumber + 1);
        var registration = new ExaminationRegistration
        {
            PatientId = request.PatientId, DoctorId = request.DoctorId, ContactId = request.ContactId,
            RegisteredBy = UserId(actor), RegisteredAt = now, BusinessDate = businessDate,
            QueueNumber = counter.LastNumber, Session = session, IsPriority = request.IsPriority,
            Symptoms = symptoms, Note = request.Note, Status = "WAITING"
        };
        db.ExaminationRegistrations.Add(registration);
        await db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);
        return await RegistrationResponseAsync(registration.RegistrationId, ct);
    }

    public async Task<RegistrationResponse> UpdateAsync(ClaimsPrincipal actor, int registrationId, UpdateRegistrationRequest request, CancellationToken ct)
    {
        Reception(actor);
        Validate(request);
        await WriteAsync(actor, registrationId, false, async registration =>
        {
            if (registration.Status != "WAITING") throw new ConflictException("Only WAITING registrations can be updated.");
            Version(registration.RowVersion, request.RowVersion);
            await ValidateAssignmentAsync(registration.PatientId, request, ct);
            var date = request.BusinessDate ?? registration.BusinessDate;
            ValidateBusinessDate(date);
            if (date != registration.BusinessDate)
            {
                var counter = await db.ExaminationQueueCounters
                    .FromSqlInterpolated($"SELECT * FROM dbo.ExaminationQueueCounter WITH (UPDLOCK, HOLDLOCK) WHERE BusinessDate = {date}")
                    .SingleOrDefaultAsync(ct);
                if (counter is null)
                {
                    counter = new ExaminationQueueCounter { BusinessDate = date };
                    db.ExaminationQueueCounters.Add(counter);
                }
                registration.QueueNumber = counter.LastNumber = checked(counter.LastNumber + 1);
                registration.BusinessDate = date;
            }
            var session = string.IsNullOrWhiteSpace(request.Session)
                ? registration.Session
                : request.Session.Trim().ToUpperInvariant();
            if (session is not ("MORNING" or "AFTERNOON"))
                session = registration.Session;
            registration.Session = session;
            registration.DoctorId = request.DoctorId;
            registration.ContactId = request.ContactId;
            registration.Symptoms = request.Symptoms ?? request.DoctorAdvice ?? registration.Symptoms;
            registration.Note = request.Note;
            registration.IsPriority = request.IsPriority;
            registration.UpdatedAt = DateTime.UtcNow;
            return true;
        }, ct);
        return await RegistrationResponseAsync(registrationId, ct);
    }

    public async Task<RegistrationResponse> CancelAsync(ClaimsPrincipal actor, int registrationId, CancelTreatment request, CancellationToken ct)
    {
        Reception(actor);
        Validate(request);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new BadRequestException("Cancellation reason is required.");
        await WriteAsync(actor, registrationId, false, async registration =>
        {
            Version(registration.RowVersion, request.RowVersion);
            if (registration.Status != "WAITING" || registration.StartedAt != null || registration.CompletedAt != null
                || await db.MedicalRecords.AnyAsync(r => r.RegistrationId == registrationId, ct))
                throw new ConflictException("Only unstarted WAITING registrations can be cancelled.");
            var now = DateTime.UtcNow;
            registration.Status = "CANCELLED";
            registration.CancelledAt = now;
            registration.UpdatedAt = now;
            registration.Note = $"{registration.Note}\n[Cancelled {now:O}, user {UserId(actor)}] {request.Reason.Trim()}".Trim();
            return true;
        }, ct);
        return await RegistrationResponseAsync(registrationId, ct);
    }

    public async Task<PagedResult<RegistrationResponse>> ListAsync(ClaimsPrincipal actor, RegistrationQuery query, bool mine, CancellationToken ct)
    {
        Validate(query);
        if (query.FromDate > query.ToDate) throw new BadRequestException("FromDate cannot exceed ToDate.");
        var q = db.ExaminationRegistrations.AsNoTracking();
        if (mine || !actor.IsInRole(RoleConstants.ReceptionStaff))
        {
            var doctorId = await DoctorIdAsync(actor, ct);
            q = q.Where(r => r.DoctorId == doctorId);
        }
        if (query.DoctorId.HasValue) q = q.Where(r => r.DoctorId == query.DoctorId);
        if (query.PatientId.HasValue) q = q.Where(r => r.PatientId == query.PatientId.Value);
        if (query.FromDate.HasValue) q = q.Where(r => r.BusinessDate >= query.FromDate);
        if (query.ToDate.HasValue) q = q.Where(r => r.BusinessDate <= query.ToDate);

        var normalizedStatus = NormalizeStatus(query.Status);
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            q = q.Where(r => r.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(query.Session))
        {
            var s = query.Session.Trim().ToUpperInvariant();
            if (s is "MORNING" or "AFTERNOON")
                q = q.Where(r => r.Session == s);
        }

        var searchTerms = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Search)) searchTerms.Add(query.Search.Trim());
        if (!string.IsNullOrWhiteSpace(query.Keyword) && !searchTerms.Contains(query.Keyword.Trim())) searchTerms.Add(query.Keyword.Trim());

        foreach (var term in searchTerms)
        {
            q = q.Where(r => r.Patient.FullName.Contains(term)
                || (r.Patient.Phone != null && r.Patient.Phone.Contains(term))
                || (r.Patient.PatientCode != null && r.Patient.PatientCode.Contains(term))
                || (r.RegistrationCode != null && r.RegistrationCode.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(query.Phone))
        {
            var p = query.Phone.Trim();
            q = q.Where(r => r.Patient.Phone != null && r.Patient.Phone.Contains(p));
        }

        if (!string.IsNullOrWhiteSpace(query.Code))
        {
            var c = query.Code.Trim();
            q = q.Where(r => (r.RegistrationCode != null && r.RegistrationCode.Contains(c))
                || (r.Patient.PatientCode != null && r.Patient.PatientCode.Contains(c)));
        }

        if (!string.IsNullOrWhiteSpace(query.PatientName))
        {
            var name = query.PatientName.Trim();
            q = q.Where(r => r.Patient.FullName.Contains(name));
        }

        if (!string.IsNullOrWhiteSpace(query.PatientCode))
        {
            var pc = query.PatientCode.Trim();
            q = q.Where(r => r.Patient.PatientCode != null && r.Patient.PatientCode.Contains(pc));
        }

        if (!string.IsNullOrWhiteSpace(query.RegistrationCode))
        {
            var rc = query.RegistrationCode.Trim();
            q = q.Where(r => r.RegistrationCode != null && r.RegistrationCode.Contains(rc));
        }

        var visitCode = query.VisitCode;
        if (!string.IsNullOrWhiteSpace(visitCode))
        {
            var vc = visitCode.Trim();
            q = q.Where(r => (r.RegistrationCode != null && r.RegistrationCode.Contains(vc))
                || (r.Patient.PatientCode != null && r.Patient.PatientCode.Contains(vc)));
        }

        if (!string.IsNullOrWhiteSpace(query.PatientKeyword))
        {
            var pk = query.PatientKeyword.Trim();
            q = q.Where(r => r.Patient.FullName.Contains(pk)
                || (r.Patient.PatientCode != null && r.Patient.PatientCode.Contains(pk)));
        }

        var total = await q.CountAsync(ct);
        var dbItems = await q.OrderByDescending(r => r.BusinessDate)
            .ThenBy(r =>
                (r.Status == "IN_PROGRESS" || r.Status == "Đang khám") ? 1 :
                (r.Status == "WAITING" || r.Status == "Chờ khám") ? 2 :
                (r.Status == "COMPLETED" || r.Status == "Đã hoàn tất" || r.Status == "Hoàn tất") ? 3 : 4)
            .ThenByDescending(r => r.IsPriority)
            .ThenBy(r => r.RegisteredAt)
            .ThenBy(r => r.QueueNumber)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(RegistrationProjection)
            .ToListAsync(ct);

        var items = dbItems.Select((r, index) =>
            r with { QueueNumber = ((query.PageNumber - 1) * query.PageSize) + index + 1 })
            .ToList();

        return new(items, query.PageNumber, query.PageSize, total);
    }

    private static string? NormalizeStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        var upper = trimmed.ToUpperInvariant();
        if (upper is "ALL" or "TẤT CẢ" or "TAT CA") return null;

        if (upper is "WAITING" or "CHỜ KHÁM" or "CHO KHAM" or "CHO_KHAM" or "CHỜ ĐIỀU TRỊ" or "CHO DIEU TRI")
            return "WAITING";
        if (upper is "IN_PROGRESS" or "INPROGRESS" or "ĐANG KHÁM" or "DANG KHAM" or "DANG_KHAM" or "ĐANG ĐIỀU TRỊ" or "DANG DIEU TRI")
            return "IN_PROGRESS";
        if (upper is "COMPLETED" or "HOÀN THÀNH" or "HOAN THANH" or "HOÀN TẤT" or "HOAN TAT" or "ĐÃ HOÀN TẤT" or "DA HOAN TAT")
            return "COMPLETED";
        if (upper is "CANCELLED" or "CANCELED" or "HỦY" or "HUY" or "ĐÃ HỦY" or "DA HUY")
            return "CANCELLED";

        return upper;
    }

    public async Task<ExaminationResponse> GetAsync(ClaimsPrincipal actor, int registrationId, CancellationToken ct)
    {
        var registration = await db.ExaminationRegistrations.AsNoTracking().SingleOrDefaultAsync(r => r.RegistrationId == registrationId, ct)
            ?? throw new NotFoundException("Registration not found.");
        await AccessAsync(actor, registration, false, ct);
        var record = await db.MedicalRecords.AsNoTracking().Where(r => r.RegistrationId == registrationId).Select(RecordProjection).SingleOrDefaultAsync(ct);
        // Patient API mapping and validation remain the single source of patient information.
        var patient = await patients.GetByIdAsync(registration.PatientId, ct);
        IReadOnlyList<PrescriptionResponse> prescriptions = record == null ? [] : await PrescriptionsQuery(record.RecordId).ToListAsync(ct);
        IReadOnlyList<TreatmentPlanResponse> plans = record == null ? [] : await PlansQuery(record.RecordId).ToListAsync(ct);
        return new(await RegistrationResponseAsync(registrationId, ct), patient, record, prescriptions, plans);
    }

    public async Task<PagedResult<MedicalRecordResponse>> HistoryAsync(ClaimsPrincipal actor, int registrationId, PageQuery query, CancellationToken ct)
    {
        Validate(query);
        var registration = await db.ExaminationRegistrations.AsNoTracking().SingleOrDefaultAsync(r => r.RegistrationId == registrationId, ct)
            ?? throw new NotFoundException("Registration not found.");
        await AccessAsync(actor, registration, true, ct);
        var q = db.MedicalRecords.AsNoTracking().Where(r => r.PatientId == registration.PatientId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.ExamDate).ThenByDescending(r => r.RecordId)
            .Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).Select(RecordProjection).ToListAsync(ct);
        return new(items, query.PageNumber, query.PageSize, total);
    }

    public async Task<MedicalRecordResponse> StartAsync(ClaimsPrincipal actor, int registrationId, CancellationToken ct)
    {
        await WriteAsync(actor, registrationId, true, async registration =>
        {
            var existing = await db.MedicalRecords.SingleOrDefaultAsync(r => r.RegistrationId == registrationId, ct);
            if (existing != null)
            {
                if (registration.Status is not ("IN_PROGRESS" or "COMPLETED") || existing.Status != registration.Status
                    || existing.PatientId != registration.PatientId || existing.DoctorId != registration.DoctorId)
                    throw new ConflictException("Registration and medical record are inconsistent.");
                return true;
            }
            if (registration.Status != "WAITING") throw new ConflictException("Only WAITING registrations can be started.");
            await DoctorAvailability.EnsureAvailableAsync(db, registration.DoctorId, ct);
            var now = DateTime.UtcNow;
            var allergy = await db.Patients.Where(p => p.PatientId == registration.PatientId).Select(p => p.AllergyNote).SingleAsync(ct);
            registration.Status = "IN_PROGRESS";
            registration.StartedAt = now;
            registration.UpdatedAt = now;
            // Save parent status before inserting the child for database consistency triggers.
            await db.SaveChangesAsync(ct);
            db.MedicalRecords.Add(new MedicalRecord
            {
                RegistrationId = registrationId, PatientId = registration.PatientId, DoctorId = registration.DoctorId,
                ExamDate = now, Status = "IN_PROGRESS", Symptoms = registration.Symptoms, AllergyNoteSnapshot = allergy
            });
            return true;
        }, ct, starting: true);
        return await RecordResponseAsync(registrationId, ct);
    }

    public async Task<ExaminationResponse> HistoryDetailAsync(ClaimsPrincipal actor, int registrationId, int recordId, CancellationToken ct)
    {
        var current = await db.ExaminationRegistrations.AsNoTracking().SingleOrDefaultAsync(r => r.RegistrationId == registrationId, ct)
            ?? throw new NotFoundException("Registration not found.");
        await AccessAsync(actor, current, true, ct);
        var record = await db.MedicalRecords.AsNoTracking().Where(r => r.RecordId == recordId && r.PatientId == current.PatientId)
            .Select(RecordProjection).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Historical record does not belong to this patient.");
        return new(await RegistrationResponseAsync(record.RegistrationId, ct), await patients.GetByIdAsync(current.PatientId, ct),
            record, await PrescriptionsQuery(record.RecordId).ToListAsync(ct), await PlansQuery(record.RecordId).ToListAsync(ct));
    }

    public async Task<MedicalRecordResponse> SaveAsync(ClaimsPrincipal actor, int registrationId, SaveMedicalRecordRequest request, CancellationToken ct)
    {
        Validate(request);
        await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, true, ct);
            Version(record.RowVersion, request.RowVersion);
            ApplyClinical(request, record);
            record.UpdatedAt = DateTime.UtcNow;
            return true;
        }, ct);
        return await RecordResponseAsync(registrationId, ct);
    }

    public async Task<MedicalRecordResponse> CompleteAsync(ClaimsPrincipal actor, int registrationId, VersionRequest request, CancellationToken ct)
    {
        Validate(request);
        await WriteAsync(actor, registrationId, true, async registration =>
        {
            var record = await RecordAsync(registration, false, ct);
            if (record.Status == "COMPLETED" && registration.Status == "COMPLETED") return true;
            if (record.Status != "IN_PROGRESS" || registration.Status != "IN_PROGRESS") throw new ConflictException("The examination is not IN_PROGRESS.");
            Version(record.RowVersion, request.RowVersion);
            if (string.IsNullOrWhiteSpace(record.Symptoms) || string.IsNullOrWhiteSpace(record.FinalDiagnosis))
                throw new BadRequestException("Symptoms and FinalDiagnosis are required to complete an examination.");
            // Publish saved drafts atomically with completion.
            var prescriptions = await db.HerbalPrescriptions.Include(p => p.HerbalPrescriptionDetails)
                .Where(p => p.RecordId == record.RecordId && (p.Status == "DRAFT" || p.Status == "SENT")).ToListAsync(ct);
            var plans = await db.TreatmentPlans.Include(p => p.TreatmentPlanDetails)
                .Where(p => p.RecordId == record.RecordId && (p.Status == "DRAFT" || p.Status == "CONFIRMED")).ToListAsync(ct);
            foreach (var prescription in prescriptions) await SendPrescriptionContentAsync(prescription, ct);
            foreach (var plan in plans) await ConfirmPlanContentAsync(plan, ct);
            var now = DateTime.UtcNow;
            record.Status = "COMPLETED";
            record.CompletedAt = now;
            record.UpdatedAt = now;
            registration.Status = "COMPLETED";
            registration.CompletedAt = now;
            registration.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            await InvoiceAccrual.GenerateAsync(db, record.RecordId, ct);
            return true;
        }, ct);
        return await RecordResponseAsync(registrationId, ct);
    }

    private Task<RegistrationResponse> RegistrationResponseAsync(int id, CancellationToken ct) =>
        db.ExaminationRegistrations.AsNoTracking().Where(r => r.RegistrationId == id).Select(RegistrationProjection).SingleAsync(ct);

    private Task<MedicalRecordResponse> RecordResponseAsync(int registrationId, CancellationToken ct) =>
        db.MedicalRecords.AsNoTracking().Where(r => r.RegistrationId == registrationId).Select(RecordProjection).SingleAsync(ct);

    private static readonly Expression<Func<ExaminationRegistration, RegistrationResponse>> RegistrationProjection = r =>
        new(r.RegistrationId, r.RegistrationCode, r.PatientId,
            r.Patient != null ? r.Patient.PatientCode : null,
            r.Patient != null ? r.Patient.FullName : "Không xác định",
            r.Patient != null ? r.Patient.Phone : null,
            r.DoctorId,
            r.Doctor != null ? r.Doctor.FullName : "Chưa phân công",
            r.RegisteredBy, r.RegisteredAt, r.BusinessDate, r.QueueNumber, r.IsPriority,
            r.ContactId, r.Contact == null ? null : r.Contact.FullName, r.Symptoms, r.Note, r.Status,
            r.StartedAt, r.CompletedAt, r.MedicalRecord == null ? null : r.MedicalRecord.RecordId, r.RowVersion, r.Session);

    private static readonly Expression<Func<MedicalRecord, MedicalRecordResponse>> RecordProjection = r => new()
    {
        RecordId = r.RecordId, RegistrationId = r.RegistrationId, PatientId = r.PatientId, DoctorId = r.DoctorId,
        ExamDate = r.ExamDate, Status = r.Status, CompletedAt = r.CompletedAt, RowVersion = r.RowVersion,
        AllergyNoteSnapshot = r.AllergyNoteSnapshot,
        Symptoms = r.Symptoms, PreliminaryDiagnosis = r.PreliminaryDiagnosis, FinalDiagnosis = r.FinalDiagnosis,
        DoctorAdvice = r.DoctorAdvice, PulseRate = r.PulseRate, Temperature = r.Temperature,
        RespiratoryRate = r.RespiratoryRate, HeightCm = r.HeightCm, WeightKg = r.WeightKg, SpO2 = r.SpO2,
        Inspection = r.Inspection, ListeningSmelling = r.ListeningSmelling, Inquiry = r.Inquiry, Palpation = r.Palpation,
        FourExaminationSummary = r.FourExaminationSummary, SyndromeDifferentiation = r.SyndromeDifferentiation,
        TraditionalDiagnosis = r.TraditionalDiagnosis, Icdcode = r.Icdcode, Icddescription = r.Icddescription
    };

    private static void ApplyClinical(MedicalRecordData source, MedicalRecord target)
    {
        target.Symptoms = source.Symptoms;
        target.PreliminaryDiagnosis = source.PreliminaryDiagnosis;
        target.FinalDiagnosis = source.FinalDiagnosis;
        target.DoctorAdvice = source.DoctorAdvice;
        target.PulseRate = source.PulseRate;
        target.Temperature = source.Temperature;
        target.RespiratoryRate = source.RespiratoryRate;
        target.HeightCm = source.HeightCm;
        target.WeightKg = source.WeightKg;
        target.SpO2 = source.SpO2;
        target.Inspection = source.Inspection;
        target.ListeningSmelling = source.ListeningSmelling;
        target.Inquiry = source.Inquiry;
        target.Palpation = source.Palpation;
        target.FourExaminationSummary = source.FourExaminationSummary;
        target.SyndromeDifferentiation = source.SyndromeDifferentiation;
        target.TraditionalDiagnosis = source.TraditionalDiagnosis;
        target.Icdcode = source.Icdcode;
        target.Icddescription = source.Icddescription;
    }
}
