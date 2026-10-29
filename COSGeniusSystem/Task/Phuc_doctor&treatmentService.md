# BÁO CÁO CÔNG VIỆC THỰC HIỆN - MODULE BÁC SĨ & DỊCH VỤ TRỊ LIỆU

> **Dự án:** COSGeniusSystem (Hệ thống quản lý phòng khám đông y & dược liệu)  
> **Người thực hiện:** Phúc  
> **Nền tảng:** .NET 10 (ASP.NET Core Web API), Entity Framework Core (SQL Server & InMemory)

---

## I. TỔNG QUAN CÔNG VIỆC ĐÃ HOÀN THÀNH

1. **Kết nối Cơ sở dữ liệu:**
   - Cấu hình lại chuỗi kết nối trong `appsettings.json` kết nối chuẩn xác vào cơ sở dữ liệu `ClinicHerbManagementDB` trên máy cục bộ (`Server=localhost`).
2. **Triển khai Cụm 1 - Module Bác sĩ (Doctor):**
   - Xây dựng API lấy danh sách bác sĩ có lọc đa điều kiện (`API-PHUC-01`).
   - Xây dựng API xem chi tiết bác sĩ kèm thông tin tài khoản liên kết (`API-PHUC-02`).
3. **Triển khai Cụm 2 - Module Dịch vụ trị liệu (Treatment Service):**
   - Xây dựng API lấy danh mục bảng giá dịch vụ khả dụng (`API-PHUC-03`).
   - Xây dựng API thêm mới dịch vụ trị liệu kèm validation chống trùng và kiểm tra giá (`API-PHUC-04`).
   - Xây dựng API cập nhật dịch vụ trị liệu và bảo đảm tính bất biến của hóa đơn cũ (`API-PHUC-05`).
4. **Tổ chức kiến trúc chuẩn N-Tier:**
   - Tách biệt rõ ràng các tầng: DTOs, Services (Interfaces + Implementations), Controllers.
   - Đăng ký Dependency Injection (`AddScoped`) trong `Program.cs`.
5. **Xây dựng toàn diện bộ mã nguồn Unit Test (xUnit & EF Core InMemory):**
   - Viết trọn vẹn 11 test cases từ `UT-PHUC-01` đến `UT-PHUC-05`.
   - Toàn bộ 11/11 test cases đều **PASS 100%**.

---

## II. CẤU TRÚC FILE MÃ NGUỒN ĐÃ TẠO

```text
COSGeniusSystem/
├── Controllers/
│   ├── DoctorsController.cs                       # Endpoints API-PHUC-01, API-PHUC-02
│   └── TreatmentServicesController.cs             # Endpoints API-PHUC-03, 04, 05
├── DTOs/
│   ├── Doctors/
│   │   ├── DoctorFilterQuery.cs                   # Tham số lọc danh sách bác sĩ
│   │   ├── DoctorResponseDto.cs                   # Output danh sách bác sĩ
│   │   └── DoctorDetailDto.cs                     # Output chi tiết bác sĩ kèm user
│   └── TreatmentServices/
│       ├── TreatmentServiceResponseDto.cs         # Output thông tin dịch vụ trị liệu
│       ├── CreateTreatmentServiceRequest.cs       # Input thêm mới dịch vụ
│       └── UpdateTreatmentServiceRequest.cs       # Input cập nhật dịch vụ
├── Services/
│   ├── Doctors/
│   │   ├── IDoctorService.cs                      # Interface nghiệp vụ Bác sĩ
│   │   └── DoctorService.cs                       # Xử lý truy vấn Bác sĩ qua EF Core
│   └── TreatmentServices/
│       ├── ITreatmentService.cs                   # Interface nghiệp vụ Dịch vụ trị liệu
│       └── TreatmentService.cs                    # Xử lý nghiệp vụ & validation dịch vụ
├── UnitTests/
│   ├── UnitTests.csproj                           # Project kiểm thử xUnit & InMemory EF Core
│   ├── Doctors/
│   │   └── DoctorServiceTests.cs                  # Unit Test UT-PHUC-01, UT-PHUC-02
│   └── TreatmentServices/
│       └── TreatmentServiceTests.cs               # Unit Test UT-PHUC-03, UT-PHUC-04, UT-PHUC-05
└── Task/
    └── Phuc_doctor&treatmentService.md                                    # File báo cáo
```

---

## III. CHI TIẾT DANH SÁCH API ENDPOINTS

### 1. CỤM 1: MODULE BÁC SĨ (DOCTOR)

#### API-PHUC-01: Lấy danh sách bác sĩ / kỹ thuật viên
* **Method & URL:** `GET /api/v1/doctors`
* **Query Parameters (`DoctorFilterQuery`):**
  * `keyword` *(string, optional)*: Tìm kiếm tương đối không phân biệt hoa thường theo `FullName` hoặc `Phone`.
  * `specialization` *(string, optional)*: Lọc theo chuyên khoa / chuyên môn.
  * `isActive` *(bool, optional, mặc định: `true`)*: Lọc trạng thái hoạt động.
* **Logic:** Dùng `AsNoTracking()`, kết hợp các bộ lọc và ánh xạ sang `DoctorResponseDto`.
* **Mẫu gọi:**
  ```text
  GET http://localhost:5217/api/v1/doctors?keyword=Nguyen&specialization=Châm cứu&isActive=true
  ```
* **Response Output (`200 OK`):**
  ```json
  [
    {
      "doctorId": 1,
      "fullName": "Bác sĩ Nguyễn Văn A",
      "specialization": "Châm cứu",
      "phone": "0912345678",
      "isActive": true
    }
  ]
  ```

---

#### API-PHUC-02: Xem chi tiết bác sĩ
* **Method & URL:** `GET /api/v1/doctors/{id}`
* **Route Parameter:** `id` *(int)*: Khóa chính `DoctorId`.
* **Logic:** Truy vấn `Doctors` theo `id`, kết hợp `.Include(d => d.User)`. Dùng `AsNoTracking()`. Nếu không tìm thấy, trả về `404 Not Found`.
* **Mẫu gọi:**
  ```text
  GET http://localhost:5217/api/v1/doctors/1
  ```
* **Response Output (`200 OK`):**
  ```json
  {
    "doctorId": 1,
    "fullName": "Bs. CKI Nguyễn Văn Tuấn",
    "specialization": "Y học cổ truyền",
    "licenseNumber": "CCHN-00123",
    "phone": "0987654321",
    "isActive": true,
    "username": "dr.nguyen",
    "email": "dr.nguyen@clinicherb.vn"
  }
  ```
* **Trường hợp lỗi (`404 Not Found`):**
  ```json
  {
    "message": "Doctor with id 99999 was not found."
  }
  ```

---

### 2. CỤM 2: MODULE DỊCH VỤ TRỊ LIỆU (TREATMENT SERVICE)

#### API-PHUC-03: Danh mục bảng giá dịch vụ trị liệu
* **Method & URL:** `GET /api/v1/services`
* **Logic:** Lấy toàn bộ danh sách dịch vụ trong bảng `[Service]` có `IsActive == true`, dùng `AsNoTracking()`.
* **Mẫu gọi:**
  ```text
  GET http://localhost:5217/api/v1/services
  ```
* **Response Output (`200 OK`):**
  ```json
  [
    {
      "serviceId": 1,
      "serviceCode": "DV001",
      "serviceName": "Châm cứu trị liệu",
      "price": 150000.0,
      "unit": "Lần",
      "description": "Châm cứu thông kinh hoạt lạc",
      "isActive": true
    }
  ]
  ```

---

#### API-PHUC-04: Thêm mới dịch vụ vào bảng giá
* **Method & URL:** `POST /api/v1/services`
* **Content-Type:** `application/json`
* **Request Body (`CreateTreatmentServiceRequest`):**
  ```json
  {
    "serviceCode": "DV002",
    "serviceName": "Xoa bóp bấm huyệt",
    "price": 200000,
    "unit": "Lần",
    "description": "Xoa bóp phục hồi chức năng cổ vai gáy"
  }
  ```
* **Validation & Logic:**
  * Bắt buộc có: `ServiceCode`, `ServiceName`, `Price`, `Unit`.
  * `Price` bắt buộc phải `> 0`. Nếu `<= 0` $\rightarrow$ Trả về `400 Bad Request`.
  * Kiểm tra trùng lặp: Nếu `ServiceCode` hoặc `ServiceName` đã tồn tại $\rightarrow$ Trả về `400 Bad Request`.
  * Mặc định lưu với `IsActive = true`.
* **Response Output (`201 Created` kèm header `Location`):**
  ```json
  {
    "serviceId": 2,
    "serviceCode": "DV002",
    "serviceName": "Xoa bóp bấm huyệt",
    "price": 200000.0,
    "unit": "Lần",
    "description": "Xoa bóp phục hồi chức năng cổ vai gáy",
    "isActive": true
  }
  ```

---

#### API-PHUC-05: Cập nhật dịch vụ trị liệu
* **Method & URL:** `PUT /api/v1/services/{id}`
* **Route Parameter:** `id` *(int)*: ID dịch vụ cần cập nhật.
* **Content-Type:** `application/json`
* **Request Body (`UpdateTreatmentServiceRequest`):**
  ```json
  {
    "serviceName": "Châm cứu trị liệu nâng cao",
    "price": 180000,
    "unit": "Lần",
    "description": "Châm cứu kết hợp điện châm",
    "isActive": true
  }
  ```
* **Validation & Logic:**
  * Tìm dịch vụ theo `id`. Nếu không tồn tại $\rightarrow$ Trả về `404 Not Found`.
  * Kiểm tra `Price > 0`. Nếu `<= 0` $\rightarrow$ Trả về `400 Bad Request`.
  * Kiểm tra trùng tên: Nếu `ServiceName` mới bị trùng với 1 dịch vụ khác $\rightarrow$ Trả về `400 Bad Request`.
  * Cập nhật các trường và lưu vào database.
* **Response Output (`200 OK`):**
  ```json
  {
    "serviceId": 1,
    "serviceCode": "DV001",
    "serviceName": "Châm cứu trị liệu nâng cao",
    "price": 180000.0,
    "unit": "Lần",
    "description": "Châm cứu kết hợp điện châm",
    "isActive": true
  }
  ```