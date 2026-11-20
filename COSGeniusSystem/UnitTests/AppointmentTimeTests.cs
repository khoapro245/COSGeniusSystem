using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Services;

namespace UnitTests;

public class AppointmentTimeTests
{
    [Fact]
    public void MorningClosesAtNoonVietnamTime()
    {
        var date = new DateOnly(2026, 11, 16);
        AppointmentTime.ValidateMorning(date, "MORNING", new DateTime(2026, 11, 16, 4, 59, 59, DateTimeKind.Utc));
        Assert.Throws<BadRequestException>(() => AppointmentTime.ValidateMorning(date, "MORNING", new DateTime(2026, 11, 16, 5, 0, 0, DateTimeKind.Utc)));
        AppointmentTime.ValidateMorning(date, "AFTERNOON", new DateTime(2026, 11, 16, 5, 0, 0, DateTimeKind.Utc));
        AppointmentTime.ValidateMorning(date.AddDays(1), "MORNING", new DateTime(2026, 11, 16, 5, 0, 0, DateTimeKind.Utc));
    }
}
