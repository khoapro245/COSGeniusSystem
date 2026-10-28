using System.Text.Json;
using COSGeniusSystem.Commons;
using COSGeniusSystem.Controllers;
using COSGeniusSystem.DTOs.Auth;
using COSGeniusSystem.DTOs.Doctors;
using COSGeniusSystem.Exceptions;
using COSGeniusSystem.Middlewares;
using COSGeniusSystem.Services;
using COSGeniusSystem.Services.Doctors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace COSGeniusSystem.UnitTests;

public class ApiResponseTests
{
    [Fact]
    public async Task Login_ReturnsPatientEnvelope()
    {
        var service = new Mock<IAuthService>();
        var request = new LoginRequestDto();
        var data = new AuthResponseDto();
        service.Setup(s => s.LoginAsync(request)).ReturnsAsync(data);

        var result = Assert.IsAssignableFrom<ObjectResult>((await new AuthController(service.Object).Login(request)).Result);
        var body = Assert.IsType<ApiResponseModel<AuthResponseDto>>(result.Value);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(result.StatusCode, body.Code);
        Assert.True(body.IsSuccess);
        Assert.Same(data, body.Data);
    }

    [Fact]
    public async Task MissingDoctor_UsesSharedNotFoundException()
    {
        var service = new Mock<IDoctorService>();
        service.Setup(s => s.GetDoctorByIdAsync(99)).ReturnsAsync((DoctorDetailDto?)null);
        var error = await Assert.ThrowsAsync<NotFoundException>(() => new DoctorsController(service.Object).GetDoctorById(99));
        Assert.Equal(404, error.StatusCode);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(500)]
    public async Task ExceptionHandler_MatchesHttpStatusAndHidesUnexpectedDetails(int code)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        Exception error = code == 500 ? new Exception("private database details") : new ApiException(code, "Business error");
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
        Assert.True(await handler.TryHandleAsync(context, error, CancellationToken.None));
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(code, context.Response.StatusCode);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetInt32());
        Assert.False(json.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").ValueKind);
        Assert.DoesNotContain("private database details", json.RootElement.GetRawText());
    }
}
