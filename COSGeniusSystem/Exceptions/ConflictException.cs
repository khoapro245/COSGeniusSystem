using COSGeniusSystem.Commons;

namespace COSGeniusSystem.Exceptions;

public sealed class ConflictException : ApiException
{
    public ConflictException(string message = "The operation conflicts with existing records.") : base(409, message) { }
    public ConflictException(SystemMessage message) : base(409, message) { }
}
