namespace COSGeniusSystem.Services;


public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, 12);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        try
        {
            if (BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash)) return true;
        }
        catch { }
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch { return false; }
    }
}
