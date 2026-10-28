using System.Net;

namespace COSGeniusSystem.Exceptions;

public class AppException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public List<string>? Errors { get; }

    public AppException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest, List<string>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors;
    }
}

public class BadRequestException : AppException
{
    public BadRequestException(string message, List<string>? errors = null)
        : base(message, HttpStatusCode.BadRequest, errors)
    {
    }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Unauthorized access")
        : base(message, HttpStatusCode.Unauthorized)
    {
    }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You do not have permission to access this resource")
        : base(message, HttpStatusCode.Forbidden)
    {
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message = "Requested resource was not found")
        : base(message, HttpStatusCode.NotFound)
    {
    }
}

public class ConflictException : AppException
{
    public ConflictException(string message)
        : base(message, HttpStatusCode.Conflict)
    {
    }
}
