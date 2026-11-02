using COSGeniusSystem.Models;

namespace COSGeniusSystem.Services;

public interface ITokenService
{
    (string Token, int ExpiresIn) GenerateToken(User user, IEnumerable<string> roles);
    TimeSpan GetTokenRemainingLifetime(string token);
}
