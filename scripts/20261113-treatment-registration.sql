-- Chạy trong đúng database phòng khám, trước khi triển khai backend mới.
-- Dừng các tiến trình ghi trong lúc áp dụng. Không xóa hoặc đánh lại số phiếu cũ.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.TreatmentSession', 'Shift') IS NULL
    ALTER TABLE dbo.TreatmentSession ADD Shift varchar(10) NULL;
IF COL_LENGTH('dbo.TreatmentSession', 'RequestCode') IS NULL
    ALTER TABLE dbo.TreatmentSession ADD RequestCode varchar(40) NULL;
IF COL_LENGTH('dbo.TreatmentSession', 'QueueNumber') IS NULL
    ALTER TABLE dbo.TreatmentSession ADD QueueNumber int NULL;
GO
-- Chỉ chuyển buổi có dấu hiệu rõ ràng từ định dạng ghi chú cũ; không suy đoán theo giờ.
UPDATE dbo.TreatmentSession SET Shift = CASE
    WHEN ResultNote = N'Buổi điều trị: Sáng' OR ResultNote LIKE N'Buổi điều trị: Sáng' + CHAR(10) + N'%' OR ResultNote LIKE N'Buổi điều trị: Sáng' + CHAR(13) + CHAR(10) + N'%' THEN 'MORNING'
    WHEN ResultNote = N'Buổi điều trị: Chiều' OR ResultNote LIKE N'Buổi điều trị: Chiều' + CHAR(10) + N'%' OR ResultNote LIKE N'Buổi điều trị: Chiều' + CHAR(13) + CHAR(10) + N'%' THEN 'AFTERNOON'
    ELSE NULL END
WHERE Shift IS NULL;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TreatmentShift')
    ALTER TABLE dbo.TreatmentSession ADD CONSTRAINT CK_TreatmentShift CHECK (Shift IS NULL OR Shift IN ('MORNING', 'AFTERNOON'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TreatmentQueue')
    ALTER TABLE dbo.TreatmentSession ADD CONSTRAINT CK_TreatmentQueue CHECK (QueueNumber IS NULL OR (QueueNumber > 0 AND ScheduledDate IS NOT NULL AND RequestCode IS NOT NULL AND Shift IS NOT NULL));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.TreatmentSession') AND name = 'UX_TreatmentRequestCode')
    CREATE UNIQUE INDEX UX_TreatmentRequestCode ON dbo.TreatmentSession(RequestCode) WHERE RequestCode IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.TreatmentSession') AND name = 'UX_TreatmentQueue')
    CREATE UNIQUE INDEX UX_TreatmentQueue ON dbo.TreatmentSession(ScheduledDate, QueueNumber) WHERE QueueNumber IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.TreatmentSession') AND name = 'IX_TreatmentWorkload')
    CREATE INDEX IX_TreatmentWorkload ON dbo.TreatmentSession(ScheduledDate, Shift, AssignedDoctorID, Status);
COMMIT TRANSACTION;
GO
