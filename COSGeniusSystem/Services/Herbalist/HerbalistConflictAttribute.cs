using COSGeniusSystem.Commons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace COSGeniusSystem.Services.Herbalist;

// Serializable transactions may select a deadlock victim. Return a retryable
// business conflict rather than exposing SQL or reporting a successful write.
public sealed class HerbalistConflictAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var ex = context.Exception;
        var sql = ex.GetBaseException() as SqlException;
        if (ex is not DbUpdateConcurrencyException && sql?.Number is not (1205 or 2601 or 2627)) return;
        context.Result = new ObjectResult(new ApiResponseModel<object>(409,
            "Dữ liệu đã thay đổi do thao tác đồng thời. Vui lòng tải lại và thử lại.", false, null)) { StatusCode = 409 };
        context.ExceptionHandled = true;
    }
}
