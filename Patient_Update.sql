-- Bổ sung thông tin bệnh nhân và tạo bảng người liên hệ.
-- Chạy một lần trên database đã có bảng dbo.Patient.
-- Không tạo lại database hoặc bảng Patient; không xóa dữ liệu.

USE [ClinicHerbManagementDB];
GO

-- 1. Bổ sung thông tin bệnh nhân
ALTER TABLE dbo.Patient
ADD
    -- Mã tự sinh từ PatientID, ví dụ: BN1, BN2...
    PatientCode AS (
        'BN' + CONVERT(VARCHAR(20), PatientID)
    ) PERSISTED,

    Email VARCHAR(100) NULL,

    IdentityCardIssuedDate DATE NULL,
    IdentityCardIssuedPlace NVARCHAR(255) NULL,

    -- Lưu đường dẫn ảnh được tải lên hoặc chụp
    PhotoUrl NVARCHAR(500) NULL,

    Province NVARCHAR(100) NULL,
    Ward NVARCHAR(100) NULL,

    Occupation NVARCHAR(150) NULL,
    Workplace NVARCHAR(255) NULL,
    Nationality NVARCHAR(100) NULL,
    Ethnicity NVARCHAR(100) NULL;
GO

-- 2. Tạo bảng người nhà / người liên hệ
-- Một bệnh nhân có thể có nhiều người liên hệ
CREATE TABLE dbo.PatientContact (
    ContactID INT IDENTITY(1,1) PRIMARY KEY,
    PatientID INT NOT NULL,

    FullName NVARCHAR(150) NOT NULL,
    Relationship NVARCHAR(50) NULL,
    Phone VARCHAR(20) NULL,

    CONSTRAINT FK_PatientContact_Patient
        FOREIGN KEY (PatientID)
        REFERENCES dbo.Patient(PatientID)
);
GO

CREATE INDEX IX_PatientContact_PatientID
ON dbo.PatientContact(PatientID);
GO
