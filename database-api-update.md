# Cập nhật API theo database.sql ngày 02/11/2026

Nguồn chuẩn: database.sql mới, 39 bảng, script lúc 11:34:06. Báo cáo database-code-comparison.md có sẵn đối chiếu bản SQL trước đó.

Đã bỏ EF mapping/model/navigation của ExamSheet, RemedyFormula, FormulaDetail, TreatmentProtocol. UsageHistory chỉ dùng SessionID, PlanDetailID, PerformedBy bắt buộc. Dashboard dùng ExaminationRegistration/TreatmentSession và trạng thái thực tế; lịch sử dùng MedicalRecord/HerbalPrescription/TreatmentPlan/TreatmentSession, tên thuốc/dịch vụ dùng snapshot. Không tự tạo MedicalRecord khi thêm bệnh nhân; hồ sơ được tạo khi bác sĩ bắt đầu khám.

## Client cần cập nhật

- GET /api/v1/reception/dashboard/quick-queues: waitingExaminations[].examSheetId đổi thành registrationId; waitingTreatments[].usageId đổi thành sessionId.
- GET /api/v1/treatments/queue: cần JWT ReceptionStaff hoặc Doctor, bác sĩ chỉ thấy hàng đợi được phân công. Dùng sessionId, thêm planId/rowVersion; status là REQUESTED.
- POST /api/v1/treatments/queue: cần JWT ReceptionStaff. Body mới bên dưới; rowVersion là phiên bản kế hoạch lấy từ API treatment-plans. Response chứa phiên bản session. Dùng chung TreatmentWorkflowService để kiểm tra thanh toán, số buổi/dịch vụ còn lại, phân công bác sĩ, khóa giao dịch. Không còn tạo UsageHistory trước khi thực hiện điều trị.
- Chi tiết hóa đơn: items[].referenceId đổi thành prescriptionId và planDetailId (nullable theo loại dòng).
- DTO lịch sử nội bộ: recordId/registrationId thay examSheetId; prescriptionId/prescriptionName thay formulaId/formulaName; planId/planCode/planName thay protocolId/protocolCode/protocolName. Chi tiết hồ sơ dùng GetMedicalRecordDetailAsync(recordId). Các phương thức lịch sử này hiện chưa có endpoint trong PatientsController.
- Đã gỡ các DTO/interface/service khám legacy không còn endpoint sử dụng. Luồng công khai /api/v1/examinations tiếp tục dùng registration ID.

```json
{
  "planId": 1,
  "assignedDoctorId": 2,
  "scheduledDate": "2026-11-02",
  "rowVersion": "AAAAAAAAAAE="
}
```

Dashboard dùng ngày Việt Nam (UTC+7), lọc BusinessDate/ScheduledDate. Ca hủy không tính vào tổng; buổi PLANNED chưa tiếp nhận không tính vào lượt điều trị. Hàng đợi khám dùng WAITING, điều trị dùng REQUESTED. Timestamp vẫn là UTC theo luồng mới. Số buổi lấy từ PlannedSessionCount, số hoàn thành đếm session COMPLETED. Lịch sử vẫn trả buổi chưa thực hiện, thời gian thực hiện có thể null.

## Kiểm tra

- node tests/check-database-schema.cjs: 39 bảng, 334 scalar column mappings khớp tên bảng/cột SQL, gồm convention/computed columns và nullability của kiểu giá trị. Kiểm tra tĩnh không thay thế kiểm tra EF hoặc constraint/kiểu trên SQL Server.
- powershell -NoProfile -ExecutionPolicy Bypass -File tests/ExaminationApi.ContractChecks/Run-Checks.ps1: 64 kiểm tra cú pháp/hợp đồng API khám thành công. Đã sửa đường dẫn RoleConstants cũ trong runner/project kiểm tra.
- Đã cập nhật unit tests về EF model/query translation, dashboard, lịch sử và gọi workflow; chưa chạy được các unit/integration test net10.
- dotnet build COSGeniusSystem/COSGeniusSystem.csproj --no-restore: NETSDK1045 vì máy chỉ có SDK 8/9, dự án yêu cầu .NET 10. Giữ nguyên target framework/packages.
- Chưa xác minh SQL Server đang chạy; không chạy database.sql/migration hoặc sửa dữ liệu SQL Server.
