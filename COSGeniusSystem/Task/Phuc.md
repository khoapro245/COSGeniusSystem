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
    └── Phuc.md                                    # File báo cáo & tài liệu kiểm thử
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

---

## IV. HƯỚNG DẪN CHẠY CHƯƠNG TRÌNH ĐỂ TEST API

### 1. Khởi động ứng dụng
Chạy lệnh tại thư mục `COSGeniusSystem`:
```bash
dotnet run
```
Hoặc bấm phím **F5** trong Visual Studio. Server sẽ chạy tại: `http://localhost:5217`.

### 2. Kiểm thử API bằng trình duyệt (Đối với các API GET)
* Xem danh sách dịch vụ: [http://localhost:5217/api/v1/services](http://localhost:5217/api/v1/services)
* Xem danh sách bác sĩ: [http://localhost:5217/api/v1/doctors](http://localhost:5217/api/v1/doctors)
* Xem chi tiết bác sĩ 1: [http://localhost:5217/api/v1/doctors/1](http://localhost:5217/api/v1/doctors/1)

### 3. Kiểm thử API bằng cURL / Postman (Dành cho POST & PUT)

* **Thêm mới dịch vụ (POST):**
  ```bash
  curl -X POST http://localhost:5217/api/v1/services \
    -H "Content-Type: application/json" \
    -d "{\"serviceCode\":\"DV003\",\"serviceName\":\"Thủy châm trị liệu\",\"price\":250000,\"unit\":\"Lần\",\"description\":\"Tiêm thuốc vào huyệt\"}"
  ```

* **Cập nhật dịch vụ (PUT):**
  ```bash
  curl -X PUT http://localhost:5217/api/v1/services/1 \
    -H "Content-Type: application/json" \
    -d "{\"serviceName\":\"Châm cứu phục hồi\",\"price\":180000,\"unit\":\"Lần\",\"description\":\"Cập nhật mô tả\",\"isActive\":true}"
  ```

---

## V. ĐẶC TẢ VÀ HƯỚNG DẪN CHẠY UNIT TEST

Toàn bộ Unit Test được lập trình độc lập bằng **xUnit** và **Entity Framework Core InMemory**, chạy hoàn toàn trên RAM, không phụ thuộc vào SQL Server và không làm biến đổi dữ liệu thực tế.

### 1. Danh sách 11 Test Cases đã triển khai

| Mã Task | Tên Test Method | Mô tả & Kiểm tra |
| :--- | :--- | :--- |
| **UT-PHUC-01** | `GetDoctorsAsync_FilterByIsActive_ReturnsOnlyActiveDoctors` | Seed bác sĩ active/inactive $\rightarrow$ Chỉ trả về các bác sĩ có `IsActive = true`. |
| **UT-PHUC-01** | `GetDoctorsAsync_FilterBySpecialization_ReturnsMatchingSpecialization` | Seed nhiều chuyên môn $\rightarrow$ Trả về chính xác danh sách bác sĩ theo chuyên môn lọc. |
| **UT-PHUC-02** | `GetDoctorByIdAsync_NonExistingId_ReturnsNull` | Truy vấn ID không tồn tại `99999` $\rightarrow$ Trả về `null` để Controller map thành 404. |
| **UT-PHUC-02** | `GetDoctorByIdAsync_ExistingIdWithLinkedUser_ReturnsMappedDoctorDetailAndUser` | Seed Doctor có liên kết `User` $\rightarrow$ Map đầy đủ thông tin bác sĩ, Username, Email. |
| **UT-PHUC-03** | `GetActiveServicesAsync_ReturnsOnlyActiveServicesWithValidPrice` | Seed dịch vụ active/inactive $\rightarrow$ Chỉ trả về dịch vụ active và kiểm tra `Price > 0`. |
| **UT-PHUC-04** | `CreateServiceAsync_PriceLessOrEqualToZero_ReturnsValidationError` (Giá = 0) | Truyền `Price = 0` $\rightarrow$ Trả về lỗi validation, không lưu vào database. |
| **UT-PHUC-04** | `CreateServiceAsync_PriceLessOrEqualToZero_ReturnsValidationError` (Giá = -1000) | Truyền `Price = -1000` $\rightarrow$ Trả về lỗi validation, không lưu vào database. |
| **UT-PHUC-04** | `CreateServiceAsync_DuplicateServiceNameOrCode_ReturnsConflictError` | Tạo mới với tên dịch vụ đã tồn tại $\rightarrow$ Báo lỗi trùng lặp tên/mã (Conflict/Bad Request). |
| **UT-PHUC-04** | `CreateServiceAsync_ValidData_CreatesSuccessfullyWithActiveStatus` | Dữ liệu hợp lệ $\rightarrow$ Thêm mới thành công vào DB với `IsActive = true`. |
| **UT-PHUC-05** | `UpdateServiceAsync_ValidRequest_UpdatesServicePriceSuccessfully` | Cập nhật giá và thông tin $\rightarrow$ Bản ghi trong bảng `[Service]` đổi giá thành công. |
| **UT-PHUC-05** | `UpdateServiceAsync_PriceChange_PreservesHistoricInvoiceDetailUnitPrice` | Cập nhật giá dịch vụ từ 100k lên 150k $\rightarrow$ **Đơn giá trong `InvoiceDetail` của hóa đơn cũ vẫn giữ nguyên 100k**. |

---

### 2. Cách chạy Unit Test

#### Cách A: Chạy bằng dòng lệnh Terminal (Nhanh nhất)
Mở terminal tại thư mục `COSGeniusSystem` và gõ lệnh:
```bash
dotnet test UnitTests/UnitTests.csproj
```

Để xem danh sách chi tiết từng test case được thực thi:
```bash
dotnet test UnitTests/UnitTests.csproj --logger "console;verbosity=normal"
```

#### Cách B: Chạy qua giao diện Visual Studio
1. Vào menu **Test** $\rightarrow$ chọn **Test Explorer** (hoặc bấm `Ctrl + E, T`).
2. Bấm nút **Run All Tests in View** (biểu tượng 2 tam giác xanh $\blacktriangleright\blacktriangleright$).
3. Quan sát kết quả: Tất cả 11 test cases đều sáng xanh.

---

### 3. Kết quả chạy thực tế
```text
Test run for E:\DoAnTotNghiep\COSGeniusSystem\COSGeniusSystem\UnitTests\bin\Debug\net10.0\UnitTests.dll (.NETCoreApp,Version=v10.0)

Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 1.4s
```
