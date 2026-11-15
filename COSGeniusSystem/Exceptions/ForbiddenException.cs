using COSGeniusSystem.Commons;

namespace COSGeniusSystem.Exceptions;

public sealed class ForbiddenException : ApiException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.") : base(403, message) { }
    public ForbiddenException(SystemMessage message) : base(403, message) { }
}
