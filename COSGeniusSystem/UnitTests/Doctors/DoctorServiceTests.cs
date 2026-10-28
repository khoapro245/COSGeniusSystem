using COSGeniusSystem.Data;
using COSGeniusSystem.DTOs.Doctors;
using COSGeniusSystem.Models;
using COSGeniusSystem.Services.Doctors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COSGeniusSystem.UnitTests.Doctors;

public class DoctorServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"ClinicHerbDb_Doctor_{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    #region UT-PHUC-01: Test lọc danh sách bác sĩ

    [Fact]
    public async Task GetDoctorsAsync_FilterByIsActive_ReturnsOnlyActiveDoctors()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        context.Doctors.AddRange(
            new Doctor { DoctorId = 1, FullName = "Bác sĩ Nguyễn Văn A", IsActive = true, Specialization = "Châm cứu" },
            new Doctor { DoctorId = 2, FullName = "Bác sĩ Trần Thị B", IsActive = true, Specialization = "Xoa bóp" },
            new Doctor { DoctorId = 3, FullName = "Bác sĩ Lê Văn C (Đã nghỉ)", IsActive = false, Specialization = "Châm cứu" }
        );
        await context.SaveChangesAsync();

        var service = new DoctorService(context);
        var query = new DoctorFilterQuery { IsActive = true };

        // Act
        var result = await service.GetDoctorsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, d => Assert.True(d.IsActive));
        Assert.DoesNotContain(result, d => d.DoctorId == 3);
    }

    [Fact]
    public async Task GetDoctorsAsync_FilterBySpecialization_ReturnsMatchingSpecialization()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        context.Doctors.AddRange(
            new Doctor { DoctorId = 1, FullName = "Bác sĩ A", Specialization = "Châm cứu", IsActive = true },
            new Doctor { DoctorId = 2, FullName = "Bác sĩ B", Specialization = "Xoa bóp", IsActive = true },
            new Doctor { DoctorId = 3, FullName = "Bác sĩ C", Specialization = "Châm cứu trị liệu", IsActive = true }
        );
        await context.SaveChangesAsync();

        var service = new DoctorService(context);
        var query = new DoctorFilterQuery { Specialization = "Châm cứu", IsActive = true };

        // Act
        var result = await service.GetDoctorsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, d => Assert.Contains("Châm cứu", d.Specialization, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result, d => d.DoctorId == 2);
    }

    #endregion

    #region UT-PHUC-02: Test chi tiết bác sĩ

    [Fact]
    public async Task GetDoctorByIdAsync_NonExistingId_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        context.Doctors.Add(new Doctor { DoctorId = 1, FullName = "Bác sĩ A", IsActive = true });
        await context.SaveChangesAsync();

        var service = new DoctorService(context);

        // Act: Gọi id không tồn tại
        var result = await service.GetDoctorByIdAsync(99999);

        // Assert: Trả về null để Controller trả về 404
        Assert.Null(result);
    }

    [Fact]
    public async Task GetDoctorByIdAsync_ExistingIdWithLinkedUser_ReturnsMappedDoctorDetailAndUser()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();

        var linkedUser = new User
        {
            UserId = 10,
            Username = "dr.nguyen",
            Email = "dr.nguyen@clinicherb.vn",
            FullName = "Nguyễn Văn Tuấn",
            PasswordHash = "hash123",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var doctor = new Doctor
        {
            DoctorId = 1,
            UserId = linkedUser.UserId,
            FullName = "Bs. CKI Nguyễn Văn Tuấn",
            Specialization = "Y học cổ truyền",
            LicenseNumber = "CCHN-00123",
            Phone = "0987654321",
            IsActive = true,
            User = linkedUser
        };

        context.Users.Add(linkedUser);
        context.Doctors.Add(doctor);
        await context.SaveChangesAsync();

        var service = new DoctorService(context);

        // Act
        var result = await service.GetDoctorByIdAsync(doctor.DoctorId);

        // Assert: Map đúng thông tin bác sĩ và thông tin tài khoản user
        Assert.NotNull(result);
        Assert.Equal(doctor.DoctorId, result.DoctorId);
        Assert.Equal(doctor.FullName, result.FullName);
        Assert.Equal(doctor.Specialization, result.Specialization);
        Assert.Equal(doctor.LicenseNumber, result.LicenseNumber);
        Assert.Equal(doctor.Phone, result.Phone);
        Assert.True(result.IsActive);

        // Thông tin User liên kết
        Assert.Equal("dr.nguyen", result.Username);
        Assert.Equal("dr.nguyen@clinicherb.vn", result.Email);
    }

    #endregion
}
