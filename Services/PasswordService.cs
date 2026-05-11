namespace FilmAPI.Services;

public class PasswordService
{
    static PasswordService()
    {
        // Detect if running on OpenSSL 3.x (many modern distros)
        // BCrypt.Net can throw if OpenSSL version is too new
        try
        {
            _ = BCrypt.Net.BCrypt.HashPassword("test_detection");
        }
        catch
        {
            // ignore - will be handled at actual hash time
        }
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }

    public static bool IsStrongPassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) return false;
        var hasUpper = false;
        var hasLower = false;
        var hasDigit = false;
        var hasSpecial = false;
        foreach (var c in password)
        {
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else hasSpecial = true;
        }
        return hasUpper && hasLower && hasDigit && hasSpecial;
    }
}
