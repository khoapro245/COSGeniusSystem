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
        else if (exception is DbUpdateConcurrencyException
            || exception is SqlException { Number: 1205 or 1222 }
            || exception is DbUpdateException { InnerException: SqlException { Number: 1205 or 1222 } })
        {
            logger.LogWarning(exception, "Concurrent update for {Path}", context.Request.Path);
            code = 409;
            message = "Data changed or is being updated. Reload and retry with the latest RowVersion.";
        }
        else if (exception is DbUpdateException { InnerException: SqlException { Number: 547 or 2601 or 2627 } })
        {
            logger.LogWarning(exception, "Database conflict for {Path}", context.Request.Path);
            code = 409;
            message = "The operation conflicts with related or existing records.";
        }
        else
        {
            logger.LogError(exception, "Unhandled failure for {Path}", context.Request.Path);
            code = SystemMessages.InternalError.Code;
            message = SystemMessages.InternalError.Message;
        }
        context.Response.StatusCode = code;
        await context.Response.WriteAsJsonAsync(new ApiResponseModel<object?>(code, message, false, null), ct);
        return true;
    }
}
