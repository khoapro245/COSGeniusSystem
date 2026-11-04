using System.Text.Json;
using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.DoctorClinical;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Impl;

public class DoctorClinicalService(ApplicationDbContext db) : IDoctorClinicalService
{
    public async Task<DoctorExamQueueResponseDto> GetExamQueueAsync(DoctorExamFilterDto filter, CancellationToken ct = default)
    {
        var fromDate = filter.FromDate?.Date ?? DateTime.Today;
        var toDate = (filter.ToDate?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

        var dbQuery = db.ExamSheets.AsNoTracking()
            .Include(e => e.Doctor)
            .Include(e => e.Record).ThenInclude(r => r.Patient)
            .Where(e => e.ExamDate >= fromDate && e.ExamDate <= toDate);

        if (filter.DoctorId.HasValue && filter.DoctorId.Value > 0)
        {
            dbQuery = dbQuery.Where(e => e.DoctorId == filter.DoctorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.PatientName))
        {
            var kw = filter.PatientName.Trim();
            dbQuery = dbQuery.Where(e => e.Record.Patient.FullName.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(filter.PatientCode))
        {
            var kw = filter.PatientCode.Trim();
            dbQuery = dbQuery.Where(e => e.Record.Patient.PatientCode != null && e.Record.Patient.PatientCode.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(filter.Phone))
        {
            var kw = filter.Phone.Trim();
            dbQuery = dbQuery.Where(e => e.Record.Patient.Phone != null && e.Record.Patient.Phone.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(filter.Gender))
        {
            var g = filter.Gender.Trim();
            dbQuery = dbQuery.Where(e => e.Record.Patient.Gender == g);
        }

        if (!string.IsNullOrWhiteSpace(filter.VisitCode))
        {
            var vc = filter.VisitCode.Trim();
            if (vc.StartsWith("KCB", StringComparison.OrdinalIgnoreCase) && int.TryParse(vc[3..], out var parsedId))
            {
                dbQuery = dbQuery.Where(e => e.ExamSheetId == parsedId);
            }
        }

        var examSheets = await dbQuery.ToListAsync(ct);

        // Parse metadata và map
        var itemsWithMeta = examSheets.Select((es, idx) =>
        {
            var meta = ClinicalMetadataJson.FromExamSheet(es);
            var age = es.Record.Patient.DateOfBirth.HasValue
                ? DateTime.Today.Year - es.Record.Patient.DateOfBirth.Value.Year
                : 0;

            var action = meta.Status switch
            {
                "Chờ khám" => "Gọi vào khám",
                "Đang khám" => "Tiếp tục khám",
                _ => "Xem hồ sơ"
            };

            return new
            {
                ExamSheet = es,
                Meta = meta,
                Item = new DoctorExamQueueItemDto
                {
                    ExamSheetId = es.ExamSheetId,
                    QueueNumber = meta.QueueNumber ?? $"{idx + 1:D3}",
                    IsPriority = meta.IsPriority,
                    PatientId = es.Record.PatientId,
                    PatientCode = es.Record.Patient.PatientCode ?? $"BN{es.Record.PatientId:D5}",
                    PatientName = es.Record.Patient.FullName,
                    RegisteredAt = es.ExamDate,
                    VisitCode = $"KCB{es.ExamSheetId:D5}",
                    Age = age,
                    Gender = es.Record.Patient.Gender ?? "Khác",
                    Phone = es.Record.Patient.Phone ?? "",
                    Status = meta.Status,
                    AllowedAction = action
                }
            };
        }).ToList();

        // Đếm trạng thái trước khi lọc status cụ thể
        var waitingCount = itemsWithMeta.Count(x => x.Meta.Status == "Chờ khám");
        var examiningCount = itemsWithMeta.Count(x => x.Meta.Status == "Đang khám");
        var completedCount = itemsWithMeta.Count(x => x.Meta.Status == "Hoàn tất");
        var totalCount = itemsWithMeta.Count;

        // Lọc theo Status nếu được chỉ định
        var filteredItems = itemsWithMeta.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filter.Status) && filter.Status != "Tất cả")
        {
            var st = filter.Status.Trim();
            filteredItems = filteredItems.Where(x => x.Meta.Status.Equals(st, StringComparison.OrdinalIgnoreCase));
        }

        // Sắp xếp: Ưu tiên lên đầu, sau đó theo thời gian đăng ký
        var sortedItems = filteredItems
            .OrderByDescending(x => x.Meta.IsPriority)
            .ThenBy(x => x.ExamSheet.ExamDate)
            .Select(x => x.Item)
            .ToList();

        // Đánh lại số thứ tự QueueNumber hiển thị
        for (var i = 0; i < sortedItems.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(sortedItems[i].QueueNumber) || sortedItems[i].QueueNumber == "000")
            {
                sortedItems[i].QueueNumber = $"{i + 1:D3}";
            }
        }

        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var pagedItems = sortedItems.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return new DoctorExamQueueResponseDto
        {
            WaitingCount = waitingCount,
            ExaminingCount = examiningCount,
            CompletedCount = completedCount,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Items = pagedItems
        };
    }

    public async Task<MedicalRecordWorkspaceDto> CallPatientAsync(int examSheetId, CancellationToken ct = default)
    {
        var es = await db.ExamSheets
            .Include(e => e.Doctor)
            .Include(e => e.Record).ThenInclude(r => r.Patient).ThenInclude(p => p.PatientContacts)
            .FirstOrDefaultAsync(e => e.ExamSheetId == examSheetId, ct)
            ?? throw new NotFoundException($"Exam sheet with id {examSheetId} was not found.");

        var meta = ClinicalMetadataJson.FromExamSheet(es);

        if (meta.Status == "Hoàn tất")
        {
            throw new BadRequestException("Lượt khám đã hoàn tất, không thể gọi vào khám.");
        }

        // Cập nhật trạng thái sang Đang khám và ghi nhận thời gian bắt đầu khám thực tế
        meta.Status = "Đang khám";
        meta.ExamStartTime ??= DateTime.Now;

        es.DoctorAdvice = meta.ToJson();
        es.FinalDiagnosis = "Đang khám";
        es.ExaminationStatus = "IN_PROGRESS";
        es.StartedAt ??= DateTime.Now;
        es.Record.Status = "Đang khám";

        await db.SaveChangesAsync(ct);

        return await BuildWorkspaceDtoAsync(es, meta, ct);
    }

    public async Task<MedicalRecordWorkspaceDto> GetMedicalRecordWorkspaceAsync(int examSheetId, CancellationToken ct = default)
    {
        var es = await db.ExamSheets
            .Include(e => e.Doctor)
            .Include(e => e.Record).ThenInclude(r => r.Patient).ThenInclude(p => p.PatientContacts)
            .FirstOrDefaultAsync(e => e.ExamSheetId == examSheetId, ct)
            ?? throw new NotFoundException($"Exam sheet with id {examSheetId} was not found.");

        var meta = ClinicalMetadataJson.FromExamSheet(es);
        return await BuildWorkspaceDtoAsync(es, meta, ct);
    }

    public async Task<MedicalRecordWorkspaceDto> SaveClinicalExamAsync(int examSheetId, SaveClinicalExamRequestDto request, CancellationToken ct = default)
    {
        var es = await db.ExamSheets
            .Include(e => e.Doctor)
            .Include(e => e.Record).ThenInclude(r => r.Patient).ThenInclude(p => p.PatientContacts)
            .FirstOrDefaultAsync(e => e.ExamSheetId == examSheetId, ct)
            ?? throw new NotFoundException($"Exam sheet with id {examSheetId} was not found.");

        var meta = ClinicalMetadataJson.FromExamSheet(es);

        if (meta.Status == "Hoàn tất")
        {
            throw new BadRequestException("Hồ sơ đã hoàn tất chỉ được xem, không thể chỉnh sửa.");
        }

        // Cập nhật sang Đang khám nếu đang là Chờ khám
        meta.Status = "Đang khám";
        meta.ExamStartTime = request.ExamStartTime ?? meta.ExamStartTime ?? DateTime.Now;

        if (request.VitalSigns != null)
        {
            meta.PulseRate = request.VitalSigns.PulseRate;
            meta.Temperature = request.VitalSigns.Temperature;
            meta.RespiratoryRate = request.VitalSigns.RespiratoryRate;
            meta.Height = request.VitalSigns.Height;
            meta.Weight = request.VitalSigns.Weight;
            meta.SpO2 = request.VitalSigns.SpO2;

            es.PulseRate = (short?)meta.PulseRate;
            es.Temperature = meta.Temperature;
            es.RespiratoryRate = (short?)meta.RespiratoryRate;
            es.HeightCm = meta.Height;
            es.WeightKg = meta.Weight;
            es.SpO2 = meta.SpO2;
        }

        if (request.Symptoms != null)
        {
            meta.Symptoms = request.Symptoms;
            var sym = meta.Symptoms;
            if (meta.IsPriority && !sym.Contains("[Ưu tiên]"))
                sym = "[Ưu tiên] " + sym;
            es.Symptoms = sym;
        }

        if (request.PreliminaryDiagnosis != null)
        {
            meta.PreliminaryDiagnosis = request.PreliminaryDiagnosis;
            es.PreliminaryDiagnosis = meta.PreliminaryDiagnosis;
        }

        if (request.TraditionalMedicine != null)
        {
            meta.VongChan = request.TraditionalMedicine.VongChan;
            meta.VanChan_NgheNgui = request.TraditionalMedicine.VanChan_NgheNgui;
            meta.VanChan_Hoi = request.TraditionalMedicine.VanChan_Hoi;
            meta.ThietChan = request.TraditionalMedicine.ThietChan;
            meta.FourExamsSummary = request.TraditionalMedicine.FourExamsSummary;
            meta.BienChung = request.TraditionalMedicine.BienChung;
            meta.TraditionalDiagnosis = request.TraditionalMedicine.TraditionalDiagnosis;

            es.TongueDiagnosis = meta.VongChan;
            es.PulseDiagnosis = meta.ThietChan;
            es.Inspection = meta.VongChan;
            es.ListeningSmelling = meta.VanChan_NgheNgui;
            es.Inquiry = meta.VanChan_Hoi;
            es.Palpation = meta.ThietChan;
            es.FourExaminationSummary = meta.FourExamsSummary;
            es.SyndromeDifferentiation = meta.BienChung;
            es.TraditionalDiagnosis = meta.TraditionalDiagnosis;
        }

        if (request.IcdCode != null)
        {
            meta.IcdCode = request.IcdCode;
            es.Icdcode = request.IcdCode;
        }

        if (request.IcdName != null)
        {
            meta.IcdName = request.IcdName;
            es.Icddescription = request.IcdName;
        }

        es.DoctorAdvice = meta.ToJson();
        es.FinalDiagnosis = meta.TraditionalDiagnosis ?? "Đang khám";
        es.ExaminationStatus = "IN_PROGRESS";
        es.StartedAt ??= meta.ExamStartTime;

        await db.SaveChangesAsync(ct);

        return await BuildWorkspaceDtoAsync(es, meta, ct);
    }

    public async Task<MedicalRecordWorkspaceDto> CompleteExaminationAsync(int examSheetId, SaveClinicalExamRequestDto? request, CancellationToken ct = default)
    {
        var es = await db.ExamSheets
            .Include(e => e.Doctor)
            .Include(e => e.Record).ThenInclude(r => r.Patient).ThenInclude(p => p.PatientContacts)
            .FirstOrDefaultAsync(e => e.ExamSheetId == examSheetId, ct)
            ?? throw new NotFoundException($"Exam sheet with id {examSheetId} was not found.");

        var meta = ClinicalMetadataJson.FromExamSheet(es);

        if (meta.Status == "Hoàn tất")
        {
            throw new BadRequestException("Lượt khám đã hoàn tất trước đó.");
        }

        // Nếu có truyền request cập nhật kèm theo lúc bấm hoàn tất
        if (request != null)
        {
            if (request.VitalSigns != null)
            {
                meta.PulseRate = request.VitalSigns.PulseRate;
                meta.Temperature = request.VitalSigns.Temperature;
                meta.RespiratoryRate = request.VitalSigns.RespiratoryRate;
                meta.Height = request.VitalSigns.Height;
                meta.Weight = request.VitalSigns.Weight;
                meta.SpO2 = request.VitalSigns.SpO2;
            }

            if (request.Symptoms != null) meta.Symptoms = request.Symptoms;
            if (request.PreliminaryDiagnosis != null) meta.PreliminaryDiagnosis = request.PreliminaryDiagnosis;

            if (request.TraditionalMedicine != null)
            {
                meta.VongChan = request.TraditionalMedicine.VongChan;
                meta.VanChan_NgheNgui = request.TraditionalMedicine.VanChan_NgheNgui;
                meta.VanChan_Hoi = request.TraditionalMedicine.VanChan_Hoi;
                meta.ThietChan = request.TraditionalMedicine.ThietChan;
                meta.FourExamsSummary = request.TraditionalMedicine.FourExamsSummary;
                meta.BienChung = request.TraditionalMedicine.BienChung;
                meta.TraditionalDiagnosis = request.TraditionalMedicine.TraditionalDiagnosis;
            }

            if (request.IcdCode != null) meta.IcdCode = request.IcdCode;
            if (request.IcdName != null) meta.IcdName = request.IcdName;
        }

        meta.Status = "Hoàn tất";
        meta.CompletedAt = DateTime.Now;

        es.DoctorAdvice = meta.ToJson();
        es.FinalDiagnosis = !string.IsNullOrWhiteSpace(meta.TraditionalDiagnosis) ? meta.TraditionalDiagnosis : "Hoàn tất";
        es.ExaminationStatus = "COMPLETED";
        es.CompletedAt = DateTime.Now;
        es.Record.Status = "Hoàn tất";

        await db.SaveChangesAsync(ct);

        return await BuildWorkspaceDtoAsync(es, meta, ct);
    }

    public async Task<ServiceIndicationDto> SaveServiceIndicationAsync(int examSheetId, SaveServiceIndicationRequestDto request, CancellationToken ct = default)
    {
        var es = await db.ExamSheets
            .Include(e => e.Record)
            .FirstOrDefaultAsync(e => e.ExamSheetId == examSheetId, ct)
            ?? throw new NotFoundException($"Exam sheet with id {examSheetId} was not found.");

        var meta = ClinicalMetadataJson.FromExamSheet(es);

        if (meta.Status == "Hoàn tất")
        {
            throw new BadRequestException("Lượt khám đã hoàn tất, không thể chỉnh sửa chỉ định dịch vụ.");
        }

        meta.TreatmentType = request.TreatmentType;
        meta.TotalSessions = request.TotalSessions > 0 ? request.TotalSessions : 1;
        meta.IndicationNote = request.IndicationNote;
        meta.AdditionalContent = request.AdditionalContent;

        // Xử lý tạo/cập nhật phác đồ nếu theo liệu trình
        if (request.TreatmentType == "Theo liệu trình")
        {
            var protocol = await db.TreatmentProtocols
                .FirstOrDefaultAsync(p => p.ExamSheetId == examSheetId, ct);

            if (protocol == null)
            {
                protocol = new TreatmentProtocol
                {
                    RecordId = es.RecordId,
                    DoctorId = es.DoctorId,
                    ExamSheetId = examSheetId,
                    ProtocolName = $"Liệu trình điều trị KCB{examSheetId:D5}",
                    TreatmentType = "Theo liệu trình",
                    PlannedSessionCount = request.TotalSessions,
                    Description = request.IndicationNote,
                    StartDate = DateOnly.FromDateTime(DateTime.Today),
                    PlanStatus = "ACTIVE",
                    CreatedAt = DateTime.Now
                };
                db.TreatmentProtocols.Add(protocol);
            }
            else
            {
                protocol.PlannedSessionCount = request.TotalSessions;
                protocol.Description = request.IndicationNote;
                protocol.UpdatedAt = DateTime.Now;
            }
        }

        // Cập nhật các chỉ định dịch vụ trong UsageHistory
        var existingUsage = await db.UsageHistories
            .Where(u => u.ExamSheetId == examSheetId)
            .ToListAsync(ct);

        db.UsageHistories.RemoveRange(existingUsage);

        foreach (var item in request.Services)
        {
            var uh = new UsageHistory
            {
                ExamSheetId = examSheetId,
                ServiceId = item.ServiceId,
                PerformedBy = es.DoctorId,
                Quantity = 1,
                ExecutionTime = DateTime.Now,
                ResultNote = item.Note ?? request.IndicationNote ?? "Chỉ định dịch vụ"
            };
            db.UsageHistories.Add(uh);
        }

        es.DoctorAdvice = meta.ToJson();
        await db.SaveChangesAsync(ct);

        // Nạp danh sách dịch vụ kèm thông tin chi tiết
        var serviceIds = request.Services.Select(s => s.ServiceId).ToList();
        var servicesDict = await db.Services.AsNoTracking()
            .Where(s => serviceIds.Contains(s.ServiceId))
            .ToDictionaryAsync(s => s.ServiceId, ct);

        var resultItems = request.Services.Select(s =>
        {
            servicesDict.TryGetValue(s.ServiceId, out var svc);
            return new IndicatedServiceItemDto
            {
                ServiceId = s.ServiceId,
                ServiceCode = svc?.ServiceCode ?? "",
                ServiceName = svc?.ServiceName ?? "",
                TreatmentType = request.TreatmentType,
                Note = s.Note ?? request.IndicationNote ?? "",
                Price = svc?.Price ?? 0,
                Unit = svc?.Unit ?? "Lần"
            };
        }).ToList();

        return new ServiceIndicationDto
        {
            TreatmentType = request.TreatmentType,
            TotalSessions = request.TotalSessions,
            IndicationNote = request.IndicationNote ?? "",
            AdditionalContent = request.AdditionalContent ?? "",
            Services = resultItems
        };
    }

    public async Task<PrescriptionCatalogDto> GetPrescriptionCatalogAsync(CancellationToken ct = default)
    {
        // 1. Lấy danh sách bài thuốc mẫu (RemedyFormula có sẵn trong hệ thống)
        var templateFormulas = await db.RemedyFormulas.AsNoTracking()
            .Include(f => f.FormulaDetails).ThenInclude(fd => fd.Herb)
            .Where(f => f.ExamSheetId == null || f.FormulaDetails.Any())
            .OrderBy(f => f.FormulaId)
            .Take(50)
            .ToListAsync(ct);

        var formulaTemplates = templateFormulas.Select(f => new FormulaTemplateDto
        {
            FormulaId = f.FormulaId,
            FormulaName = f.FormulaName,
            UsageInstructions = f.UsageInstructions,
            DosageDays = f.DosageDays,
            TotalDoses = f.TotalDoses,
            Ingredients = f.FormulaDetails.Select(fd => new FormulaTemplateIngredientDto
            {
                HerbId = fd.HerbId,
                HerbCode = fd.Herb?.HerbCode ?? "",
                HerbName = fd.Herb?.CommonName ?? "",
                DosagePerDose = fd.DosagePerDose,
                RoleInFormula = fd.RoleInFormula,
                Note = fd.Note
            }).ToList()
        }).ToList();

        // 2. Lấy danh sách dược liệu đang hoạt động
        var herbs = await db.Herbs.AsNoTracking()
            .Where(h => h.IsActive)
            .OrderBy(h => h.CommonName)
            .Select(h => new HerbItemDto
            {
                HerbId = h.HerbId,
                HerbCode = h.HerbCode,
                CommonName = h.CommonName,
                Unit = h.Unit,
                StandardPrice = h.StandardPrice,
                IsActive = h.IsActive
            })
            .ToListAsync(ct);

        return new PrescriptionCatalogDto
        {
            FormulaTemplates = formulaTemplates,
            Herbs = herbs
        };
    }

    public async Task<PrescriptionDetailDto> SavePrescriptionAsync(int examSheetId, SavePrescriptionRequestDto request, CancellationToken ct = default)
    {
        var es = await db.ExamSheets
            .FirstOrDefaultAsync(e => e.ExamSheetId == examSheetId, ct)
            ?? throw new NotFoundException($"Exam sheet with id {examSheetId} was not found.");

        var meta = ClinicalMetadataJson.FromExamSheet(es);

        if (meta.Status == "Hoàn tất")
        {
            throw new BadRequestException("Lượt khám đã hoàn tất, không thể chỉnh sửa đơn thuốc.");
        }

        if (meta.PrescriptionStatus == "Đã gửi Herbalist")
        {
            throw new BadRequestException("Sau khi gửi Herbalist, nội dung đơn không còn được chỉnh sửa ở màn kê đơn.");
        }

        var isSend = request.ActionType.Equals("SEND_HERBALIST", StringComparison.OrdinalIgnoreCase);

        // Tìm hoặc tạo RemedyFormula gắn với ExamSheetId
        var formula = await db.RemedyFormulas
            .Include(f => f.FormulaDetails)
            .FirstOrDefaultAsync(f => f.ExamSheetId == examSheetId, ct);

        if (formula == null)
        {
            formula = new RemedyFormula
            {
                ExamSheetId = examSheetId,
                FormulaName = $"Đơn thuốc KCB{examSheetId:D5}",
                UsageInstructions = request.PrescriptionNote,
                DosageDays = 5,
                TotalDoses = request.TotalDoses > 0 ? request.TotalDoses : 1,
                CreatedAt = DateTime.Now
            };
            db.RemedyFormulas.Add(formula);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            formula.UsageInstructions = request.PrescriptionNote;
            formula.TotalDoses = request.TotalDoses > 0 ? request.TotalDoses : 1;
            db.FormulaDetails.RemoveRange(formula.FormulaDetails);
            await db.SaveChangesAsync(ct);
        }

        // Thêm các vị thuốc mới
        foreach (var ing in request.Ingredients)
        {
            var detail = new FormulaDetail
            {
                FormulaId = formula.FormulaId,
                HerbId = ing.HerbId,
                DosagePerDose = ing.GramsPerDose,
                Note = ing.Note,
                RoleInFormula = "Vị thuốc"
            };
            db.FormulaDetails.Add(detail);
        }

        // Cập nhật metadata đơn thuốc
        meta.PrescriptionStatus = isSend ? "Đã gửi Herbalist" : "Nháp";
        meta.PrescriptionMethod = request.PrescriptionMethod ?? "Tự kê đơn";
        meta.TemplateFormulaId = request.TemplateFormulaId;
        meta.TotalDoses = formula.TotalDoses;
        meta.PrescriptionNote = request.PrescriptionNote;
        meta.PrescriptionCode = $"DT{formula.FormulaId:D5}";

        es.DoctorAdvice = meta.ToJson();
        await db.SaveChangesAsync(ct);

        // Nạp danh sách vị thuốc kèm tên dược liệu
        var herbIds = request.Ingredients.Select(i => i.HerbId).ToList();
        var herbsDict = await db.Herbs.AsNoTracking()
            .Where(h => herbIds.Contains(h.HerbId))
            .ToDictionaryAsync(h => h.HerbId, ct);

        string? templateName = null;
        if (request.TemplateFormulaId.HasValue)
        {
            templateName = await db.RemedyFormulas.AsNoTracking()
                .Where(f => f.FormulaId == request.TemplateFormulaId.Value)
                .Select(f => f.FormulaName)
                .FirstOrDefaultAsync(ct);
        }

        var ingredientsResult = request.Ingredients.Select(ing =>
        {
            herbsDict.TryGetValue(ing.HerbId, out var h);
            var grams = ing.GramsPerDose;
            var totalGrams = grams * formula.TotalDoses;
            return new PrescriptionIngredientDto
            {
                HerbId = ing.HerbId,
                HerbCode = h?.HerbCode ?? "",
                HerbName = h?.CommonName ?? "",
                GramsPerDose = grams,
                TotalDoses = formula.TotalDoses,
                TotalGrams = totalGrams,
                Note = ing.Note
            };
        }).ToList();

        return new PrescriptionDetailDto
        {
            FormulaId = formula.FormulaId,
            PrescriptionCode = meta.PrescriptionCode,
            PrescriptionMethod = meta.PrescriptionMethod,
            TemplateFormulaId = meta.TemplateFormulaId,
            TemplateFormulaName = templateName,
            TotalDoses = formula.TotalDoses,
            PrescriptionNote = meta.PrescriptionNote ?? "",
            Status = meta.PrescriptionStatus,
            IsLocked = isSend,
            Ingredients = ingredientsResult
        };
    }

    private async Task<MedicalRecordWorkspaceDto> BuildWorkspaceDtoAsync(ExamSheet es, ClinicalMetadataJson meta, CancellationToken ct)
    {
        var patient = es.Record.Patient;
        var age = patient.DateOfBirth.HasValue
            ? DateTime.Today.Year - patient.DateOfBirth.Value.Year
            : 0;

        var contactPerson = patient.PatientContacts.FirstOrDefault()?.FullName ?? "";

        var banner = new PatientSummaryBannerDto
        {
            PatientId = patient.PatientId,
            PatientCode = patient.PatientCode ?? $"BN{patient.PatientId:D5}",
            FullName = patient.FullName,
            DateOfBirth = patient.DateOfBirth.HasValue ? patient.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue) : null,
            Age = age,
            Gender = patient.Gender ?? "Khác",
            Phone = patient.Phone ?? "",
            Address = patient.Address ?? "",
            ContactPersonName = contactPerson,
            RegisteredAt = es.ExamDate,
            AllergyHistory = patient.AllergyNote ?? "Không có"
        };

        var clinicalExam = new ClinicalExamDataDto
        {
            ExamStartTime = meta.ExamStartTime ?? es.StartedAt ?? es.ExamDate,
            DoctorId = es.DoctorId,
            DoctorName = es.Doctor?.FullName ?? "",
            VitalSigns = new VitalSignsDto
            {
                PulseRate = meta.PulseRate ?? es.PulseRate,
                Temperature = meta.Temperature ?? es.Temperature,
                RespiratoryRate = meta.RespiratoryRate ?? es.RespiratoryRate,
                Height = meta.Height ?? es.HeightCm,
                Weight = meta.Weight ?? es.WeightKg,
                SpO2 = meta.SpO2 ?? es.SpO2
            },
            Symptoms = meta.Symptoms ?? es.Symptoms ?? "",
            PreliminaryDiagnosis = meta.PreliminaryDiagnosis ?? es.PreliminaryDiagnosis ?? "",
            TraditionalMedicine = new TraditionalFourExamsDto
            {
                VongChan = meta.VongChan ?? es.TongueDiagnosis ?? es.Inspection ?? "",
                VanChan_NgheNgui = meta.VanChan_NgheNgui ?? es.ListeningSmelling ?? "",
                VanChan_Hoi = meta.VanChan_Hoi ?? es.Inquiry ?? "",
                ThietChan = meta.ThietChan ?? es.PulseDiagnosis ?? es.Palpation ?? "",
                FourExamsSummary = meta.FourExamsSummary ?? es.FourExaminationSummary ?? "",
                BienChung = meta.BienChung ?? es.SyndromeDifferentiation ?? "",
                TraditionalDiagnosis = meta.TraditionalDiagnosis ?? es.TraditionalDiagnosis ?? ""
            },
            IcdCode = meta.IcdCode ?? es.Icdcode ?? "",
            IcdName = meta.IcdName ?? es.Icddescription ?? ""
        };

        // Kê dịch vụ
        var usageList = await db.UsageHistories.AsNoTracking()
            .Include(u => u.Service)
            .Where(u => u.ExamSheetId == es.ExamSheetId)
            .ToListAsync(ct);

        var serviceIndication = new ServiceIndicationDto
        {
            TreatmentType = meta.TreatmentType ?? "Theo liệu trình",
            TotalSessions = meta.TotalSessions > 0 ? meta.TotalSessions : 1,
            IndicationNote = meta.IndicationNote ?? "",
            AdditionalContent = meta.AdditionalContent ?? "",
            Services = usageList.Select(u => new IndicatedServiceItemDto
            {
                ServiceId = u.ServiceId ?? 0,
                ServiceCode = u.Service?.ServiceCode ?? "",
                ServiceName = u.Service?.ServiceName ?? "",
                TreatmentType = meta.TreatmentType ?? "Theo liệu trình",
                Note = u.ResultNote ?? "",
                Price = u.Service?.Price ?? 0,
                Unit = u.Service?.Unit ?? "Lần"
            }).ToList()
        };

        // Kê đơn thuốc
        var formula = await db.RemedyFormulas.AsNoTracking()
            .Include(f => f.FormulaDetails).ThenInclude(fd => fd.Herb)
            .FirstOrDefaultAsync(f => f.ExamSheetId == es.ExamSheetId, ct);

        string? templateFormulaName = null;
        if (meta.TemplateFormulaId.HasValue)
        {
            templateFormulaName = await db.RemedyFormulas.AsNoTracking()
                .Where(f => f.FormulaId == meta.TemplateFormulaId.Value)
                .Select(f => f.FormulaName)
                .FirstOrDefaultAsync(ct);
        }

        var prescription = new PrescriptionDetailDto
        {
            FormulaId = formula?.FormulaId,
            PrescriptionCode = formula != null ? $"DT{formula.FormulaId:D5}" : (meta.PrescriptionCode ?? ""),
            PrescriptionMethod = meta.PrescriptionMethod ?? "Tự kê đơn",
            TemplateFormulaId = meta.TemplateFormulaId,
            TemplateFormulaName = templateFormulaName,
            TotalDoses = formula?.TotalDoses ?? (meta.TotalDoses ?? 5),
            PrescriptionNote = formula?.UsageInstructions ?? meta.PrescriptionNote ?? "",
            Status = meta.PrescriptionStatus ?? "Nháp",
            IsLocked = meta.PrescriptionStatus == "Đã gửi Herbalist",
            Ingredients = formula?.FormulaDetails.Select(fd => new PrescriptionIngredientDto
            {
                HerbId = fd.HerbId,
                HerbCode = fd.Herb?.HerbCode ?? "",
                HerbName = fd.Herb?.CommonName ?? "",
                GramsPerDose = fd.DosagePerDose,
                TotalDoses = formula.TotalDoses,
                TotalGrams = fd.DosagePerDose * formula.TotalDoses,
                Note = fd.Note
            }).ToList() ?? new()
        };

        // Lịch sử khám bệnh trước đây
        var previousExamSheets = await db.ExamSheets.AsNoTracking()
            .Include(e => e.Doctor)
            .Include(e => e.Record)
            .Where(e => e.Record.PatientId == patient.PatientId && e.ExamSheetId != es.ExamSheetId)
            .OrderByDescending(e => e.ExamDate)
            .Take(5)
            .ToListAsync(ct);

        var previousVisits = previousExamSheets.Select(p => new PreviousVisitBriefDto
        {
            ExamSheetId = p.ExamSheetId,
            MedicalRecordCode = $"MR{p.RecordId:D5}",
            VisitCode = $"KCB{p.ExamSheetId:D5}",
            ExamDate = p.ExamDate,
            DoctorName = p.Doctor?.FullName ?? "",
            SummarySymptom = p.Symptoms ?? ""
        }).ToList();

        // Liệu trình đang chạy
        var activeProtocol = await db.TreatmentProtocols.AsNoTracking()
            .Where(p => p.Record.PatientId == patient.PatientId && p.PlanStatus != "COMPLETED")
            .OrderByDescending(p => p.ProtocolId)
            .FirstOrDefaultAsync(ct);

        ActiveProtocolBriefDto? activeProtocolDto = null;
        if (activeProtocol != null)
        {
            var completedCount = await db.UsageHistories.AsNoTracking()
                .CountAsync(u => u.ExamSheet != null && u.ExamSheet.Record != null && u.ExamSheet.Record.PatientId == patient.PatientId && u.ResultNote != "Chờ điều trị", ct);

            activeProtocolDto = new ActiveProtocolBriefDto
            {
                ProtocolId = activeProtocol.ProtocolId,
                ProtocolCode = $"LT{activeProtocol.ProtocolId:D5}",
                ProtocolName = activeProtocol.ProtocolName,
                CompletedSessions = completedCount,
                TotalSessions = activeProtocol.PlannedSessionCount ?? 5,
                PaymentStatus = "Đã thanh toán toàn bộ"
            };
        }

        var isReadOnly = meta.Status == "Hoàn tất" || meta.Status == "Chờ khám";

        return new MedicalRecordWorkspaceDto
        {
            ExamSheetId = es.ExamSheetId,
            VisitCode = $"KCB{es.ExamSheetId:D5}",
            MedicalRecordId = es.RecordId,
            MedicalRecordCode = $"MR{es.RecordId:D5}",
            Status = meta.Status,
            IsReadOnly = isReadOnly,
            PatientBanner = banner,
            ClinicalExam = clinicalExam,
            ServiceIndication = serviceIndication,
            Prescription = prescription,
            PatientHistorySidebar = new PatientHistorySidebarDto
            {
                PreviousVisits = previousVisits,
                ActiveProtocol = activeProtocolDto
            }
        };
    }
}
