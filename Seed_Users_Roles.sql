-- =====================================================================
-- Script bổ sung 4 tài khoản còn lại để đủ 7 Roles và 7 Users trong ClinicHerbManagementDB
-- 3 tài khoản đã có sẵn:
--   1. reception   (Role: RECEPTION_STAFF)
--   2. doctor      (Role: DOCTOR)
--   3. admin       (Role: ADMIN, RECEPTION_STAFF, DOCTOR)
-- 4 tài khoản bổ sung:
--   4. manager     / manager123     (Role: MANAGER)
--   5. pharmacist  / pharmacist123  (Role: PHARMACIST)
--   6. cashier     / cashier123     (Role: CASHIER)
--   7. inventory   / inventory123   (Role: INVENTORY_STAFF)
-- =====================================================================

USE [ClinicHerbManagementDB];
GO

-- 1. Bổ sung các Roles còn lại nếu chưa có
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleCode = 'MANAGER')
    INSERT INTO [Role] (RoleCode, RoleName, Description) VALUES ('MANAGER', 'MANAGER', 'Role for MANAGER');
GO

IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleCode = 'PHARMACIST')
    INSERT INTO [Role] (RoleCode, RoleName, Description) VALUES ('PHARMACIST', 'PHARMACIST', 'Role for PHARMACIST');
GO

IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleCode = 'CASHIER')
    INSERT INTO [Role] (RoleCode, RoleName, Description) VALUES ('CASHIER', 'CASHIER', 'Role for CASHIER');
GO

IF NOT EXISTS (SELECT 1 FROM [Role] WHERE RoleCode = 'INVENTORY_STAFF')
    INSERT INTO [Role] (RoleCode, RoleName, Description) VALUES ('INVENTORY_STAFF', 'INVENTORY_STAFF', 'Role for INVENTORY_STAFF');
GO

-- 2. Bổ sung 4 Users mới (Password được mã hóa bằng chuẩn BCrypt Enhanced của IPasswordHasher)
-- 4. manager / manager123
IF NOT EXISTS (SELECT 1 FROM [User] WHERE Username = 'manager')
    INSERT INTO [User] (Username, PasswordHash, FullName, Email, Phone, IsActive, CreatedAt)
    VALUES ('manager', '$2a$12$8yXBzZFbJadGKVNH4AhXK.W08ynL.EZWsR2OsWMR/E8HCHC9aldtq', N'Quan Ly', NULL, NULL, 1, GETDATE());
GO

-- 5. pharmacist / pharmacist123
IF NOT EXISTS (SELECT 1 FROM [User] WHERE Username = 'pharmacist')
    INSERT INTO [User] (Username, PasswordHash, FullName, Email, Phone, IsActive, CreatedAt)
    VALUES ('pharmacist', '$2a$12$lXT7fB8JEta9waPKYQcNL.3L7NhqqYyD5Xx/TWbR5A.qNlkJpuYXK', N'Duoc Si', NULL, NULL, 1, GETDATE());
GO

-- 6. cashier / cashier123
IF NOT EXISTS (SELECT 1 FROM [User] WHERE Username = 'cashier')
    INSERT INTO [User] (Username, PasswordHash, FullName, Email, Phone, IsActive, CreatedAt)
    VALUES ('cashier', '$2a$12$2vTSh9SoCvzLWr2FQmgtxePbvbrtMIRCLmSaOM4XpTs6ZgKCIxwPO', N'Thu Ngan', NULL, NULL, 1, GETDATE());
GO

-- 7. inventory / inventory123
IF NOT EXISTS (SELECT 1 FROM [User] WHERE Username = 'inventory')
    INSERT INTO [User] (Username, PasswordHash, FullName, Email, Phone, IsActive, CreatedAt)
    VALUES ('inventory', '$2a$12$i1pGhtMhVk/S7kPWAR2OO.Rc36SA5ohaYuxCzgH8Rh0Bc21MfnPTa', N'Nhan Vien Kho', NULL, NULL, 1, GETDATE());
GO

-- 3. Gán Role tương ứng cho 4 tài khoản mới vào [UserRole]
DECLARE @mgrUserId INT = (SELECT UserID FROM [User] WHERE Username = 'manager');
DECLARE @mgrRoleId INT = (SELECT RoleID FROM [Role] WHERE RoleCode = 'MANAGER');
IF @mgrUserId IS NOT NULL AND @mgrRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [UserRole] WHERE UserID = @mgrUserId AND RoleID = @mgrRoleId)
    INSERT INTO [UserRole] (UserID, RoleID, AssignedAt) VALUES (@mgrUserId, @mgrRoleId, GETDATE());

DECLARE @phaUserId INT = (SELECT UserID FROM [User] WHERE Username = 'pharmacist');
DECLARE @phaRoleId INT = (SELECT RoleID FROM [Role] WHERE RoleCode = 'PHARMACIST');
IF @phaUserId IS NOT NULL AND @phaRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [UserRole] WHERE UserID = @phaUserId AND RoleID = @phaRoleId)
    INSERT INTO [UserRole] (UserID, RoleID, AssignedAt) VALUES (@phaUserId, @phaRoleId, GETDATE());

DECLARE @casUserId INT = (SELECT UserID FROM [User] WHERE Username = 'cashier');
DECLARE @casRoleId INT = (SELECT RoleID FROM [Role] WHERE RoleCode = 'CASHIER');
IF @casUserId IS NOT NULL AND @casRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [UserRole] WHERE UserID = @casUserId AND RoleID = @casRoleId)
    INSERT INTO [UserRole] (UserID, RoleID, AssignedAt) VALUES (@casUserId, @casRoleId, GETDATE());

DECLARE @invUserId INT = (SELECT UserID FROM [User] WHERE Username = 'inventory');
DECLARE @invRoleId INT = (SELECT RoleID FROM [Role] WHERE RoleCode = 'INVENTORY_STAFF');
IF @invUserId IS NOT NULL AND @invRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [UserRole] WHERE UserID = @invUserId AND RoleID = @invRoleId)
    INSERT INTO [UserRole] (UserID, RoleID, AssignedAt) VALUES (@invUserId, @invRoleId, GETDATE());
GO
