using COSGeniusSystem.Commons;

namespace COSGeniusSystem.Exceptions;

public sealed class UnauthorizedException : ApiException
{
    public UnauthorizedException(string message = "Authentication is required.") : base(401, message) { }
    public UnauthorizedException(SystemMessage message) : base(401, message) { }
}
