namespace COSGeniusSystem.Services;

public interface ITokenBlacklistService
{
    void BlacklistToken(string token, TimeSpan duration);
    bool IsTokenBlacklisted(string token);
}
