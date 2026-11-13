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
        var vnToday = DateTime.UtcNow.AddHours(7).Date;
        var fromDate = filter.FromDate?.Date ?? vnToday;
        var toDate = (filter.ToDate?.Date ?? vnToday).AddDays(1).AddTicks(-1);

        var dbQuery = db.MedicalRecords.AsNoTracking()
            .Include(e => e.Doctor)
            .Include(e => e.Patient)
            .Where(e => e.ExamDate >= fromDate && e.ExamDate <= toDate);

        if (filter.DoctorId.HasValue && filter.DoctorId.Value > 0)
        {
            dbQuery = dbQuery.Where(e => e.DoctorId == filter.DoctorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.PatientName))
        {
            var kw = filter.PatientName.Trim();
            dbQuery = dbQuery.Where(e => e.Patient.FullName.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(filter.PatientCode))
        {
            var kw = filter.PatientCode.Trim();
            dbQuery = dbQuery.Where(e => e.Patient.PatientCode != null && e.Patient.PatientCode.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(filter.Phone))
        {
            var kw = filter.Phone.Trim();
            dbQuery = dbQuery.Where(e => e.Patient.Phone != null && e.Patient.Phone.Contains(kw));
        }

        var searchKw = filter.Keyword ?? filter.Search;
        if (!string.IsNullOrWhiteSpace(searchKw))
        {
            var sk = searchKw.Trim();
            dbQuery = dbQuery.Where(e => e.Patient.FullName.Contains(sk)
                || (e.Patient.Phone != null && e.Patient.Phone.Contains(sk))
                || (e.Patient.PatientCode != null && e.Patient.PatientCode.Contains(sk)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Gender))
        {
            var g = filter.Gender.Trim();
            dbQuery = dbQuery.Where(e => e.Patient.Gender == g);
        }

        if (!string.IsNullOrWhiteSpace(filter.VisitCode))
        {
            var vc = filter.VisitCode.Trim();
            if (vc.StartsWith("KCB", StringComparison.OrdinalIgnoreCase) && int.TryParse(vc[3..], out var parsedId))
            {
                dbQuery = dbQuery.Where(e => e.RecordId == parsedId);
            }
        }

        var records = await dbQuery.ToListAsync(ct);

        // Parse metadata và map
        var itemsWithMeta = records.Select((es, idx) =>
        {
            var meta = ClinicalMetadataJson.FromMedicalRecord(es);
            var age = es.Patient.DateOfBirth.HasValue
                ? DateTime.Today.Year - es.Patient.DateOfBirth.Value.Year
                : 0;

            var action = meta.Status switch
            {
                "Chờ khám" => "Gọi vào khám",
                "Đang khám" => "Tiếp tục khám",
                _ => "Xem hồ sơ"
            };

            return new
            {
                Record = es,
                Meta = meta,
                Item = new DoctorExamQueueItemDto
                {
                    ExamSheetId = es.RecordId,
                    QueueNumber = meta.QueueNumber ?? $"{idx + 1:D3}",
                    IsPriority = meta.IsPriority,
                    PatientId = es.PatientId,
                    PatientCode = es.Patient.PatientCode ?? $"BN{es.PatientId:D5}",
                    PatientName = es.Patient.FullName,
                    RegisteredAt = es.ExamDate,
                    VisitCode = $"KCB{es.RecordId:D5}",
                    Age = age,
                    Gender = es.Patient.Gender ?? "Khác",
                    Phone = es.Patient.Phone ?? "",
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
            var st = filter.Status.Trim().ToUpperInvariant();
            if (st is not ("ALL" or "TẤT CẢ" or "TAT CA"))
            {
                string targetStatus = st switch
                {
                    "WAITING" or "CHỜ KHÁM" or "CHO KHAM" or "CHO_KHAM" => "Chờ khám",
                    "IN_PROGRESS" or "INPROGRESS" or "ĐANG KHÁM" or "DANG KHAM" or "DANG_KHAM" => "Đang khám",
                    "COMPLETED" or "HOÀN THÀNH" or "HOAN THANH" or "HOÀN TẤT" or "HOAN TAT" or "ĐÃ HOÀN TẤT" => "Hoàn tất",
                    _ => filter.Status.Trim()
                };
                filteredItems = filteredItems.Where(x => x.Meta.Status.Equals(targetStatus, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Sắp xếp: Ngày mới nhất lên đầu, sau đó đến Đang khám -> Chờ khám -> Hoàn tất, ưu tiên và thời gian
        var sortedItems = filteredItems
            .OrderByDescending(x => x.Record.ExamDate.Date)
            .ThenBy(x => (x.Meta.Status == "Đang khám") ? 1 : (x.Meta.Status == "Chờ khám") ? 2 : 3)
            .ThenByDescending(x => x.Meta.IsPriority)
            .ThenBy(x => x.Record.ExamDate)
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
        var es = await db.MedicalRecords
            .Include(e => e.Doctor)
            .Include(e => e.Patient).ThenInclude(p => p.PatientContacts)
            .FirstOrDefaultAsync(e => e.RecordId == examSheetId, ct)
            ?? throw new NotFoundException($"Medical record with id {examSheetId} was not found.");

        var meta = ClinicalMetadataJson.FromMedicalRecord(es);

        if (meta.Status == "Hoàn tất")
        {
            throw new BadRequestException("Lượt khám này đã hoàn tất, không thể gọi vào khám lại.");
        }

        if (meta.Status != "Đang khám")
        {
            meta.Status = "Đang khám";
            meta.ExamStartTime ??= DateTime.UtcNow;
            es.Status = "IN_PROGRESS";
            es.DoctorAdvice = meta.ToJson();
            await db.SaveChangesAsync(ct);
        }

        return await BuildWorkspaceDtoAsync(es, meta, ct);
    }

    public async Task<MedicalRecordWorkspaceDto> GetMedicalRecordWorkspaceAsync(int examSheetId, CancellationToken ct = default)
    {
        var es = await db.MedicalRecords
            .Include(e => e.Doctor)
            .Include(e => e.Patient).ThenInclude(p => p.PatientContacts)
            .FirstOrDefaultAsync(e => e.RecordId == examSheetId, ct)
            ?? throw new NotFoundException($"Medical record with id {examSheetId} was not found.");

        var meta = ClinicalMetadataJson.FromMedicalRecord(es);

        return await BuildWorkspaceDtoAsync(es, meta, ct);
    }

    public async Task<MedicalRecordWorkspaceDto> SaveClinicalExamAsync(int examSheetId, SaveClinicalExamRequestDto request, CancellationToken ct = default)
    {
        var es = await db.MedicalRecords
            .Include(e => e.Doctor)
            .Include(e => e.Patient).ThenInclude(p => p.PatientContacts)
            .FirstOrDefaultAsync(e => e.RecordId == examSheetId, ct)
            ?? throw new NotFoundException($"Medical record with id {examSheetId} was not found.");

        var meta = ClinicalMetadataJson.FromMedicalRecord(es);

        if (meta.Status == "Hoàn tất")
        {
            throw new BadRequestException("Lượt khám đã hoàn tất và bị khóa, không thể chỉnh sửa thông tin lâm sàng.");
        }

        // Cập nhật sinh hiệu vào metadata và bảng
        if (request.VitalSigns != null)
        {
            meta.PulseRate = request.VitalSigns.PulseRate;
            meta.Temperature = request.VitalSigns.Temperature;
            meta.RespiratoryRate = request.VitalSigns.RespiratoryRate;
            meta.Height = request.VitalSigns.Height;
            meta.Weight = request.VitalSigns.Weight;
            meta.SpO2 = request.VitalSigns.SpO2;

            es.PulseRate = (short?)request.VitalSigns.PulseRate;
            es.Temperature = request.VitalSigns.Temperature;
            es.RespiratoryRate = (short?)request.VitalSigns.RespiratoryRate;
            es.HeightCm = request.VitalSigns.Height;
            es.WeightKg = request.VitalSigns.Weight;
            es.SpO2 = request.VitalSigns.SpO2;
        }

        // Cập nhật triệu chứng & chẩn đoán
        if (request.Symptoms != null)
        {
            meta.Symptoms = request.Symptoms;
            es.Symptoms = request.Symptoms;
        }

        if (request.PreliminaryDiagnosis != null)
        {
            meta.PreliminaryDiagnosis = request.PreliminaryDiagnosis;
            es.PreliminaryDiagnosis = request.PreliminaryDiagnosis;
        }

        // Cập nhật Tứ chẩn YHCT
        if (request.TraditionalMedicine != null)
        {
            meta.VongChan = request.TraditionalMedicine.VongChan;
            meta.VanChan_NgheNgui = request.TraditionalMedicine.VanChan_NgheNgui;
            meta.VanChan_Hoi = request.TraditionalMedicine.VanChan_Hoi;
            meta.ThietChan = request.TraditionalMedicine.ThietChan;
            meta.FourExamsSummary = request.TraditionalMedicine.FourExamsSummary;
            meta.BienChung = request.TraditionalMedicine.BienChung;
            meta.TraditionalDiagnosis = request.TraditionalMedicine.TraditionalDiagnosis;

            es.Inspection = request.TraditionalMedicine.VongChan;
            es.ListeningSmelling = request.TraditionalMedicine.VanChan_NgheNgui;
            es.Inquiry = request.TraditionalMedicine.VanChan_Hoi;
            es.Palpation = request.TraditionalMedicine.ThietChan;
            es.FourExaminationSummary = request.TraditionalMedicine.FourExamsSummary;
            es.SyndromeDifferentiation = request.TraditionalMedicine.BienChung;
            es.TraditionalDiagnosis = request.TraditionalMedicine.TraditionalDiagnosis;
        }

        // Cập nhật mã ICD
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

        if (request.ExamStartTime.HasValue)
        {
            meta.ExamStartTime = request.ExamStartTime.Value;
        }

        if (meta.Status == "Chờ khám")
        {
            meta.Status = "Đang khám";
            es.Status = "IN_PROGRESS";
        }

        es.DoctorAdvice = meta.ToJson();
        es.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await BuildWorkspaceDtoAsync(es, meta, ct);
    }

    public async Task<MedicalRecordWorkspaceDto> CompleteExaminationAsync(int examSheetId, SaveClinicalExamRequestDto? request, CancellationToken ct = default)
    {
        var es = await db.MedicalRecords
            .Include(e => e.Doctor)
            .Include(e => e.Patient).ThenInclude(p => p.PatientContacts)
            .FirstOrDefaultAsync(e => e.RecordId == examSheetId, ct)
            ?? throw new NotFoundException($"Medical record with id {examSheetId} was not found.");

        var meta = ClinicalMetadataJson.FromMedicalRecord(es);

        if (meta.Status == "Hoàn tất")
        {
            return await BuildWorkspaceDtoAsync(es, meta, ct);
        }

        // Lưu thông tin lâm sàng cuối cùng nếu có gửi kèm
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
        es.Status = "COMPLETED";
        es.CompletedAt = DateTime.UtcNow;
        es.UpdatedAt = DateTime.UtcNow;
        es.DoctorAdvice = meta.ToJson();

        await db.SaveChangesAsync(ct);

        return await BuildWorkspaceDtoAsync(es, meta, ct);
    }

    public async Task<ServiceIndicationDto> SaveServiceIndicationAsync(int examSheetId, SaveServiceIndicationRequestDto request, CancellationToken ct = default)
    {
        var es = await db.MedicalRecords
            .FirstOrDefaultAsync(e => e.RecordId == examSheetId, ct)
            ?? throw new NotFoundException($"Medical record with id {examSheetId} was not found.");

        var meta = ClinicalMetadataJson.FromMedicalRecord(es);

        if (meta.Status == "Hoàn tất")
        {
            throw new BadRequestException("Lượt khám đã hoàn tất, không thể chỉnh sửa chỉ định dịch vụ.");
        }

        meta.TreatmentType = request.TreatmentType;
        meta.TotalSessions = request.TotalSessions > 0 ? request.TotalSessions : 1;
        meta.IndicationNote = request.IndicationNote;
        meta.AdditionalContent = request.AdditionalContent;

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
                TreatmentType = request.TreatmentType ?? "Theo liệu trình",
                Note = s.Note ?? request.IndicationNote ?? "",
                Price = svc?.Price ?? 0,
                Unit = svc?.Unit ?? "Lần"
            };
        }).ToList();

        return new ServiceIndicationDto
        {
            TreatmentType = request.TreatmentType ?? "Theo liệu trình",
            TotalSessions = request.TotalSessions,
            IndicationNote = request.IndicationNote ?? "",
            AdditionalContent = request.AdditionalContent ?? "",
            Services = resultItems
        };
    }

    public async Task<PrescriptionCatalogDto> GetPrescriptionCatalogAsync(CancellationToken ct = default)
    {
        // 1. Lấy danh sách bài thuốc mẫu (HerbalFormula)
        var templateFormulas = await db.HerbalFormulas.AsNoTracking()
            .Include(f => f.HerbalFormulaDetails).ThenInclude(fd => fd.Herb)
            .Where(f => f.IsActive)
            .OrderBy(f => f.HerbalFormulaId)
            .Take(50)
            .ToListAsync(ct);

        var formulaTemplates = templateFormulas.Select(f => new FormulaTemplateDto
        {
            FormulaId = f.HerbalFormulaId,
            FormulaName = f.FormulaName,
            UsageInstructions = f.UsageInstructions,
            DosageDays = 5,
            TotalDoses = 5,
            Ingredients = f.HerbalFormulaDetails.Select(fd => new FormulaTemplateIngredientDto
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
        var es = await db.MedicalRecords
            .FirstOrDefaultAsync(e => e.RecordId == examSheetId, ct)
            ?? throw new NotFoundException($"Medical record with id {examSheetId} was not found.");

        var meta = ClinicalMetadataJson.FromMedicalRecord(es);

        if (meta.Status == "Hoàn tất")
        {
            throw new BadRequestException("Lượt khám đã hoàn tất, không thể chỉnh sửa đơn thuốc.");
        }

        if (meta.PrescriptionStatus == "Đã gửi Herbalist")
        {
            throw new BadRequestException("Sau khi gửi Herbalist, nội dung đơn không còn được chỉnh sửa ở màn kê đơn.");
        }

        var isSend = request.ActionType.Equals("SEND_HERBALIST", StringComparison.OrdinalIgnoreCase);

        // Cập nhật metadata đơn thuốc
        meta.PrescriptionStatus = isSend ? "Đã gửi Herbalist" : "Nháp";
        meta.PrescriptionMethod = request.PrescriptionMethod ?? "Tự kê đơn";
        meta.TemplateFormulaId = request.TemplateFormulaId;
        meta.TotalDoses = request.TotalDoses > 0 ? request.TotalDoses : 1;
        meta.PrescriptionNote = request.PrescriptionNote;
        meta.PrescriptionCode = $"DT{examSheetId:D5}";

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
            templateName = await db.HerbalFormulas.AsNoTracking()
                .Where(f => f.HerbalFormulaId == request.TemplateFormulaId.Value)
                .Select(f => f.FormulaName)
                .FirstOrDefaultAsync(ct);
        }

        var ingredientsResult = request.Ingredients.Select(ing =>
        {
            herbsDict.TryGetValue(ing.HerbId, out var h);
            var grams = ing.GramsPerDose;
            var totalDoses = meta.TotalDoses ?? 1;
            var totalGrams = grams * totalDoses;
            return new PrescriptionIngredientDto
            {
                HerbId = ing.HerbId,
                HerbCode = h?.HerbCode ?? "",
                HerbName = h?.CommonName ?? "",
                GramsPerDose = grams,
                TotalDoses = totalDoses,
                TotalGrams = totalGrams,
                Note = ing.Note
            };
        }).ToList();

        return new PrescriptionDetailDto
        {
            FormulaId = request.TemplateFormulaId,
            PrescriptionCode = meta.PrescriptionCode,
            PrescriptionMethod = meta.PrescriptionMethod,
            TemplateFormulaId = meta.TemplateFormulaId,
            TemplateFormulaName = templateName,
            TotalDoses = meta.TotalDoses ?? 1,
            PrescriptionNote = meta.PrescriptionNote ?? "",
            Status = meta.PrescriptionStatus,
            IsLocked = isSend,
            Ingredients = ingredientsResult
        };
    }

    private async Task<MedicalRecordWorkspaceDto> BuildWorkspaceDtoAsync(MedicalRecord es, ClinicalMetadataJson meta, CancellationToken ct)
    {
        var patient = es.Patient;
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
            ExamStartTime = meta.ExamStartTime ?? es.ExamDate,
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
                VongChan = meta.VongChan ?? es.Inspection ?? "",
                VanChan_NgheNgui = meta.VanChan_NgheNgui ?? es.ListeningSmelling ?? "",
                VanChan_Hoi = meta.VanChan_Hoi ?? es.Inquiry ?? "",
                ThietChan = meta.ThietChan ?? es.Palpation ?? "",
                FourExamsSummary = meta.FourExamsSummary ?? es.FourExaminationSummary ?? "",
                BienChung = meta.BienChung ?? es.SyndromeDifferentiation ?? "",
                TraditionalDiagnosis = meta.TraditionalDiagnosis ?? es.TraditionalDiagnosis ?? ""
            },
            IcdCode = meta.IcdCode ?? es.Icdcode ?? "",
            IcdName = meta.IcdName ?? es.Icddescription ?? ""
        };

        var serviceIndication = new ServiceIndicationDto
        {
            TreatmentType = meta.TreatmentType ?? "Theo liệu trình",
            TotalSessions = meta.TotalSessions > 0 ? meta.TotalSessions : 1,
            IndicationNote = meta.IndicationNote ?? "",
            AdditionalContent = meta.AdditionalContent ?? "",
            Services = new List<IndicatedServiceItemDto>()
        };

        string? templateFormulaName = null;
        if (meta.TemplateFormulaId.HasValue)
        {
            templateFormulaName = await db.HerbalFormulas.AsNoTracking()
                .Where(f => f.HerbalFormulaId == meta.TemplateFormulaId.Value)
                .Select(f => f.FormulaName)
                .FirstOrDefaultAsync(ct);
        }

        var prescription = new PrescriptionDetailDto
        {
            FormulaId = meta.TemplateFormulaId,
            PrescriptionCode = meta.PrescriptionCode ?? $"DT{es.RecordId:D5}",
            PrescriptionMethod = meta.PrescriptionMethod ?? "Tự kê đơn",
            TemplateFormulaId = meta.TemplateFormulaId,
            TemplateFormulaName = templateFormulaName,
            TotalDoses = meta.TotalDoses ?? 5,
            PrescriptionNote = meta.PrescriptionNote ?? "",
            Status = meta.PrescriptionStatus ?? "Nháp",
            IsLocked = meta.PrescriptionStatus == "Đã gửi Herbalist",
            Ingredients = new List<PrescriptionIngredientDto>()
        };

        // Lịch sử khám bệnh trước đây
        var previousRecords = await db.MedicalRecords.AsNoTracking()
            .Include(e => e.Doctor)
            .Where(e => e.PatientId == patient.PatientId && e.RecordId != es.RecordId)
            .OrderByDescending(e => e.ExamDate)
            .Take(5)
            .ToListAsync(ct);

        var previousVisits = previousRecords.Select(p => new PreviousVisitBriefDto
        {
            ExamSheetId = p.RecordId,
            MedicalRecordCode = $"MR{p.RecordId:D5}",
            VisitCode = $"KCB{p.RecordId:D5}",
            ExamDate = p.ExamDate,
            DoctorName = p.Doctor?.FullName ?? "",
            SummarySymptom = p.Symptoms ?? ""
        }).ToList();

        // Liệu trình đang chạy
        var activePlan = await db.TreatmentPlans.AsNoTracking()
            .Where(p => p.Record.PatientId == patient.PatientId && p.Status != "COMPLETED")
            .OrderByDescending(p => p.PlanId)
            .FirstOrDefaultAsync(ct);

        ActiveProtocolBriefDto? activeProtocolDto = null;
        if (activePlan != null)
        {
            activeProtocolDto = new ActiveProtocolBriefDto
            {
                ProtocolId = activePlan.PlanId,
                ProtocolCode = activePlan.PlanCode ?? $"LT{activePlan.PlanId:D5}",
                ProtocolName = activePlan.PlanName ?? "Kế hoạch điều trị",
                CompletedSessions = 0,
                TotalSessions = 5,
                PaymentStatus = "Đã thanh toán"
            };
        }

        var isReadOnly = meta.Status == "Hoàn tất" || meta.Status == "Chờ khám";

        return new MedicalRecordWorkspaceDto
        {
            ExamSheetId = es.RecordId,
            VisitCode = $"KCB{es.RecordId:D5}",
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
