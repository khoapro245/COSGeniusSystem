using COSGeniusSystem.Commons;

namespace COSGeniusSystem.Exceptions;

public sealed class InternalServerErrorException : ApiException
{
    public InternalServerErrorException(string message = "An unexpected error occurred.") : base(500, message) { }
    public InternalServerErrorException(SystemMessage message) : base(500, message) { }
}
