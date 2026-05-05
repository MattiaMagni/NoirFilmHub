using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Services;

public class AuthService
{
    private readonly FilmDbContext _db;
    private readonly PasswordService _passwordService;
    private readonly JwtTokenService _jwtTokenService;

    public AuthService(FilmDbContext db, PasswordService passwordService, JwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordService = passwordService;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<(bool Success, string? Error, Utente? Utente)> RegisterAsync(RegisterRequestDTO dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(dto.Password) ||
            string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Cognome))
        {
            return (false, "Dati registrazione non validi", null);
        }

        if (dto.Password.Length < 8)
        {
            return (false, "La password deve contenere almeno 8 caratteri", null);
        }

        var exists = await _db.Utenti.AnyAsync(u => u.Email == email);
        if (exists)
        {
            return (false, "Email gia registrata", null);
        }

        var utente = new Utente
        {
            Email = email,
            PasswordHash = _passwordService.HashPassword(dto.Password),
            Nome = dto.Nome.Trim(),
            Cognome = dto.Cognome.Trim(),
            Telefono = dto.Telefono?.Trim() ?? string.Empty,
            Ruolo = RuoloUtente.Utente
        };

        _db.Utenti.Add(utente);
        await _db.SaveChangesAsync();
        return (true, null, utente);
    }

    public async Task<(bool Success, string? Error, LoginResponseDTO? Response)> LoginAsync(LoginRequestDTO dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.Email == email);
        if (utente is null)
        {
            return (false, "Credenziali non valide", null);
        }

        if (!_passwordService.VerifyPassword(dto.Password, utente.PasswordHash))
        {
            return (false, "Credenziali non valide", null);
        }

        var accessToken = _jwtTokenService.GenerateAccessToken(utente);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        utente.RefreshToken = refreshToken;
        utente.RefreshTokenExpiryTime = _jwtTokenService.GetRefreshExpiryUtc();
        await _db.SaveChangesAsync();

        return (true, null, new LoginResponseDTO
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            Utente = ToUtenteDto(utente)
        });
    }

    public async Task<(bool Success, string? Error, LoginResponseDTO? Response)> RefreshAsync(string refreshToken)
    {
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
        if (utente is null || utente.RefreshTokenExpiryTime is null || utente.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return (false, "Refresh token non valido o scaduto", null);
        }

        var newAccessToken = _jwtTokenService.GenerateAccessToken(utente);
        var newRefreshToken = _jwtTokenService.GenerateRefreshToken();

        utente.RefreshToken = newRefreshToken;
        utente.RefreshTokenExpiryTime = _jwtTokenService.GetRefreshExpiryUtc();
        await _db.SaveChangesAsync();

        return (true, null, new LoginResponseDTO
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            Utente = ToUtenteDto(utente)
        });
    }

    public async Task LogoutAsync(int userId)
    {
        var utente = await _db.Utenti.FindAsync(userId);
        if (utente is null)
        {
            return;
        }

        utente.RefreshToken = null;
        utente.RefreshTokenExpiryTime = null;
        await _db.SaveChangesAsync();
    }

    public static UtenteDTO ToUtenteDto(Utente utente)
    {
        return new UtenteDTO
        {
            Id = utente.Id,
            Email = utente.Email,
            Nome = utente.Nome,
            Cognome = utente.Cognome,
            Telefono = utente.Telefono,
            Ruolo = utente.Ruolo,
            CinemaPreferitoId = utente.CinemaPreferitoId
        };
    }
}
