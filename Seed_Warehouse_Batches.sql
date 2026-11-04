USE [ClinicHerbManagementDB];
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- 1. Thêm Kho mẫu nếu chưa có
IF NOT EXISTS (SELECT 1 FROM [Warehouse] WHERE WarehouseCode = 'KHO-DL01')
BEGIN
    INSERT INTO [Warehouse] (WarehouseCode, WarehouseName, Location, IsActive)
    VALUES 
        ('KHO-DL01', N'Kho Dược liệu trung tâm', N'Kệ DL-04 • Tầng 1 - Khu Dược', 1),
        ('KHO-TD02', N'Kho Tân dược & Vật tư', N'Tầng 2 - Phòng Dược lâm sàng', 1);
    PRINT 'Created default warehouses.';
END

DECLARE @whId INT = (SELECT TOP 1 WarehouseID FROM [Warehouse] WHERE WarehouseCode = 'KHO-DL01');

-- 2. Thêm các lô mẫu thực tế theo nguyên tắc FEFO nếu chưa có lô
IF NOT EXISTS (SELECT 1 FROM [Batch] WHERE BatchNumber = 'LO-CT-2401')
BEGIN
    -- Vị thuốc 1: Cam Thảo (HerbId = 1, MinThreshold = 50kg)
    -- Lô cũ 15kg (hạn 11/2025) + Lô mới 30kg (hạn 06/2026) = 45kg (Sắp đạt / dưới ngưỡng 50kg)
    INSERT INTO [Batch] (BatchNumber, HerbID, WarehouseID, ImportDate, ExpiryDate, OriginalQuantity, CurrentQuantity, UnitCost, Status)
    VALUES 
        ('LO-CT-2401', 1, @whId, '2024-09-10', '2025-11-10', 20.0, 15.0, 90000.0, N'Đạt chuẩn'),
        ('LO-CT-2501', 1, @whId, '2025-02-15', '2026-06-15', 30.0, 30.0, 95000.0, N'Đạt chuẩn');

    -- Vị thuốc 2: Kỉ Tử (HerbId = 2, MinThreshold = 50kg)
    -- Lô cũ 100kg (hạn 12/2025) + Lô mới 250kg (hạn 10/2026) = 350kg (An toàn)
    INSERT INTO [Batch] (BatchNumber, HerbID, WarehouseID, ImportDate, ExpiryDate, OriginalQuantity, CurrentQuantity, UnitCost, Status)
    VALUES 
        ('LO-KT-2402', 2, @whId, '2024-10-15', '2025-12-15', 120.0, 100.0, 85000.0, N'Đạt chuẩn'),
        ('LO-KT-2501', 2, @whId, '2025-01-20', '2026-10-20', 250.0, 250.0, 88000.0, N'Đạt chuẩn');

    -- Vị thuốc 3: Đương Quy (HerbId = 3, MinThreshold = 50kg)
    -- Lô 80kg (hạn 01/2026) = 80kg (An toàn)
    INSERT INTO [Batch] (BatchNumber, HerbID, WarehouseID, ImportDate, ExpiryDate, OriginalQuantity, CurrentQuantity, UnitCost, Status)
    VALUES 
        ('LO-DQ-2403', 3, @whId, '2024-11-01', '2026-01-15', 100.0, 80.0, 92000.0, N'Đạt chuẩn');

    -- Vị thuốc 4: Táo (HerbId = 4, MinThreshold = 50kg)
    -- Lô 25kg (hạn 10/2025) = 25kg (Dưới tối thiểu)
    INSERT INTO [Batch] (BatchNumber, HerbID, WarehouseID, ImportDate, ExpiryDate, OriginalQuantity, CurrentQuantity, UnitCost, Status)
    VALUES 
        ('LO-TAO-2401', 4, @whId, '2024-12-05', '2025-10-30', 50.0, 25.0, 8000.0, N'Đạt chuẩn');

    PRINT 'Seeded sample batches successfully.';
END
ELSE
BEGIN
    PRINT 'Batches already exist.';
END

SELECT b.BatchID, b.BatchNumber, h.CommonName, b.CurrentQuantity, b.ExpiryDate, w.WarehouseName, b.Status
FROM [Batch] b
JOIN [Herb] h ON b.HerbID = h.HerbID
JOIN [Warehouse] w ON b.WarehouseID = w.WarehouseID
ORDER BY h.CommonName, b.ExpiryDate;

