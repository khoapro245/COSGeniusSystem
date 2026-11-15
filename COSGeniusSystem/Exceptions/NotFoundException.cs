using COSGeniusSystem.Commons;

namespace COSGeniusSystem.Exceptions;

public sealed class NotFoundException : ApiException
{
    public NotFoundException(string message = "The requested data was not found.") : base(404, message) { }
    public NotFoundException(SystemMessage message) : base(404, message) { }
}
