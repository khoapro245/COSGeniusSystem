using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using COSGeniusSystem.Models;
using Microsoft.IdentityModel.Tokens;

namespace COSGeniusSystem.Services;

public interface ITokenService
{
    (string Token, int ExpiresIn) GenerateToken(User user, IEnumerable<string> roles);
    TimeSpan GetTokenRemainingLifetime(string token);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, int ExpiresIn) GenerateToken(User user, IEnumerable<string> roles)
    {
        var secretKey = _configuration["Jwt:SecretKey"] 
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        var issuer = _configuration["Jwt:Issuer"] ?? "COSGeniusSystem";
        var audience = _configuration["Jwt:Audience"] ?? "COSGeniusClient";
        var expirationMinutes = int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var exp) ? exp : 1440;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var expires = DateTime.UtcNow.AddMinutes(expirationMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(securityToken);

        return (tokenString, expirationMinutes * 60);
    }

    public TimeSpan GetTokenRemainingLifetime(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);
                var validTo = jwtToken.ValidTo;
                var remaining = validTo - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }
        catch
        {
            // Fallback default
        }

        return TimeSpan.FromHours(1);
    }
}
