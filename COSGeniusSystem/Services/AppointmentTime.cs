using COSGeniusSystem.Exceptions;

namespace COSGeniusSystem.Services;

public static class AppointmentTime
{
    public static void ValidateMorning(DateOnly date, string? shift, DateTime? utcNow = null)
    {
        var local = (utcNow ?? DateTime.UtcNow).AddHours(7);
        if (date == DateOnly.FromDateTime(local) && local.Hour >= 12
            && string.Equals(shift?.Trim(), "MORNING", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Từ 12:00 không thể đăng ký buổi sáng cho hôm nay. Vui lòng chọn buổi chiều.");
    }
}
