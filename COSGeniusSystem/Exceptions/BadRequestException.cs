using COSGeniusSystem.Commons;

namespace COSGeniusSystem.Exceptions;

public sealed class BadRequestException : ApiException
{
    public BadRequestException(string message = "Invalid request.") : base(400, message) { }
    public BadRequestException(SystemMessage message) : base(400, message) { }
}
