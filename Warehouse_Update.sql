-- Script bổ sung cột cho phân hệ Nhập kho và Quản lý Lô dược liệu
-- 1. Bổ sung cột Status cho bảng GoodsReceipt
-- 2. Bổ sung cột Location và SupplierBatchCode cho bảng Batch

USE [ClinicHerbManagementDB];
GO

-- 1. Bổ sung cột Status vào bảng GoodsReceipt
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.GoodsReceipt') AND name = 'Status')
BEGIN
    ALTER TABLE dbo.GoodsReceipt
    ADD Status NVARCHAR(50) NOT NULL CONSTRAINT DF_GoodsReceipt_Status DEFAULT ('PENDING_APPROVAL');
    PRINT N'Đã thêm cột Status vào bảng GoodsReceipt.';
END
GO

-- 2. Bổ sung cột Location và SupplierBatchCode vào bảng Batch
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Batch') AND name = 'Location')
BEGIN
    ALTER TABLE dbo.Batch
    ADD Location NVARCHAR(100) NULL;
    PRINT N'Đã thêm cột Location vào bảng Batch.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Batch') AND name = 'SupplierBatchCode')
BEGIN
    ALTER TABLE dbo.Batch
    ADD SupplierBatchCode NVARCHAR(50) NULL;
    PRINT N'Đã thêm cột SupplierBatchCode vào bảng Batch.';
END
GO

