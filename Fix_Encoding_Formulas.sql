USE [ClinicHerbManagementDB];
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- 1. Cập nhật lại tiếng Việt chuẩn cho HerbalFormula
UPDATE [HerbalFormula]
SET FormulaName = N'Công thức mẫu A',
    UsageInstructions = N'Sắc 1 thang chia làm 2 lần uống sau bữa ăn 30 phút.',
    IntendedUse = N'Bổ khí huyết, kiện tỳ vị, an thần.',
    ReviewNote = N'[VERSION:v2] Công thức chuẩn phòng khám'
WHERE HerbalFormulaID = 1;

UPDATE [HerbalFormula]
SET FormulaName = N'Công thức mẫu B',
    UsageInstructions = N'Uống ấm sáng tối sau ăn.',
    IntendedUse = N'Thanh nhiệt, giải độc, lương huyết.',
    ReviewNote = N'[VERSION:v1] Đã duyệt dùng thử nghiệm'
WHERE HerbalFormulaID = 2;

UPDATE [HerbalFormula]
SET FormulaName = N'Công thức mẫu A',
    UsageInstructions = N'Sắc 1 thang chia làm 2 lần uống sau ăn.',
    IntendedUse = N'Bổ khí huyết, kiện tỳ vị (điều chỉnh liều lượng cam thảo).',
    ReviewNote = N'[UPDATE:CT1:v2->v3] Bác sĩ Lê Minh đề xuất tăng liều Cam thảo lên 10g'
WHERE HerbalFormulaID = 3;

UPDATE [HerbalFormula]
SET FormulaName = N'Công thức mẫu C',
    UsageInstructions = N'Sắc uống ngày 1 thang.',
    IntendedUse = N'Dưỡng huyết điều kinh.',
    ReviewNote = N'[VERSION:v1] Đề xuất công thức mới'
WHERE HerbalFormulaID = 4;

-- 2. Cập nhật lại tiếng Việt chuẩn cho HerbalFormulaDetail
-- Công thức 1
UPDATE [HerbalFormulaDetail] SET RoleInFormula = N'Quân', Note = N'Vị thuốc A - Cam Thảo' WHERE HerbalFormulaID = 1 AND HerbId = 1;
UPDATE [HerbalFormulaDetail] SET RoleInFormula = N'Thần', Note = N'Vị thuốc B - Kỉ Tử' WHERE HerbalFormulaID = 1 AND HerbId = 2;
UPDATE [HerbalFormulaDetail] SET RoleInFormula = N'Tá', Note = N'Vị thuốc C - Đương Quy' WHERE HerbalFormulaID = 1 AND HerbId = 3;

-- Công thức 2
UPDATE [HerbalFormulaDetail] SET RoleInFormula = N'Quân', Note = N'Kỉ Tử' WHERE HerbalFormulaID = 2 AND HerbId = 2;
UPDATE [HerbalFormulaDetail] SET RoleInFormula = N'Thần', Note = N'Táo' WHERE HerbalFormulaID = 2 AND HerbId = 4;

-- Công thức 3
UPDATE [HerbalFormulaDetail] SET RoleInFormula = N'Quân', Note = N'Vị thuốc A - Cam Thảo (tăng 2g)' WHERE HerbalFormulaID = 3 AND HerbId = 1;
UPDATE [HerbalFormulaDetail] SET RoleInFormula = N'Thần', Note = N'Vị thuốc B - Kỉ Tử' WHERE HerbalFormulaID = 3 AND HerbId = 2;
UPDATE [HerbalFormulaDetail] SET RoleInFormula = N'Tá', Note = N'Vị thuốc C - Đương Quy' WHERE HerbalFormulaID = 3 AND HerbId = 3;

-- Công thức 4
UPDATE [HerbalFormulaDetail] SET RoleInFormula = N'Quân', Note = N'Đương Quy' WHERE HerbalFormulaID = 4 AND HerbId = 3;
UPDATE [HerbalFormulaDetail] SET RoleInFormula = N'Thần', Note = N'Táo' WHERE HerbalFormulaID = 4 AND HerbId = 4;

SELECT HerbalFormulaID, FormulaCode, FormulaName, ReviewNote FROM [HerbalFormula];

