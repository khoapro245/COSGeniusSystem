USE [ClinicHerbManagementDB];
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- Clean existing seeded sample formulas if re-seeding
IF EXISTS (SELECT 1 FROM [HerbalFormula] WHERE FormulaName IN (N'Công thức mẫu A', N'Công thức mẫu B', N'Công thức mẫu C'))
BEGIN
    DELETE FROM [HerbalFormulaDetail] WHERE HerbalFormulaID IN (SELECT HerbalFormulaID FROM [HerbalFormula] WHERE FormulaName IN (N'Công thức mẫu A', N'Công thức mẫu B', N'Công thức mẫu C'));
    DELETE FROM [HerbalFormula] WHERE FormulaName IN (N'Công thức mẫu A', N'Công thức mẫu B', N'Công thức mẫu C');
END

DECLARE @docId INT = (SELECT TOP 1 DoctorId FROM [Doctor] WHERE IsActive = 1);
DECLARE @mgrUserId INT = (SELECT TOP 1 UserID FROM [User] WHERE Username = 'manager');
DECLARE @now DATETIME = GETDATE();

-- 1. Công thức mẫu A (v2, APPROVED)
INSERT INTO [HerbalFormula] (FormulaName, UsageInstructions, IntendedUse, Status, ProposedByDoctorId, SubmittedAt, ReviewedBy, ReviewedAt, ReviewNote, IsActive, CreatedAt)
VALUES (N'Công thức mẫu A', N'Sắc 1 thang chia làm 2 lần uống sau bữa ăn 30 phút.', N'Bổ khí huyết, kiện tỳ vị, an thần.', 'APPROVED', @docId, DATEADD(day, -30, @now), @mgrUserId, DATEADD(day, -29, @now), N'[VERSION:v2] Công thức chuẩn phòng khám', 1, DATEADD(day, -30, @now));

DECLARE @f1Id INT = SCOPE_IDENTITY();
INSERT INTO [HerbalFormulaDetail] (HerbalFormulaID, HerbId, DosagePerDose, RoleInFormula, Note)
VALUES 
    (@f1Id, 1, 8.0, N'Quân', N'Vị thuốc A - Cam Thảo'),
    (@f1Id, 2, 6.0, N'Thần', N'Vị thuốc B - Kỉ Tử'),
    (@f1Id, 3, 2.0, N'Tá', N'Vị thuốc C - Đương Quy');

-- 2. Công thức mẫu B (v1, APPROVED)
INSERT INTO [HerbalFormula] (FormulaName, UsageInstructions, IntendedUse, Status, ProposedByDoctorId, SubmittedAt, ReviewedBy, ReviewedAt, ReviewNote, IsActive, CreatedAt)
VALUES (N'Công thức mẫu B', N'Uống ấm sáng tối sau ăn.', N'Thanh nhiệt, giải độc, lương huyết.', 'APPROVED', @docId, DATEADD(day, -25, @now), @mgrUserId, DATEADD(day, -24, @now), N'[VERSION:v1] Đã duyệt dùng thử nghiệm', 1, DATEADD(day, -25, @now));

DECLARE @f2Id INT = SCOPE_IDENTITY();
INSERT INTO [HerbalFormulaDetail] (HerbalFormulaID, HerbId, DosagePerDose, RoleInFormula, Note)
VALUES 
    (@f2Id, 2, 10.0, N'Quân', N'Kỉ Tử'),
    (@f2Id, 4, 15.0, N'Thần', N'Táo');

-- 3. Yêu cầu sửa v2 -> v3 cho Công thức mẫu A (PENDING)
DECLARE @f1Code VARCHAR(20) = (SELECT FormulaCode FROM [HerbalFormula] WHERE HerbalFormulaID = @f1Id);
DECLARE @updateNote NVARCHAR(200) = N'[UPDATE:' + @f1Code + N':v2->v3] Bác sĩ Lê Minh đề xuất tăng liều Cam thảo lên 10g';

INSERT INTO [HerbalFormula] (FormulaName, UsageInstructions, IntendedUse, Status, ProposedByDoctorId, SubmittedAt, ReviewedBy, ReviewedAt, ReviewNote, IsActive, CreatedAt)
VALUES (N'Công thức mẫu A', N'Sắc 1 thang chia làm 2 lần uống sau ăn.', N'Bổ khí huyết, kiện tỳ vị (điều chỉnh liều lượng cam thảo).', 'PENDING', @docId, DATEADD(day, -2, @now), NULL, NULL, @updateNote, 1, DATEADD(day, -2, @now));

DECLARE @f3Id INT = SCOPE_IDENTITY();
INSERT INTO [HerbalFormulaDetail] (HerbalFormulaID, HerbId, DosagePerDose, RoleInFormula, Note)
VALUES 
    (@f3Id, 1, 10.0, N'Quân', N'Vị thuốc A - Cam Thảo (tăng 2g)'),
    (@f3Id, 2, 6.0, N'Thần', N'Vị thuốc B - Kỉ Tử'),
    (@f3Id, 3, 2.0, N'Tá', N'Vị thuốc C - Đương Quy');

-- 4. Yêu cầu tạo mới Công thức mẫu C (PENDING)
INSERT INTO [HerbalFormula] (FormulaName, UsageInstructions, IntendedUse, Status, ProposedByDoctorId, SubmittedAt, ReviewedBy, ReviewedAt, ReviewNote, IsActive, CreatedAt)
VALUES (N'Công thức mẫu C', N'Sắc uống ngày 1 thang.', N'Dưỡng huyết điều kinh.', 'PENDING', @docId, DATEADD(day, -1, @now), NULL, NULL, N'[VERSION:v1] Đề xuất công thức mới', 1, DATEADD(day, -1, @now));

DECLARE @f4Id INT = SCOPE_IDENTITY();
INSERT INTO [HerbalFormulaDetail] (HerbalFormulaID, HerbId, DosagePerDose, RoleInFormula, Note)
VALUES 
    (@f4Id, 3, 12.0, N'Quân', N'Đương Quy'),
    (@f4Id, 4, 10.0, N'Thần', N'Táo');

PRINT 'Seeded 4 formulas successfully with UTF-8 Vietnamese.';
