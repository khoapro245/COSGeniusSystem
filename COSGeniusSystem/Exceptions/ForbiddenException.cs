using COSGeniusSystem.Commons;

namespace COSGeniusSystem.Exceptions;

public sealed class ForbiddenException : ApiException
{
    public ForbiddenException(string message = "Access is forbidden.") : base(403, message) { }
    public ForbiddenException(SystemMessage message) : base(403, message) { }
}
