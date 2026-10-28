using System.Net;
using System.Text.Json;
using COSGeniusSystem.Common;
using COSGeniusSystem.Exceptions;

namespace COSGeniusSystem.Middlewares;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            _logger.LogWarning(ex, "Business rule exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex.StatusCode, ex.Message, ex.Errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(
                context,
                HttpStatusCode.InternalServerError,
                "An unexpected server error occurred. Please try again later.",
                new List<string> { ex.Message });
        }
    }

    private static Task HandleExceptionAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string message,
        List<string>? errors = null)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse<object>.Fail(message, errors);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(response, jsonOptions);
        return context.Response.WriteAsync(json);
    }
}
