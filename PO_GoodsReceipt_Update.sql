-- ====================================================================================
-- Script: PO_GoodsReceipt_Update.sql
-- Mục đích: Cập nhật schema CSDL cho module Phiếu Nhập Kho & Quản lý kệ kho
-- Tác giả: [hieppm]
-- Ngày tạo: 15/11/2026
-- ====================================================================================

USE [ClinicHerbManagementDB];
GO

-- 1. Cập nhật bảng WarehouseStorageLocation: Thêm Sức chứa (MaxCapacityGrams) và Loại kệ (StorageType)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[WarehouseStorageLocation]') AND name = 'MaxCapacityGrams')
BEGIN
    ALTER TABLE [dbo].[WarehouseStorageLocation]
    ADD [MaxCapacityGrams] [decimal](18, 3) NOT NULL CONSTRAINT [DF_WarehouseStorageLocation_MaxCapacityGrams] DEFAULT (100000.000);
    PRINT N'[hieppm] Đã thêm cột MaxCapacityGrams vào bảng WarehouseStorageLocation.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[WarehouseStorageLocation]') AND name = 'StorageType')
BEGIN
    ALTER TABLE [dbo].[WarehouseStorageLocation]
    ADD [StorageType] [varchar](20) NOT NULL CONSTRAINT [DF_WarehouseStorageLocation_StorageType] DEFAULT ('STANDARD');
    PRINT N'[hieppm] Đã thêm cột StorageType vào bảng WarehouseStorageLocation.';
END
GO

-- 2. Thêm dữ liệu mẫu kệ kho (STANDARD và COLD) nếu bảng chưa có dữ liệu hoặc sửa lại đúng Tiếng Việt có dấu
IF NOT EXISTS (SELECT 1 FROM [dbo].[WarehouseStorageLocation])
BEGIN
    INSERT INTO [dbo].[WarehouseStorageLocation] ([WarehouseId], [Code], [IsActive], [MaxCapacityGrams], [StorageType])
    VALUES
    (1, N'Kệ A - Ô 01 (Thường)', 1, 50000.000, 'STANDARD'),
    (1, N'Kệ A - Ô 02 (Thường)', 1, 50000.000, 'STANDARD'),
    (1, N'Kệ A - Ô 03 (Thường)', 1, 75000.000, 'STANDARD'),
    (1, N'Kệ B - Ô 01 (Thường)', 1, 100000.000, 'STANDARD'),
    (1, N'Kệ B - Ô 02 (Thường)', 1, 100000.000, 'STANDARD'),
    (1, N'Kệ Lạnh C - Ô 01 (Lạnh)', 1, 30000.000, 'COLD'),
    (1, N'Kệ Lạnh C - Ô 02 (Lạnh)', 1, 30000.000, 'COLD'),
    (1, N'Kệ Lạnh D - Ô 01 (Lạnh)', 1, 50000.000, 'COLD');
    PRINT N'[hieppm] Đã chèn dữ liệu mẫu cho bảng WarehouseStorageLocation.';
END
ELSE
BEGIN
    -- Cập nhật lại chuẩn Unicode Tiếng Việt cho các kệ mẫu
    UPDATE [dbo].[WarehouseStorageLocation] SET [Code] = N'Kệ A - Ô 01 (Thường)' WHERE [Id] = 1;
    UPDATE [dbo].[WarehouseStorageLocation] SET [Code] = N'Kệ A - Ô 02 (Thường)' WHERE [Id] = 2;
    UPDATE [dbo].[WarehouseStorageLocation] SET [Code] = N'Kệ A - Ô 03 (Thường)' WHERE [Id] = 3;
    UPDATE [dbo].[WarehouseStorageLocation] SET [Code] = N'Kệ B - Ô 01 (Thường)' WHERE [Id] = 4;
    UPDATE [dbo].[WarehouseStorageLocation] SET [Code] = N'Kệ B - Ô 02 (Thường)' WHERE [Id] = 5;
    UPDATE [dbo].[WarehouseStorageLocation] SET [Code] = N'Kệ Lạnh C - Ô 01 (Lạnh)' WHERE [Id] = 6;
    UPDATE [dbo].[WarehouseStorageLocation] SET [Code] = N'Kệ Lạnh C - Ô 02 (Lạnh)' WHERE [Id] = 7;
    UPDATE [dbo].[WarehouseStorageLocation] SET [Code] = N'Kệ Lạnh D - Ô 01 (Lạnh)' WHERE [Id] = 8;
    PRINT N'[hieppm] Đã đồng bộ chuẩn Unicode Tiếng Việt cho WarehouseStorageLocation.';
END
GO

-- 3. Cập nhật bảng GoodsReceipt: Thêm Tài xế, Biển số xe, File chứng từ
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GoodsReceipt]') AND name = 'DriverName')
BEGIN
    ALTER TABLE [dbo].[GoodsReceipt]
    ADD [DriverName] [nvarchar](100) NULL;
    PRINT N'[hieppm] Đã thêm cột DriverName vào bảng GoodsReceipt.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GoodsReceipt]') AND name = 'VehicleNumber')
BEGIN
    ALTER TABLE [dbo].[GoodsReceipt]
    ADD [VehicleNumber] [nvarchar](50) NULL;
    PRINT N'[hieppm] Đã thêm cột VehicleNumber vào bảng GoodsReceipt.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[GoodsReceipt]') AND name = 'DocumentUrl')
BEGIN
    ALTER TABLE [dbo].[GoodsReceipt]
    ADD [DocumentUrl] [nvarchar](500) NULL;
    PRINT N'[hieppm] Đã thêm cột DocumentUrl vào bảng GoodsReceipt.';
END
GO

-- 4. Cập nhật bảng Herb: Thêm cờ yêu cầu bảo quản lạnh (IsColdStorageRequired)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Herb]') AND name = 'IsColdStorageRequired')
BEGIN
    ALTER TABLE [dbo].[Herb]
    ADD [IsColdStorageRequired] [bit] NOT NULL CONSTRAINT [DF_Herb_IsColdStorageRequired] DEFAULT (0);
    PRINT N'[hieppm] Đã thêm cột IsColdStorageRequired vào bảng Herb.';
END
GO

-- Đánh dấu một số vị thuốc tươi/quý yêu cầu kệ lạnh để kiểm thử
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Herb]') AND name = 'IsColdStorageRequired')
BEGIN
    UPDATE [dbo].[Herb]
    SET [IsColdStorageRequired] = 1
    WHERE [HerbID] IN (7, 8); -- Sinh khương, Đại táo
    PRINT N'[hieppm] Đã cập nhật IsColdStorageRequired = 1 cho các vị thuốc mẫu cần bảo quản lạnh.';
END
GO

-- 5. Cho phép Batch.Location nullable để tương thích ngược khi chuyển sang dùng StorageLocationId
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Batch]') AND name = 'Location')
BEGIN
    ALTER TABLE [dbo].[Batch] ALTER COLUMN [Location] [nvarchar](255) NULL;
    PRINT N'[hieppm] Đã chuyển Batch.Location về NULLABLE.';
END
GO

-- 6. Cập nhật bảng Herb: Thêm trường Giá nhập gần nhất (LastImportPrice) theo yêu cầu Quản lý PO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Herb]') AND name = 'LastImportPrice')
BEGIN
    ALTER TABLE [dbo].[Herb]
    ADD [LastImportPrice] [decimal](18, 2) NULL;
    PRINT N'[hieppm] Đã thêm cột LastImportPrice vào bảng Herb.';
END
GO

-- Seed dữ liệu mẫu Giá nhập gần nhất (LastImportPrice) cho các vị thuốc
UPDATE [dbo].[Herb] SET [LastImportPrice] = 160.00 WHERE [HerbCode] = 'G79DEMO_H01' AND ([LastImportPrice] IS NULL OR [LastImportPrice] = 0);
UPDATE [dbo].[Herb] SET [LastImportPrice] = 170.00 WHERE [HerbCode] = 'G79DEMO_H02' AND ([LastImportPrice] IS NULL OR [LastImportPrice] = 0);
UPDATE [dbo].[Herb] SET [LastImportPrice] = 180.00 WHERE [HerbCode] = 'G79DEMO_H03' AND ([LastImportPrice] IS NULL OR [LastImportPrice] = 0);
UPDATE [dbo].[Herb] SET [LastImportPrice] = 190.00 WHERE [HerbCode] = 'G79DEMO_H04' AND ([LastImportPrice] IS NULL OR [LastImportPrice] = 0);
UPDATE [dbo].[Herb] SET [LastImportPrice] = 200.00 WHERE [HerbCode] = 'G79DEMO_H05' AND ([LastImportPrice] IS NULL OR [LastImportPrice] = 0);
UPDATE [dbo].[Herb] SET [LastImportPrice] = 210.00 WHERE [HerbCode] = 'G79DEMO_H06' AND ([LastImportPrice] IS NULL OR [LastImportPrice] = 0);
UPDATE [dbo].[Herb] SET [LastImportPrice] = 220.00 WHERE [HerbCode] = 'G79DEMO_H07' AND ([LastImportPrice] IS NULL OR [LastImportPrice] = 0);
UPDATE [dbo].[Herb] SET [LastImportPrice] = 230.00 WHERE [HerbCode] = 'G79DEMO_H08' AND ([LastImportPrice] IS NULL OR [LastImportPrice] = 0);
UPDATE [dbo].[Herb] SET [LastImportPrice] = 240.00 WHERE [HerbCode] = 'G79DEMO_H09' AND ([LastImportPrice] IS NULL OR [LastImportPrice] = 0);
UPDATE [dbo].[Herb] SET [LastImportPrice] = 250.00 WHERE [HerbCode] = 'G79DEMO_H10' AND ([LastImportPrice] IS NULL OR [LastImportPrice] = 0);
UPDATE [dbo].[Herb] SET [LastImportPrice] = 800.00 WHERE [HerbCode] = 'VT1326' AND ([LastImportPrice] IS NULL OR [LastImportPrice] = 0);
PRINT N'[hieppm] Đã cập nhật giá nhập gần nhất mẫu cho các vị thuốc trong Herb.';
GO

-- 7. Seed dữ liệu mẫu cho luồng Yêu cầu bổ sung kho (ImportRequest) ở trạng thái 'Chờ duyệt' để kiểm thử
IF NOT EXISTS (SELECT 1 FROM [dbo].[ImportRequest] WHERE [RequestCode] = 'YC00011')
BEGIN
    INSERT INTO [dbo].[ImportRequest] ([RequestCode], [RequestedBy], [RequestDate], [Status], [Reason])
    VALUES ('YC00011', 7, GETDATE(), N'Chờ duyệt', N'Đề xuất bổ sung dược liệu tiêu hao tháng 11/2026');

    DECLARE @NewReqID INT = SCOPE_IDENTITY();
    INSERT INTO [dbo].[ImportRequestDetail] ([RequestID], [HerbID], [RequestedQuantity], [Note])
    VALUES 
    (@NewReqID, 1, 10000.00, N'Bổ sung dự trữ kho'),
    (@NewReqID, 4, 5000.00, N'Nhu cầu bốc thuốc sắp tới');

    PRINT N'[hieppm] Đã thêm dữ liệu mẫu Yêu cầu bổ sung kho YC00011 (Chờ duyệt).';
END
GO

-- 8. Quy tắc tự động cập nhật số lượng và vị trí kho khi Manager duyệt phiếu nhập kho (Putaway):
-- Khi Manager duyệt phiếu nhập kho (GoodsReceipt.Status = 'Approved' / 'Đã duyệt'):
--   a) Bảng Batch: CurrentQuantity = CurrentQuantity + detail.Quantity 
--      (Cộng dồn số lượng nhập từ PO vào số lượng hiện có trong kho, tuyệt đối KHÔNG thay thế)
--   b) Bảng Batch: OriginalQuantity = OriginalQuantity + detail.Quantity, Status = N'Đạt chuẩn'
--   c) Bảng Batch: Location và StorageLocationId được gán chính xác theo vị trí kệ kho đã phân
--   d) Bảng ReceiptHistory: Tự động ghi nhận biến động tăng tồn:
--      - QuantityChanged = detail.Quantity (Số lượng nhập từ PO)
--      - BalanceAfter = (Số lượng tồn trước khi nhập của vị thuốc trong kho + detail.Quantity)
--   e) Bảng Herb: LastImportPrice được tự động cập nhật theo đơn giá nhập của lô thuốc vừa duyệt
--   f) Toàn hệ thống (Màn hình Tồn kho & Đề xuất bổ sung): 
--      Tổng số lượng tồn thực tế hiển thị = Tổng tồn cũ trong kho + Số lượng nhập từ PO
PRINT N'[hieppm] Đã thiết lập quy tắc cộng dồn tồn kho (Số lượng trong kho + Số lượng nhập từ PO, không thay thế) và cập nhật vị trí kho khi duyệt Putaway.';
GO

PRINT N'[hieppm] HOÀN TẤT CẬP NHẬT DATABASE CHO MODULE PO & GOODS RECEIPT!';
GO
