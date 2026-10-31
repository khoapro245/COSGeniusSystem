# Genius: API khám bệnh V2

## Phạm vi và kiểm tra thực tế

Đã triển khai Controller → IExaminationService/ExaminationService → ApplicationDbContext, tái sử dụng Patient/Auth/Doctor/Herb/Service. Không tạo Repository, không sửa Models/DbContext/schema/Dockerfile/connection string/package. Không tìm thấy AGENTS.md. Đã đối chiếu REC-02, DOC-03, DOC-04, DOC-06B trong GENIUS. Không dùng entity legacy trong luồng mới; không thực hiện thanh toán, xuất kho, bốc thuốc hay điều trị.

Kết quả ngày 29/10/2026:

- 56 contract checks pass: compile độc lập DTO/controller/interface trên .NET9, kiểm tra role attributes, CancellationToken, validation DTO, base64 RowVersion, envelope; kiểm tra cú pháp 132 file C# production.
- Kiểm tra kiểu service/controller thay đổi với Models hiện tại, EF9 cache và phần khai báo DbSet: không lỗi. Đây chỉ là kiểm tra tương thích cục bộ, không phải build .NET10 và không kiểm tra OnModelCreating/SQL translation/trigger.
- `docker build -f Dockerfile -t genius-examination-check .` bị chặn quyền truy cập Docker config/buildx/engine.
- `dotnet build COSGeniusSystem/COSGeniusSystem.csproj --no-restore` và `dotnet run --project tests/ExaminationApi.SqlChecks` bị NETSDK1045: SDK cao nhất 9.0.308, dự án dùng net10.0. **Chưa chạy SQL integration hoặc JWT end-to-end.**
- Đọc metadata constraints/triggers qua kết nối hiện có ở chế độ read-only không thành công. Không ghi dữ liệu thử vào database chính, không chạy migration. Không commit/push.

Chạy lại contract checks không cần NuGet:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/ExaminationApi.ContractChecks/Run-Checks.ps1
```

## Endpoint theo thứ tự gọi

`E = /api/v1/examinations`; r = RegistrationId, p = PrescriptionId, t = PlanId. JWT lấy từ Auth: `Authorization: Bearer <data.accessToken>`.

| Bước | Endpoint | ID lấy cho bước tiếp |
|---|---|---|
| Đăng nhập | POST `/api/Auth/login` | data.accessToken |
| Tìm bệnh nhân | GET `/api/patients?search=0900&pageNumber=1&pageSize=20` | data.items[].patientId |
| Tạo bệnh nhân nếu cần | POST `/api/patients` | data.patientId |
| Bác sĩ phân công được | GET `/api/v1/doctors?assignableOnly=true&isActive=true` | data[].doctorId |
| Tiếp đón | POST `E` | data.registrationId, data.rowVersion |
| Danh sách lễ tân | GET `E?fromDate=2026-10-29&toDate=2026-10-29&doctorId=7&status=WAITING&search=0900&pageNumber=1&pageSize=20` | data.items[].registrationId |
| Chi tiết | GET `E/{r}` | registration, patient, medicalRecord, prescriptions, treatmentPlans trong data |
| Sửa WAITING | PUT `E/{r}` | RowVersion đăng ký mới |
| Danh sách bác sĩ | GET `E/mine?status=WAITING&pageNumber=1&pageSize=20` | RegistrationId |
| Lịch sử | GET `E/{r}/history?pageNumber=1&pageSize=20` | data.items[].recordId |
| Chi tiết lịch sử | GET `E/{r}/history/{recordId}` | Hồ sơ, đơn và dịch vụ cùng PatientId |
| Bắt đầu/tiếp tục | POST `E/{r}/start`, không body | data.recordId, data.rowVersion MedicalRecord |
| Lưu khám | PUT `E/{r}/medical-record` | RowVersion MedicalRecord mới |
| Dược liệu hoạt động | GET `/api/v1/herbs?keyword=cam&isActive=true&page=1&pageSize=20` | data[].herbId |
| Công thức APPROVED, active | GET `E/herbal-formulas?search=&pageNumber=1&pageSize=20` | data.items[].herbalFormulaId và items |
| Tạo/lấy đơn | POST `E/{r}/prescriptions`, không body | data.prescriptionId, data.rowVersion |
| Nạp công thức | POST `E/{r}/prescriptions/{p}/load-formula` | RowVersion đơn mới |
| Lưu/gia giảm | PUT `E/{r}/prescriptions/{p}` | RowVersion đơn mới |
| Gửi Herbalist | POST `E/{r}/prescriptions/{p}/send` | SENT, sentAt |
| Xóa đơn nháp | DELETE `E/{r}/prescriptions/{p}`, body RowVersion | Không còn nháp |
| Dịch vụ hoạt động | GET `/api/v1/services` | data[].serviceId |
| Tạo/lấy chỉ định | POST `E/{r}/treatment-plans`, không body | data.planId, data.rowVersion |
| Lưu chỉ định | PUT `E/{r}/treatment-plans/{t}` | RowVersion plan mới |
| Xác nhận | POST `E/{r}/treatment-plans/{t}/confirm` | CONFIRMED, confirmedAt |
| Xóa chỉ định nháp | DELETE `E/{r}/treatment-plans/{t}`, body RowVersion | Không còn nháp |
| Hoàn tất | POST `E/{r}/complete` | MedicalRecord/Registration COMPLETED cùng thời điểm |

POST tiếp đón trả 201 và Location; các thao tác còn lại trả 200. Lỗi DTO/nghiệp vụ 400, chưa đăng nhập 401, sai quyền 403, không tìm thấy/ID khác lượt 404, sai trạng thái/phiên bản/xung đột 409. Module mới giữ code/message/isSuccess/data. Herb GET danh sách đã được chuẩn hóa: mảng trước đây nay ở data trong ApiResponseModel; không thêm API catalog trùng.

## JSON mẫu khớp DTO

Các ID chỉ minh họa: thay bằng ID thực theo bảng. RowVersion là base64 của đúng entity đang sửa; không dùng phiên bản Registration cho MedicalRecord/Prescription/Plan. AAAAAAAAAAE= chỉ minh họa, luôn thay bằng phiên bản server trả mới nhất sau mỗi lần ghi.

Đăng nhập bằng tài khoản đã có role:

```json
{"username":"reception-account","password":"<password>"}
```

POST Patient hiện có:

```json
{"fullName":"Nguyễn Văn Minh","gender":"Nam","dateOfBirth":"1985-04-12","phone":"0900000001","address":"TP. Hồ Chí Minh"}
```

POST tiếp đón; RegisteredBy lấy từ JWT:

```json
{"patientId":12,"doctorId":7,"contactId":null,"isPriority":false,"symptoms":"Đau mỏi vai gáy","note":"Tiếp nhận trực tiếp"}
```

PUT tiếp đón WAITING; không sửa PatientId/STT/BusinessDate/RegisteredBy/trạng thái:

```json
{"doctorId":7,"contactId":null,"isPriority":true,"symptoms":"Đau mỏi vai gáy","note":"Ưu tiên","rowVersion":"AAAAAAAAAAE="}
```

PUT medical-record lưu toàn bộ nội dung; trường bỏ qua được lưu null. Cho lưu nháp trước khi đủ thông tin hoàn tất:

```json
{
  "rowVersion":"AAAAAAAAAAE=",
  "symptoms":"Đau mỏi vai gáy",
  "preliminaryDiagnosis":"Đau cổ vai gáy",
  "finalDiagnosis":"Đau vùng cổ",
  "doctorAdvice":"Theo dõi và tái khám theo hướng dẫn",
  "pulseRate":76,"temperature":36.8,"respiratoryRate":18,
  "heightCm":170,"weightKg":65,"spO2":98,
  "inspection":"Vọng chẩn","listeningSmelling":"Văn chẩn",
  "inquiry":"Vấn chẩn","palpation":"Thiết chẩn",
  "fourExaminationSummary":"Tóm tắt tứ chẩn",
  "syndromeDifferentiation":"Biện chứng",
  "traditionalDiagnosis":"Chẩn đoán YHCT",
  "icdcode":"M54.2","icddescription":"Đau vùng cổ"
}
```

POST load-formula thay thành phần bằng bản sao công thức, giữ số thang và lấy tên/hướng dẫn từ mẫu:

```json
{"herbalFormulaId":3,"rowVersion":"AAAAAAAAAAE="}
```

PUT đơn: items là toàn bộ thành phần muốn giữ; thêm/sửa/bỏ item để gia giảm. Tổng lượng trả về = DosagePerDose × TotalDoses. DosagePerDose tối đa ba chữ số thập phân. Không sửa công thức mẫu:

```json
{
  "rowVersion":"AAAAAAAAAAE=","prescriptionName":"Đơn gia giảm",
  "totalDoses":5,"dosageDays":5,
  "usageInstructions":"Sử dụng theo hướng dẫn của bác sĩ","note":"Gia giảm",
  "items":[{"herbId":9,"dosagePerDose":8,"roleInFormula":null,"note":null}]
}
```

Send yêu cầu số thang dương, hướng dẫn không trống, có ít nhất một vị liều dương và dược liệu còn hoạt động. Không kiểm tra tồn kho hoặc tạo MedicineDispense.

PUT plan: PlannedQuantity là tổng lượng dịch vụ cả plan, độc lập số buổi. Mỗi buổi một lần trong năm buổi gửi cả hai bằng 5; backend không tự nhân lại. Không nhận giá/tên/đơn vị frontend: snapshot từ Service lúc thêm dòng, giữ snapshot dòng cũ khi sửa lượng/hướng dẫn:

```json
{
  "rowVersion":"AAAAAAAAAAE=","planName":"Liệu trình vai gáy",
  "treatmentType":"COURSE","plannedSessionCount":5,
  "description":"Theo dõi đáp ứng",
  "items":[{"serviceId":4,"plannedQuantity":5,"instructions":"Theo chỉ định"}]
}
```

SINGLE phải có plannedSessionCount=1. Xác nhận không coi là thanh toán/thực hiện; không tạo buổi, lịch, UsageHistory hoặc phân công bác sĩ điều trị.

Send/confirm/complete và hai DELETE nhận cùng cấu trúc, dùng RowVersion đơn/plan/MedicalRecord tương ứng:

```json
{"rowVersion":"AAAAAAAAAAE="}
```

## Quyền, trạng thái và đồng thời

- RECEPTION_STAFF tiếp đón, sửa đăng ký WAITING và xem danh sách/chi tiết. Patient API cho RECEPTION_STAFF/DOCTOR đọc; chỉ RECEPTION_STAFF tạo/sửa/xóa Patient.
- DOCTOR chỉ thao tác lượt phân công; ánh xạ Doctor.UserId từ JWT UserId, kiểm tra Doctor/User hoạt động. Bác sĩ được phân công phải có role DOCTOR trong database. Không tự thêm quyền vượt phân công cho ADMIN/MANAGER.
- Lịch sử chi tiết kiểm tra cùng PatientId; mọi ID đơn/plan tra cùng RecordId của registration trên route.
- WAITING → IN_PROGRESS → COMPLETED. Tiếp đón không tạo MedicalRecord. Start tạo đúng một hồ sơ; gọi lặp trả hồ sơ hiện có, kể cả đã COMPLETED.
- Mọi mutation của lượt và đơn/plan con khóa cùng hàng Registration bằng UPDLOCK,HOLDLOCK trong transaction, ngăn complete chạy xen với kê đơn. EF RowVersion và kiểm tra phiên bản chặn dữ liệu cũ; SQL deadlock/lock timeout trả 409 để client tải lại và thử lại.
- Cấp STT khóa counter theo BusinessDate bằng UPDLOCK,HOLDLOCK bảo vệ cả ngày chưa có counter; cấp số và insert đăng ký trong cùng transaction. BusinessDate là ngày Việt Nam UTC+7, STT toàn phòng khám mỗi ngày. Mã do computed columns cấp.
- Hai nhánh thuốc/dịch vụ độc lập, có thể không kê. Hoàn tất yêu cầu Symptoms/FinalDiagnosis và không còn DRAFT; không tự gửi/xóa/xác nhận nháp. MedicalRecord và Registration COMPLETED cùng timestamp trong transaction.
- PUT/load/delete chỉ cho DRAFT khi lượt IN_PROGRESS. Send/confirm/complete lặp giữ timestamp, không gửi/xác nhận lại. Sau COMPLETED không sửa chuyên môn.
- POST đơn/plan trả aggregate hiện có kể cả sau gửi/xác nhận, không tạo thêm do retry. Xóa nháp rồi tạo lại được. Phạm vi hiện tại là một đơn/một plan trên màn hình mỗi lượt.

## Chạy SQL integration trên database test riêng

Cần .NET10 và database clone V2 đã provision đủ constraints/triggers. Suite không tạo/sửa schema, không đọc appsettings production, không fallback database chính.

```powershell
$env:GENIUS_EXAM_TEST_CONNECTION = '<connection string database test V2>'
$env:GENIUS_EXAM_ALLOW_TEST_WRITES = 'GeniusExaminationTest_<tên môi trường>'
dotnet run --project tests/ExaminationApi.SqlChecks
```

Initial Catalog phải bắt đầu GeniusExaminationTest_ và khớp chính xác biến cho phép; thiếu cấu hình suite dừng. Suite tạo fixture có suffix riêng và giữ lại trong database test để điều tra. Bao gồm cấp 12 STT đồng thời, 8 Start đồng thời, retry tạo đơn/plan, phân công, ID khác lượt, snapshot/gia giảm, giá backend, phiên bản cũ, DRAFT/SENT/CONFIRMED và cả bốn nhánh hoàn tất. Đây là kiểm tra tầng service; cần chạy thêm API với JWT thật ở test deployment.

## Quy tắc còn cần xác nhận

1. Hoàn tất hiện yêu cầu Symptoms và FinalDiagnosis; source chưa chỉ rõ ICD/tứ chẩn/chỉ số/lời khuyên nào bắt buộc.
2. Hiện một đơn thuốc/một plan mỗi lượt: POST tái sử dụng aggregate để chống retry trùng. Nếu cần nhiều đơn/plan độc lập cần quy tắc định danh request bền vững; schema không có IdempotencyKey, không tự thêm cột/constraint.
3. Phải đối chiếu CHECK/trigger V2 chấp nhận CONFIRMED và các chuyển trạng thái thực tế. Scaffold chỉ có string/default, chưa đủ định nghĩa CHECK/trigger; đọc metadata chưa thành công.
4. Chưa có quy tắc backend tạo sẵn TreatmentSession; hiện để bước tiếp nhận/thực hiện điều trị sau này tạo buổi, không tạo MedicalRecord cho từng buổi.
5. PlannedQuantity hiện là tổng lượng cả plan; xác nhận nếu nghiệp vụ muốn lượng trên mỗi buổi.
6. POST tiếp đón tạo lượt mới; schema không có idempotency key cho retry sau timeout mạng. Không tự cấm nhiều lượt một bệnh nhân trong ngày.
