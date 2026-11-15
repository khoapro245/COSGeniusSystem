using COSGeniusSystem.Commons;
using COSGeniusSystem.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Middlewares;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        int code;
        string message;
        if (exception is ApiException api)
        {
            code = api.StatusCode;
            message = api.Message;
            if (code >= 500) logger.LogError(exception, "API failure for {Path}", context.Request.Path);
        }
        else if (exception is SqlException { Number: >= 51030 and <= 51034 }
            || exception is DbUpdateException { InnerException: SqlException { Number: >= 51030 and <= 51034 } }
            || exception is DbUpdateConcurrencyException
            || exception is SqlException { Number: 1205 or 1222 }
            || exception is DbUpdateException { InnerException: SqlException { Number: 1205 or 1222 } })
        {
            logger.LogWarning(exception, "Concurrent update for {Path}", context.Request.Path);
            code = 409;
            message = "The data has changed or is being processed. Please reload before continuing.";
        }
        else if (exception is DbUpdateException { InnerException: SqlException { Number: 547 or 2601 or 2627 } })
        {
            logger.LogWarning(exception, "Database conflict for {Path}", context.Request.Path);
            code = 409;
            message = "The operation conflicts with related or existing data.";
        }
        else if (exception is System.Text.Json.JsonException || exception is BadHttpRequestException || exception is FormatException)
        {
            logger.LogWarning(exception, "Malformed payload or type mismatch for {Path}", context.Request.Path);
            code = 400;
            message = "Invalid request format. Numeric fields accept only numbers, and text fields accept only strings.";
        }
        else
        {
            logger.LogError(exception, "Unhandled failure for {Path}", context.Request.Path);
            code = SystemMessages.InternalError.Code;
            message = SystemMessages.InternalError.Message;
        }
        context.Response.StatusCode = code;
        await context.Response.WriteAsJsonAsync(new ApiResponseModel<object?>(code, message, false, exception is DuplicatePatientPhoneException duplicate ? duplicate.Warning : null), ct);
        return true;
    }
}
