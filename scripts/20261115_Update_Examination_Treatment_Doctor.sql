-- =========================================================================================
-- Script cập nhật CSDL: Bổ sung ca khám (Session), thông tin buổi điều trị & đồng bộ Bác sĩ
-- Áp dụng cho: CSDL ClinicHerbManagementDB
-- Mục đích: Khắc phục lỗi thiếu cột 'Session' (ExaminationRegistration), 
--          các cột ca/số thứ tự điều trị (TreatmentSession) và đồng bộ tài khoản Bác sĩ (Doctor).
-- =========================================================================================

USE [ClinicHerbManagementDB];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO

PRINT N'--- BẮT ĐẦU CẬP NHẬT DATABASE ---';
GO

-- -----------------------------------------------------------------------------------------
-- 1. BẢNG [dbo].[ExaminationRegistration]: Bổ sung cột [Session]
-- -----------------------------------------------------------------------------------------
IF COL_LENGTH('dbo.ExaminationRegistration', 'Session') IS NULL
BEGIN
    ALTER TABLE dbo.ExaminationRegistration 
    ADD [Session] varchar(10) NOT NULL CONSTRAINT DF_RegSession DEFAULT ('MORNING');
    PRINT N'[OK] Đã thêm cột Session (DEFAULT MORNING) vào bảng ExaminationRegistration.';
END
ELSE
BEGIN
    PRINT N'[INFO] Cột Session đã tồn tại trong bảng ExaminationRegistration.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_RegSession')
BEGIN
    ALTER TABLE dbo.ExaminationRegistration 
    WITH CHECK ADD CONSTRAINT CK_RegSession CHECK ([Session] IN ('UNKNOWN', 'AFTERNOON', 'MORNING'));
    PRINT N'[OK] Đã tạo ràng buộc CK_RegSession cho bảng ExaminationRegistration.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ExaminationRegistration') AND name = 'IX_RegSessionWorkload')
BEGIN
    CREATE INDEX IX_RegSessionWorkload 
    ON dbo.ExaminationRegistration(BusinessDate, [Session], DoctorID, Status);
    PRINT N'[OK] Đã tạo index IX_RegSessionWorkload trên ExaminationRegistration.';
END
GO

-- -----------------------------------------------------------------------------------------
-- 2. BẢNG [dbo].[TreatmentSession]: Bổ sung các cột Shift, RequestCode, QueueNumber
-- -----------------------------------------------------------------------------------------
IF COL_LENGTH('dbo.TreatmentSession', 'Shift') IS NULL
BEGIN
    ALTER TABLE dbo.TreatmentSession ADD Shift varchar(10) NULL;
    PRINT N'[OK] Đã thêm cột Shift vào bảng TreatmentSession.';
END
GO

IF COL_LENGTH('dbo.TreatmentSession', 'RequestCode') IS NULL
BEGIN
    ALTER TABLE dbo.TreatmentSession ADD RequestCode varchar(40) NULL;
    PRINT N'[OK] Đã thêm cột RequestCode vào bảng TreatmentSession.';
END
GO

IF COL_LENGTH('dbo.TreatmentSession', 'QueueNumber') IS NULL
BEGIN
    ALTER TABLE dbo.TreatmentSession ADD QueueNumber int NULL;
    PRINT N'[OK] Đã thêm cột QueueNumber vào bảng TreatmentSession.';
END
GO

-- Cập nhật dữ liệu cũ nếu có ghi chú ca khám
UPDATE dbo.TreatmentSession 
SET Shift = CASE
    WHEN ResultNote = N'Buổi điều trị: Sáng' OR ResultNote LIKE N'Buổi điều trị: Sáng' + CHAR(10) + N'%' OR ResultNote LIKE N'Buổi điều trị: Sáng' + CHAR(13) + CHAR(10) + N'%' THEN 'MORNING'
    WHEN ResultNote = N'Buổi điều trị: Chiều' OR ResultNote LIKE N'Buổi điều trị: Chiều' + CHAR(10) + N'%' OR ResultNote LIKE N'Buổi điều trị: Chiều' + CHAR(13) + CHAR(10) + N'%' THEN 'AFTERNOON'
    ELSE NULL END
WHERE Shift IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TreatmentShift')
BEGIN
    ALTER TABLE dbo.TreatmentSession ADD CONSTRAINT CK_TreatmentShift CHECK (Shift IS NULL OR Shift IN ('MORNING', 'AFTERNOON'));
    PRINT N'[OK] Đã tạo ràng buộc CK_TreatmentShift.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TreatmentQueue')
BEGIN
    ALTER TABLE dbo.TreatmentSession ADD CONSTRAINT CK_TreatmentQueue CHECK (QueueNumber IS NULL OR (QueueNumber > 0 AND ScheduledDate IS NOT NULL AND RequestCode IS NOT NULL AND Shift IS NOT NULL));
    PRINT N'[OK] Đã tạo ràng buộc CK_TreatmentQueue.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.TreatmentSession') AND name = 'UX_TreatmentRequestCode')
BEGIN
    CREATE UNIQUE INDEX UX_TreatmentRequestCode ON dbo.TreatmentSession(RequestCode) WHERE RequestCode IS NOT NULL;
    PRINT N'[OK] Đã tạo index UX_TreatmentRequestCode.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.TreatmentSession') AND name = 'UX_TreatmentQueue')
BEGIN
    CREATE UNIQUE INDEX UX_TreatmentQueue ON dbo.TreatmentSession(ScheduledDate, QueueNumber) WHERE QueueNumber IS NOT NULL;
    PRINT N'[OK] Đã tạo index UX_TreatmentQueue.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.TreatmentSession') AND name = 'IX_TreatmentWorkload')
BEGIN
    CREATE INDEX IX_TreatmentWorkload ON dbo.TreatmentSession(ScheduledDate, Shift, AssignedDoctorID, Status);
    PRINT N'[OK] Đã tạo index IX_TreatmentWorkload.';
END
GO

-- -----------------------------------------------------------------------------------------
-- 3. BẢNG [dbo].[Doctor]: Đồng bộ hồ sơ Bác sĩ cho các tài khoản có Role Doctor
-- -----------------------------------------------------------------------------------------
INSERT INTO dbo.Doctor (UserID, FullName, Specialization, IsActive)
SELECT 
    u.UserID, 
    u.FullName, 
    N'Bác sĩ Y học cổ truyền', 
    1
FROM dbo.[User] u
INNER JOIN dbo.UserRole ur ON u.UserID = ur.UserID
INNER JOIN dbo.Role r ON ur.RoleID = r.RoleID
WHERE r.RoleName = 'Doctor'
  AND NOT EXISTS (SELECT 1 FROM dbo.Doctor d WHERE d.UserID = u.UserID);

PRINT N'[OK] Đã kiểm tra và đồng bộ danh sách Bác sĩ từ bảng User.';
GO

PRINT N'--- HOÀN TẤT CẬP NHẬT DATABASE THÀNH CÔNG ---';
GO
