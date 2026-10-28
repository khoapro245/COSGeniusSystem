using COSGeniusSystem.Commons;

namespace COSGeniusSystem.Exceptions;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public int Code => StatusCode;
    public bool IsSuccess => false;

    public ApiException(int statusCode, string message) : base(message)
    {
        if (statusCode < 400 || statusCode > 599)
            throw new ArgumentOutOfRangeException(nameof(statusCode), "Errors require HTTP 400 through 599.");
        StatusCode = statusCode;
    }

    public ApiException(SystemMessage message) : this(message.Code, message.Message)
    {
        if (message.IsSuccess)
            throw new ArgumentException("A success message cannot be used as an error.", nameof(message));
    }

    protected ApiException(int expectedStatus, SystemMessage message) : this(message)
    {
        if (message.Code != expectedStatus)
            throw new ArgumentException("The message code must match the exception HTTP status.", nameof(message));
    }
}
