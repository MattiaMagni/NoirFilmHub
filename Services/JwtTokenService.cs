using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FilmAPI.Model;
using Microsoft.IdentityModel.Tokens;

namespace FilmAPI.Services;

public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(Utente utente)
    {
        var issuer = _configuration["JWT_ISSUER"] ?? "FilmAPI";
        var audience = _configuration["JWT_AUDIENCE"] ?? "FilmFrontend";
        var secret = _configuration["JWT_SECRET_KEY"] ?? "dev-secret-key-change-in-production-123456";
        var expiryMinutes = int.TryParse(_configuration["JWT_ACCESS_TOKEN_EXPIRY_MINUTES"], out var parsed)
            ? parsed
            : 15;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, utente.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, utente.Email),
            new(ClaimTypes.NameIdentifier, utente.Id.ToString()),
            new(ClaimTypes.Name, $"{utente.Nome} {utente.Cognome}".Trim()),
            new(ClaimTypes.Role, utente.Ruolo),
            new("ruolo", utente.Ruolo),
            new("auth_version", utente.AuthVersion.ToString()),
            new("security_stamp", utente.SecurityStamp)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public DateTime GetRefreshExpiryUtc()
    {
        var expiryDays = int.TryParse(_configuration["JWT_REFRESH_TOKEN_EXPIRY_DAYS"], out var parsed)
            ? parsed
            : 7;

        return DateTime.UtcNow.AddDays(expiryDays);
    }
}
