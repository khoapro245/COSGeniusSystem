using Microsoft.AspNetCore.Http;
using COSGeniusSystem.Commons;
using Microsoft.AspNetCore.Mvc;

namespace COSGeniusSystem.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected ActionResult<ApiResponseModel<T>> SuccessResponse<T>(SystemMessage message, T data) =>
        StatusCode(message.Code, ApiResponseModel<T>.Success(message, data));

    protected ActionResult<ApiResponseModel<object?>> SuccessResponse(SystemMessage message) =>
        SuccessResponse<object?>(message, null);

    protected ActionResult<ApiResponseModel<T>> CreatedResponse<T>(
        string actionName, object routeValues, SystemMessage message, T data)
    {
        if (message.Code != StatusCodes.Status201Created)
            throw new ArgumentException("A creation response must use HTTP status code 201.", nameof(message));
        try
        {
            return CreatedAtAction(actionName, routeValues, ApiResponseModel<T>.Success(message, data));
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status201Created, ApiResponseModel<T>.Success(message, data));
        }
    }
}
