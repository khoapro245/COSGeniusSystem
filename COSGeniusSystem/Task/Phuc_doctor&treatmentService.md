# BÁO CÁO CÔNG VIỆC THỰC HIỆN - PHÂN HỆ BÁC SĨ & DỊCH VỤ TRỊ LIỆU

> **Dự án:** COSGeniusSystem (Hệ thống quản lý phòng khám đông y & dược liệu)  
> **Người thực hiện:** Phúc  
> **Nền tảng:** .NET 10 (ASP.NET Core Web API), Entity Framework Core (SQL Server & InMemory)  
> **Tổng số API do Phúc phụ trách:** **13 APIs** (2 Doctor Master Data, 3 Treatment Services, 8 Doctor Clinical)  
> **Cập nhật lần cuối:** 04/11/2026  

---

## I. TỔNG QUAN CÔNG VIỆC ĐÃ HOÀN THÀNH

1. **Kết nối Cơ sở dữ liệu & Cấu hình môi trường:**
   - Cấu hình chuỗi kết nối trong `appsettings.json` kết nối chuẩn xác vào cơ sở dữ liệu `ClinicHerbManagementDB`.
2. **Cụm 1 - Danh mục Bác sĩ (Doctor Management - 2 APIs):**
   - `API-PHUC-01`: Lấy danh sách bác sĩ/kỹ thuật viên có bộ lọc đa điều kiện (từ khóa, chuyên khoa, trạng thái).
   - `API-PHUC-02`: Xem chi tiết bác sĩ kết hợp thông tin tài khoản đăng nhập (`User`).
3. **Cụm 2 - Bảng giá Dịch vụ Trị liệu (Treatment Services - 3 APIs):**
   - `API-PHUC-03`: Lấy danh mục bảng giá dịch vụ khả dụng (`IsActive = true`).
   - `API-PHUC-04`: Thêm mới dịch vụ vào bảng giá (validate giá > 0, chống trùng mã và trùng tên).
   - `API-PHUC-05`: Cập nhật dịch vụ trị liệu (bảo đảm tính toàn vẹn và bất biến của hóa đơn cũ).
4. **Cụm 3 - Phân hệ Bác sĩ Khám bệnh & Kê đơn lâm sàng (Doctor Clinical Workflow - 8 APIs MỚI):**
   - Thiết kế kiến trúc **Zero-Conflict**: Tạo nhóm file độc lập (`DoctorClinicalController`, `IDoctorClinicalService`, `DoctorClinicalService`, `DTOs/DoctorClinical/`).
   - Cơ chế **Extensible Metadata (`ClinicalMetadataJson`)**: Lưu trữ toàn bộ dữ liệu khám mới (Sinh hiệu 6 chỉ số, Tứ chẩn YHCT đầy đủ, Biện chứng, Mã ICD, Trạng thái khám) dưới dạng JSON vào cột `DoctorAdvice` mà không cần thay đổi schema Database.
   - `API-PHUC-06`: Hàng đợi khám bệnh kèm bộ đếm trạng thái (`Chờ khám`, `Đang khám`, `Hoàn tất`), tự động ưu tiên ca cấp cứu.
   - `API-PHUC-07`: Nghiệp vụ "Gọi vào khám" (Chuyển trạng thái `Chờ khám` $\rightarrow$ `Đang khám`, ghi nhận giờ bắt đầu).
   - `API-PHUC-08`: Xem chi tiết hồ sơ bệnh án Workspace (kèm lịch sử các lần khám trước và phác đồ đang chạy).
   - `API-PHUC-09`: Lưu khám lâm sàng (Sinh hiệu, Tứ chẩn YHCT, Chẩn đoán sơ bộ, Mã ICD).
   - `API-PHUC-10`: Hoàn tất khám bệnh (Chuyển trạng thái `Hoàn tất`, khóa hồ sơ `IsReadOnly = true`).
   - `API-PHUC-11`: Kê dịch vụ điều trị (Chỉ định dịch vụ theo liệu trình / đơn lẻ, tự động tạo/cập nhật `TreatmentProtocol` và `UsageHistory`).
   - `API-PHUC-12`: Danh mục kê đơn (Bài thuốc mẫu `RemedyFormula` để nạp tự động + Danh mục dược liệu `Herb` để chọn lẻ).
   - `API-PHUC-13`: Kê đơn thuốc (Lưu nháp / Gửi Herbalist; khi gửi Herbalist sẽ khóa đơn không cho sửa đổi).
5. **Đăng ký Dependency Injection & Middleware:**
   - Đăng ký `IDoctorService`, `ITreatmentService`, `IDoctorClinicalService` vào DI Container (`Program.cs`).
6. **Kiểm thử tự động & Kịch bản Test:**
   - Xây dựng 20 bài Unit Test (11 test Master Data + 9 test Clinical) bằng xUnit & EF Core InMemory. Toàn bộ solution đạt **80/80 tests PASS (100%)**.
   - Bổ sung kịch bản kiểm thử 8 bước tuần tự vào file [`COSGeniusSystem.http`](file:///e:/DoAnTotNghiep/COSGeniusSystem/COSGeniusSystem/COSGeniusSystem.http).

---

## II. CẤU TRÚC FILE MÃ NGUỒN ĐÃ TẠO

```text
COSGeniusSystem/
├── Controllers/
│   ├── DoctorsController.cs                       # Endpoints API-PHUC-01, API-PHUC-02 (Danh mục Bác sĩ)
│   ├── TreatmentServicesController.cs             # Endpoints API-PHUC-03, 04, 05 (Bảng giá Dịch vụ)
│   └── DoctorClinicalController.cs                # Endpoints API-PHUC-06 -> API-PHUC-13 (Khám lâm sàng Bác sĩ)
├── DTOs/
│   ├── Doctors/
│   │   ├── DoctorFilterQuery.cs                   # Tham số lọc danh sách bác sĩ
│   │   ├── DoctorResponseDto.cs                   # Output danh sách bác sĩ
│   │   └── DoctorDetailDto.cs                     # Output chi tiết bác sĩ kèm user
│   ├── TreatmentServices/
│   │   ├── TreatmentServiceResponseDto.cs         # Output thông tin dịch vụ trị liệu
│   │   ├── CreateTreatmentServiceRequest.cs       # Input thêm mới dịch vụ
│   │   └── UpdateTreatmentServiceRequest.cs       # Input cập nhật dịch vụ
│   └── DoctorClinical/                            # Nhóm DTOs Khám bệnh Lâm sàng Bác sĩ (Mới)
│       ├── DoctorExamFilterDto.cs                 # Tham số lọc hàng đợi khám
│       ├── DoctorExamQueueDtos.cs                 # Output hàng đợi & bộ đếm trạng thái
│       ├── ClinicalMetadataJson.cs                # Lớp quản lý JSON Metadata lưu trữ mở rộng
│       ├── ClinicalExamDtos.cs                    # DTOs Sinh hiệu, Tứ chẩn YHCT, Khám lâm sàng
│       ├── ServiceIndicationDtos.cs               # DTOs Chỉ định dịch vụ điều trị / liệu trình
│       ├── PrescriptionDtos.cs                    # DTOs Kê đơn thuốc, Danh mục mẫu, Vị thuốc
│       ├── PatientHistorySidebarDto.cs            # DTOs Lịch sử khám cũ & Phác đồ đang chạy
│       └── MedicalRecordWorkspaceDto.cs           # DTO tổng hợp Workspace khám bệnh toàn diện
├── Services/
│   ├── Doctors/
│   │   ├── IDoctorService.cs                      # Interface nghiệp vụ Bác sĩ
│   │   └── DoctorService.cs                       # Xử lý truy vấn Bác sĩ qua EF Core
│   ├── TreatmentServices/
│   │   ├── ITreatmentService.cs                   # Interface nghiệp vụ Dịch vụ trị liệu
│   │   └── TreatmentService.cs                    # Xử lý nghiệp vụ & validation dịch vụ
│   ├── Interfaces/
│   │   └── IDoctorClinicalService.cs              # Interface Khám bệnh lâm sàng Bác sĩ (Mới)
│   └── Impl/
│       └── DoctorClinicalService.cs               # Hiện thực 8 nghiệp vụ lâm sàng Bác sĩ (Mới)
├── UnitTests/
│   ├── Doctors/DoctorServiceTests.cs              # 2 Unit Tests Bác sĩ
│   ├── TreatmentServices/TreatmentServiceTests.cs # 9 Unit Tests Dịch vụ trị liệu
│   └── DoctorClinical/                            # Unit Tests Lâm sàng Bác sĩ (Mới)
│       └── DoctorClinicalServiceTests.cs          # 9 Unit Tests kiểm thử toàn diện 8 endpoints
├── COSGeniusSystem.http                           # Chuỗi 8 Request test lâm sàng tuần tự (Mục 10 - 18)
└── Task/
    └── Phuc_doctor&treatmentService.md            # Báo cáo chi tiết công việc
```

---

## III. CHI TIẾT DANH SÁCH 13 API ENDPOINTS

### CỤM 1: DANH MỤC BÁC SĨ (DOCTOR MASTER DATA)

#### API-PHUC-01: Lấy danh sách bác sĩ / kỹ thuật viên
* **Method & URL:** `GET /api/v1/doctors`
* **Params:** `keyword` (string), `specialization` (string), `isActive` (bool = true).
* **Mục đích:** Lấy danh sách bác sĩ khả dụng phân công phòng khám, hỗ trợ tìm kiếm và lọc chuyên khoa.
* **Response:** `200 OK` kèm `List<DoctorResponseDto>`.

#### API-PHUC-02: Xem chi tiết bác sĩ
* **Method & URL:** `GET /api/v1/doctors/{id}`
* **Params:** `id` (int, DoctorId).
* **Mục đích:** Hiển thị thông tin chuyên môn, số chứng chỉ hành nghề (CCHN), SĐT và tài khoản đăng nhập liên kết.
* **Response:** `200 OK` kèm `DoctorDetailDto`. Không tìm thấy trả về `404 Not Found`.

---

### CỤM 2: BẢNG GIÁ DỊCH VỤ TRỊ LIỆU (TREATMENT SERVICES)

#### API-PHUC-03: Danh mục bảng giá dịch vụ trị liệu
* **Method & URL:** `GET /api/v1/services`
* **Mục đích:** Tra cứu danh mục dịch vụ/thủ thuật khả dụng (châm cứu, xoa bóp bấm huyệt...) phục vụ tiếp tân tra cứu và bác sĩ chỉ định.
* **Response:** `200 OK` kèm `List<TreatmentServiceResponseDto>`.

#### API-PHUC-04: Thêm mới dịch vụ vào bảng giá
* **Method & URL:** `POST /api/v1/services`
* **Request Body:** `serviceCode`, `serviceName`, `price`, `unit`, `description`.
* **Validation:** Bắt buộc nhập đầy đủ; `price > 0`; chống trùng mã và trùng tên dịch vụ (`400 Bad Request`).
* **Response:** `201 Created` kèm `TreatmentServiceResponseDto`.

#### API-PHUC-05: Cập nhật dịch vụ trị liệu
* **Method & URL:** `PUT /api/v1/services/{id}`
* **Request Body:** `serviceName`, `price`, `unit`, `description`, `isActive`.
* **Validation:** Kiểm tra tồn tại (`404`); kiểm tra `price > 0`; chống trùng tên dịch vụ khác (`400`).
* **Response:** `200 OK` kèm thông tin dịch vụ sau cập nhật.

---

### CỤM 3: PHÂN HỆ BÁC SĨ KHÁM BỆNH & ĐIỀU TRỊ LÂM SÀNG (DOCTOR CLINICAL WORKFLOW)

#### API-PHUC-06: Tra cứu danh sách đăng ký khám & hàng đợi của Bác sĩ
* **Method & URL:** `GET /api/v1/doctor/examinations`
* **Query Params (`DoctorExamFilterDto`):** `DoctorId`, `Status` (Tất cả / Chờ khám / Đang khám / Hoàn tất), `FromDate`, `ToDate`, `PatientName`, `PatientCode`, `VisitCode`, `Phone`, `Gender`, `PageNumber`, `PageSize`.
* **Logic:**
  * Tính toán bộ đếm 3 trạng thái trong ngày: `WaitingCount`, `ExaminingCount`, `CompletedCount`, `TotalCount`.
  * Sắp xếp tự động: Ca ưu tiên (`IsPriority`) lên đầu, sau đó theo giờ đăng ký khám.
  * Tự động xác định hành động cho phép: `"Gọi vào khám"`, `"Tiếp tục khám"` hoặc `"Xem hồ sơ"`.
* **Response:** `200 OK` kèm `DoctorExamQueueResponseDto`.

#### API-PHUC-07: Nghiệp vụ "Gọi vào khám"
* **Method & URL:** `PATCH /api/v1/doctor/examinations/{examSheetId}/call`
* **Route Param:** `examSheetId` (int).
* **Logic:**
  * Kiểm tra nếu lượt khám đã `Hoàn tất` $\rightarrow$ Trả về `400 Bad Request`.
  * Nếu đang `Chờ khám`: chuyển trạng thái sang `Đang khám`, ghi nhận thời gian bắt đầu khám `ExamStartTime = DateTime.Now`.
  * Trả về ngay toàn bộ dữ liệu Workspace để Frontend mở màn hình khám bệnh.
* **Response:** `200 OK` kèm `MedicalRecordWorkspaceDto`.

#### API-PHUC-08: Xem chi tiết hồ sơ khám bệnh Workspace (DOC-02, DOC-05, DOC-06)
* **Method & URL:** `GET /api/v1/doctor/examinations/{examSheetId}`
* **Route Param:** `examSheetId` (int).
* **Logic:**
  * Nạp thông tin hành chính bệnh nhân & người liên hệ (`PatientBanner`).
  * Nạp dữ liệu khám lâm sàng, sinh hiệu, tứ chẩn YHCT, mã ICD (`ClinicalExam`).
  * Nạp danh sách dịch vụ điều trị đã chỉ định (`ServiceIndication`).
  * Nạp đơn thuốc thang đã kê (`Prescription`).
  * Tự động truy vấn 5 lần khám gần nhất (`PreviousVisits`) và phác đồ liệu trình đang điều trị (`ActiveProtocol`).
  * Thiết lập cờ `IsReadOnly = true` nếu ca đã `Hoàn tất` hoặc còn `Chờ khám` chưa bấm gọi.
* **Response:** `200 OK` kèm `MedicalRecordWorkspaceDto`.

#### API-PHUC-09: Lưu thông tin khám lâm sàng, sinh hiệu & tứ chẩn YHCT
* **Method & URL:** `PUT /api/v1/doctor/examinations/{examSheetId}/clinical`
* **Request Body (`SaveClinicalExamRequestDto`):**
  * `ExamStartTime` (DateTime).
  * `VitalSigns`: Mạch (lần/phút), Nhiệt độ (°C), Nhịp thở, Chiều cao (cm), Cân nặng (kg), SpO2 (%).
  * `Symptoms`: Triệu chứng lâm sàng (tự động bảo toàn cờ `[Ưu tiên]`).
  * `PreliminaryDiagnosis`: Chẩn đoán sơ bộ.
  * `TraditionalMedicine`: Vọng chẩn, Văn chẩn nghe/ngửi, Vấn chẩn hỏi, Thiết chẩn (mạch/ấn), Tóm tắt tứ chẩn, Biện chứng, Chẩn đoán YHCT.
  * `IcdCode`, `IcdName`: Mã ICD và tên bệnh.
* **Logic:** Chặn sửa nếu ca đã `Hoàn tất` (`400 Bad Request`). Cập nhật dữ liệu vào `ExamSheet` và lưu toàn bộ metadata vào `DoctorAdvice`.
* **Response:** `200 OK` kèm `MedicalRecordWorkspaceDto` cập nhật.

#### API-PHUC-10: Hoàn tất lượt khám bệnh
* **Method & URL:** `POST /api/v1/doctor/examinations/{examSheetId}/complete`
* **Request Body (`SaveClinicalExamRequestDto?`):** Tùy chọn truyền kèm dữ liệu cập nhật lần cuối trước khi đóng hồ sơ.
* **Logic:**
  * Cập nhật trạng thái `ExamSheet` và `MedicalRecord` sang `Hoàn tất` (`COMPLETED`).
  * Ghi nhận thời gian hoàn tất `CompletedAt = DateTime.Now`.
  * Khóa hồ sơ bệnh án: trả về `IsReadOnly = true`.
* **Response:** `200 OK` kèm `MedicalRecordWorkspaceDto` ở chế độ chỉ đọc.

#### API-PHUC-11: Kê dịch vụ điều trị (Tab Kê dịch vụ DOC-03)
* **Method & URL:** `PUT /api/v1/doctor/examinations/{examSheetId}/services`
* **Request Body (`SaveServiceIndicationRequestDto`):** `TreatmentType` ("Theo liệu trình" / "Đơn lẻ"), `TotalSessions` (số buổi), `IndicationNote`, `AdditionalContent`, `Services` (danh sách `ServiceId`, `Note`).
* **Logic:**
  * Nếu là `"Theo liệu trình"`: tự động tạo mới hoặc cập nhật bản ghi trong `TreatmentProtocol` (`PlannedSessionCount`, `Description`).
  * Đồng bộ các dịch vụ chỉ định vào `UsageHistory` gắn liền với `ExamSheetId`.
* **Response:** `200 OK` kèm `ServiceIndicationDto`.

#### API-PHUC-12: Danh mục bài thuốc mẫu & vị dược liệu kê đơn (Tab DOC-04)
* **Method & URL:** `GET /api/v1/doctor/prescriptions/catalog`
* **Mục đích:**
  * Lấy danh sách bài thuốc mẫu (`RemedyFormula`) có sẵn trong hệ thống kèm chi tiết từng vị thuốc để UI hỗ trợ tính năng "Nạp công thức" tự động.
  * Lấy danh mục vị dược liệu đang hoạt động (`Herb`) để bác sĩ chọn thêm vị thuốc lẻ và gia giảm liều lượng.
* **Response:** `200 OK` kèm `PrescriptionCatalogDto`.

#### API-PHUC-13: Kê đơn thuốc (Lưu nháp / Gửi Herbalist)
* **Method & URL:** `PUT /api/v1/doctor/examinations/{examSheetId}/prescription`
* **Request Body (`SavePrescriptionRequestDto`):**
  * `ActionType`: `"SAVE_DRAFT"` (Lưu nháp) hoặc `"SEND_HERBALIST"` (Gửi cho Herbalist bốc thuốc).
  * `PrescriptionMethod`: `"Công thức đã duyệt"` hoặc `"Tự kê đơn"`.
  * `TemplateFormulaId`: ID bài thuốc mẫu nếu có.
  * `TotalDoses`: Số thang thuốc (ví dụ: 5).
  * `PrescriptionNote`: Hướng dẫn sử dụng (cách sắc, thời điểm uống).
  * `Ingredients`: Danh sách vị thuốc (`HerbId`, `GramsPerDose`, `Note`).
* **Logic:**
  * Kiểm tra nếu đơn thuốc đã ở trạng thái `"Đã gửi Herbalist"` $\rightarrow$ Chặn sửa đổi và trả về `400 Bad Request`.
  * Tạo hoặc cập nhật `RemedyFormula` gắn với `ExamSheetId`, cập nhật chi tiết vị thuốc trong `FormulaDetail`.
  * Tự động tính toán tổng lượng dược liệu: $\text{TotalGrams} = \text{GramsPerDose} \times \text{TotalDoses}$.
  * Nếu gửi Herbalist: chuyển trạng thái đơn sang `"Đã gửi Herbalist"` và đặt `IsLocked = true`.
* **Response:** `200 OK` kèm `PrescriptionDetailDto`.

---

## IV. BẢNG TỔNG KẾT TIẾN ĐỘ VÀ KIỂM THỬ

| Module / Phân hệ | Số lượng API | Tầng Controller | Tầng Service | Unit Test (xUnit) | Trạng thái |
| :--- | :---: | :--- | :--- | :---: | :---: |
| **Cụm 1: Danh mục Bác sĩ** | 2 | `DoctorsController` | `DoctorService` | 2/2 PASS | Hoàn thành |
| **Cụm 2: Dịch vụ Trị liệu** | 3 | `TreatmentServicesController` | `TreatmentService` | 9/9 PASS | Hoàn thành |
| **Cụm 3: Khám lâm sàng Bác sĩ** | 8 | `DoctorClinicalController` | `DoctorClinicalService` | 9/9 PASS | Hoàn thành |
| **TỔNG CỘNG** | **13 APIs** | **3 Controllers** | **3 Services** | **20/20 PASS** | **100% Hoàn thành** |

* Toàn bộ solution: Biên dịch thành công **0 lỗi**, **80/80 Unit Tests PASS 100%**.
