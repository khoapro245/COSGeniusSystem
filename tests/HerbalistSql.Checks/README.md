# Chạy kiểm tra Herbalist với SQL Server

`HerbalistSql.Checks` ghi dữ liệu, chỉ chấp nhận tên database bắt đầu `HerbalistIntegration_`. Không dùng connection string database nguồn.

1. Tạo database test riêng với kích thước data/log nhỏ, dựng bảng/constraint và fixture từ `database.sql` (bỏ CREATE DATABASE/USE/ALTER DATABASE trỏ tên nguồn).
2. Mang các trigger nghiệp vụ đang chạy vào bản test, vì snapshot repository không bao gồm tất cả trigger. Áp dụng `Herbalist_IntegrationGuards.sql` chỉ ở bản test.
3. Fixture cần ít nhất bốn đơn SENT, khám và đăng ký COMPLETED, chưa có MedicineDispense/yêu cầu đang hiệu lực. Fixture snapshot hiện có đáp ứng. Test tự gán vị trí lô, dùng một đơn lượng 0.001g để kiểm tra precision thực, rồi tiêu thụ bốn đơn này.
4. Đặt biến môi trường `HERBALIST_TEST_CONNECTION` bằng connection string database test, không ghi credential vào repo; chạy `dotnet run --project tests/HerbalistSql.Checks`.
5. Mỗi lần chạy cần fixture sạch riêng. Không chạy dọn dữ liệu test vào database nguồn. Test giữ dữ liệu để review; cleanup database test sau khi không còn cần.

Lần kiểm tra 10/11/2026: 35 checks qua trên SQL Server 16, database `HerbalistIntegration_20261110135325`, với 19 trigger từ instance local. Tests gọi controller trực tiếp, không kiểm tra HTTP/JWT middleware. Đã kiểm tra EF translation bằng truy vấn thực, rowversion, serializable transaction, race hai connection, trừ tồn một lần, NULL/0, chặn chênh lệch và trigger chống lách budget.

Nếu không restore được package trong môi trường offline, có thể compile cùng Program.cs qua harness dùng các binary vừa build của backend. SqlClient phải dùng runtime win thực và native SNI, không dùng reference stub; đây là cách đã chạy ở workspace này. Các project/harness phụ nằm trong `../../outputs/`. Không coi model checks hoặc frontend mocks là bằng chứng SQL.
