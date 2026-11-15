using COSGeniusSystem.Commons;

namespace COSGeniusSystem.Exceptions;

public sealed class UnauthorizedException : ApiException
{
    public UnauthorizedException(string message = "Please sign in to continue.") : base(401, message) { }
    public UnauthorizedException(SystemMessage message) : base(401, message) { }
}
