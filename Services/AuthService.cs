using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private readonly SecurityAuditService _auditService;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(FilmDbContext db, PasswordService passwordService, JwtTokenService jwtTokenService,
        SecurityAuditService auditService, EmailService emailService, IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordService = passwordService;
        _jwtTokenService = jwtTokenService;
        _auditService = auditService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(bool Success, string? Error, Utente? Utente)> RegisterAsync(RegisterRequestDTO dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(dto.Password) ||
            string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Cognome))
        {
            return (false, "Dati registrazione non validi", null);
        }

        if (!PasswordService.IsStrongPassword(dto.Password))
        {
            return (false, "La password deve contenere almeno 8 caratteri, 1 maiuscola, 1 minuscola, 1 numero e 1 carattere speciale", null);
        }

        var normalizedEmail = email.ToUpperInvariant();
        var exists = await _db.Utenti.AnyAsync(u => u.NormalizedEmail == normalizedEmail);
        if (exists)
        {
            return (false, "Email gia registrata", null);
        }

        var utente = new Utente
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = _passwordService.HashPassword(dto.Password),
            Nome = dto.Nome.Trim(),
            Cognome = dto.Cognome.Trim(),
            Telefono = dto.Telefono?.Trim() ?? string.Empty,
            Ruolo = RuoloUtente.Utente,
            LocalCredentialsEnabled = true,
            AuthVersion = 1,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Utenti.Add(utente);
        await _db.SaveChangesAsync();
        return (true, null, utente);
    }

    public async Task<(bool Success, string? Error, LoginResponseDTO? Response)> LoginAsync(LoginRequestDTO dto,
        string? ipAddress = null, string? userAgent = null)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var normalizedEmail = email.ToUpperInvariant();
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

        if (utente is null)
        {
            await _auditService.LogEventAsync(null, "LoginFailed", "local", ipAddress, userAgent, $"Unknown email: {email}");
            return (false, "Credenziali non valide", null);
        }

        if (utente.IsDisabled)
        {
            return (false, "Account disabilitato. Contatta l'amministratore.", null);
        }

        if (utente.LockoutEndUtc.HasValue && utente.LockoutEndUtc > DateTime.UtcNow)
        {
            var remaining = utente.LockoutEndUtc.Value - DateTime.UtcNow;
            return (false, $"Account temporaneamente bloccato. Riprova tra {remaining.Minutes} minuti.", null);
        }

        if (!utente.LocalCredentialsEnabled || utente.PasswordHash == null)
        {
            await _auditService.LogEventAsync(utente.Id, "LoginFailed", "local", ipAddress, userAgent, "Social-only account");
            return (false, "Questo account usa il login social. Usa Google o Microsoft.", null);
        }

        if (!_passwordService.VerifyPassword(dto.Password, utente.PasswordHash))
        {
            utente.FailedLoginAttempts++;
            if (utente.FailedLoginAttempts >= 5 && utente.FailedLoginAttempts < 10)
            {
                _ = _emailService.SendSecurityAlertEmail(utente.Email, utente.Nome,
                    "Tentativi di accesso sospetti", $"{utente.FailedLoginAttempts} tentativi falliti");
            }
            if (utente.FailedLoginAttempts >= 10)
            {
                utente.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
            }
            await _db.SaveChangesAsync();
            await _auditService.LogEventAsync(utente.Id, "LoginFailed", "local", ipAddress, userAgent);
            return (false, "Credenziali non valide", null);
        }

        return await GenerateLoginResponse(utente, "local", ipAddress, userAgent);
    }

    public async Task<(bool Success, string? Error, LoginResponseDTO? Response)> RefreshAsync(string refreshToken)
    {
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
        if (utente is null || utente.RefreshTokenExpiryTime is null || utente.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return (false, "Refresh token non valido o scaduto", null);
        }

        if (utente.IsDisabled)
        {
            utente.RefreshToken = null;
            utente.RefreshTokenExpiryTime = null;
            await _db.SaveChangesAsync();
            return (false, "Account disabilitato", null);
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

    public async Task LogoutAsync(int userId, bool allDevices = false)
    {
        var utente = await _db.Utenti.FindAsync(userId);
        if (utente is null) return;

        if (allDevices)
        {
            utente.AuthVersion++;
            utente.RefreshToken = null;
            utente.RefreshTokenExpiryTime = null;
            utente.SecurityStamp = Guid.NewGuid().ToString("N");
        }
        else
        {
            utente.RefreshToken = null;
            utente.RefreshTokenExpiryTime = null;
        }
        await _db.SaveChangesAsync();
        await _auditService.LogEventAsync(userId, "Logout", null, null, null, allDevices ? "All devices" : null);
    }

    public async Task<(bool Success, string? Error, LoginResponseDTO? Response)> ChangePasswordAsync(
        int userId, string currentPassword, string newPassword, string? ipAddress = null)
    {
        var utente = await _db.Utenti.FindAsync(userId);
        if (utente is null) return (false, "Utente non trovato", null);

        if (!utente.LocalCredentialsEnabled || utente.PasswordHash == null)
            return (false, "Account social-only. Imposta prima una password.", null);

        if (!PasswordService.IsStrongPassword(newPassword))
            return (false, "La password deve contenere almeno 8 caratteri, 1 maiuscola, 1 minuscola, 1 numero e 1 carattere speciale", null);

        if (!_passwordService.VerifyPassword(currentPassword, utente.PasswordHash))
            return (false, "Password corrente non valida", null);

        if (currentPassword == newPassword)
            return (false, "La nuova password deve essere diversa da quella corrente", null);

        utente.PasswordHash = _passwordService.HashPassword(newPassword);
        utente.PasswordChangedAtUtc = DateTime.UtcNow;
        utente.AuthVersion++;
        utente.SecurityStamp = Guid.NewGuid().ToString("N");
        utente.RefreshToken = null;
        utente.RefreshTokenExpiryTime = null;
        await _db.SaveChangesAsync();

        await _auditService.LogEventAsync(userId, "PasswordChanged", "local", ipAddress);
        _ = _emailService.SendPasswordChangedEmail(utente.Email, utente.Nome);

        return await GenerateLoginResponse(utente, "local", ipAddress, null);
    }

    public async Task<(bool Success, string? Message, string? Token, string? Email)> ForgotPasswordAsync(string email)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var utente = await _db.Utenti.FirstOrDefaultAsync(u =>
            u.NormalizedEmail == normalizedEmail && u.LocalCredentialsEnabled);

        if (utente != null)
        {
            var tokenRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
            var tokenHash = ComputeSha256(tokenRaw);

            _db.AccountActionTokens.Add(new AccountActionToken
            {
                UtenteId = utente.Id,
                TokenHash = tokenHash,
                TokenType = "PasswordReset",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(GetTtl("ACCOUNT_TOKEN_PASSWORD_RESET_TTL_MINUTES", 60))
            });
            await _db.SaveChangesAsync();

            var emailSent = await _emailService.SendPasswordResetEmail(utente.Email, tokenRaw, utente.Nome);
            if (!emailSent)
            {
                _logger.LogWarning("Failed to send password reset email to {Email}. Returning direct token.", utente.Email);
                return (true, "Email non inviata (errore SMTP). Usa il link diretto:", tokenRaw, utente.Email);
            }
        }

        return (true, "Se l'email e associata a un account, riceverai un link di recupero.", null, null);
    }

    public async Task<(bool Success, string? Error, LoginResponseDTO? Response)> ResetPasswordAsync(
        string email, string token, string newPassword)
    {
        if (!PasswordService.IsStrongPassword(newPassword))
            return (false, "La password deve contenere almeno 8 caratteri, 1 maiuscola, 1 minuscola, 1 numero e 1 carattere speciale", null);

        var normalizedEmail = email.Trim().ToUpperInvariant();
        var utente = await _db.Utenti.FirstOrDefaultAsync(u =>
            u.NormalizedEmail == normalizedEmail && u.LocalCredentialsEnabled);
        if (utente is null)
            return (false, "Token non valido o scaduto.", null);

        var tokenHash = ComputeSha256(token);
        var actionToken = await _db.AccountActionTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash
                && t.TokenType == "PasswordReset"
                && t.UtenteId == utente.Id
                && t.ConsumedAtUtc == null
                && t.ExpiresAtUtc > DateTime.UtcNow);

        if (actionToken is null)
            return (false, "Token non valido o scaduto.", null);

        actionToken.ConsumedAtUtc = DateTime.UtcNow;
        utente.PasswordHash = _passwordService.HashPassword(newPassword);
        utente.PasswordChangedAtUtc = DateTime.UtcNow;
        utente.AuthVersion++;
        utente.SecurityStamp = Guid.NewGuid().ToString("N");
        utente.RefreshToken = null;
        utente.RefreshTokenExpiryTime = null;
        utente.FailedLoginAttempts = 0;
        utente.LockoutEndUtc = null;
        await _db.SaveChangesAsync();

        await _auditService.LogEventAsync(utente.Id, "PasswordReset");

        return await GenerateLoginResponse(utente, "local", null, null);
    }

    public async Task<(bool Success, string? Error)> RequestPasswordSetupAsync(int userId)
    {
        var utente = await _db.Utenti.FindAsync(userId);
        if (utente is null) return (false, "Utente non trovato");
        if (utente.LocalCredentialsEnabled) return (false, "Hai gia una password.");

        var tokenRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var tokenHash = ComputeSha256(tokenRaw);

        _db.AccountActionTokens.Add(new AccountActionToken
        {
            UtenteId = utente.Id,
            TokenHash = tokenHash,
            TokenType = "PasswordSetup",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(GetTtl("ACCOUNT_TOKEN_PASSWORD_SETUP_TTL_MINUTES", 1440))
        });
        await _db.SaveChangesAsync();

        var emailSent = await _emailService.SendPasswordSetupEmail(utente.Email, tokenRaw, utente.Nome);
        if (!emailSent)
        {
            var setupLink = $"http://localhost:5001/setup-password.html?token={Uri.EscapeDataString(tokenRaw)}&email={Uri.EscapeDataString(utente.Email)}";
            _logger.LogWarning("Failed to send password setup email to {Email}. Direct link: {Link}", utente.Email, setupLink);
        }
        return (true, null);
    }

    public async Task<(bool Success, string? Error, LoginResponseDTO? Response)> SetupPasswordAsync(
        string email, string token, string newPassword)
    {
        if (!PasswordService.IsStrongPassword(newPassword))
            return (false, "La password deve contenere almeno 8 caratteri, 1 maiuscola, 1 minuscola, 1 numero e 1 carattere speciale", null);

        var normalizedEmail = email.Trim().ToUpperInvariant();
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (utente is null)
            return (false, "Token non valido o scaduto.", null);

        var tokenHash = ComputeSha256(token);
        var actionToken = await _db.AccountActionTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash
                && (t.TokenType == "PasswordSetup" || t.TokenType == "AdminInvite")
                && t.UtenteId == utente.Id
                && t.ConsumedAtUtc == null
                && t.ExpiresAtUtc > DateTime.UtcNow);

        if (actionToken is null)
            return (false, "Token non valido o scaduto.", null);

        actionToken.ConsumedAtUtc = DateTime.UtcNow;
        utente.PasswordHash = _passwordService.HashPassword(newPassword);
        utente.LocalCredentialsEnabled = true;
        utente.IsDisabled = false;
        utente.EmailVerified = true;
        utente.AuthVersion++;
        utente.SecurityStamp = Guid.NewGuid().ToString("N");
        utente.RefreshToken = null;
        utente.RefreshTokenExpiryTime = null;
        await _db.SaveChangesAsync();

        await _auditService.LogEventAsync(utente.Id, "PasswordSetup");

        return await GenerateLoginResponse(utente, "local", null, null);
    }

    public async Task RevokeAllSessionsAsync(int userId)
    {
        var utente = await _db.Utenti.FindAsync(userId);
        if (utente is null) return;

        utente.AuthVersion++;
        utente.RefreshToken = null;
        utente.RefreshTokenExpiryTime = null;
        utente.SecurityStamp = Guid.NewGuid().ToString("N");
        await _db.SaveChangesAsync();
        await _auditService.LogEventAsync(userId, "Logout", null, null, null, "All sessions revoked");
    }

    public async Task<UtenteListResponseDTO> GetUsersAsync(string? search, string? ruolo,
        bool? isDisabled, bool? hasLocalCredentials, int page, int pageSize,
        string? orderBy, string? orderDirection)
    {
        var query = _db.Utenti
            .Include(u => u.ExternalLogins)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(u => u.NormalizedEmail.Contains(term)
                || u.Nome.ToUpper().Contains(term)
                || u.Cognome.ToUpper().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(ruolo))
        {
            query = query.Where(u => u.Ruolo == ruolo);
        }

        if (isDisabled.HasValue)
        {
            query = query.Where(u => u.IsDisabled == isDisabled.Value);
        }

        if (hasLocalCredentials.HasValue)
        {
            query = query.Where(u => u.LocalCredentialsEnabled == hasLocalCredentials.Value);
        }

        var totalCount = await query.CountAsync();

        query = (orderBy?.ToLowerInvariant(), orderDirection?.ToLowerInvariant()) switch
        {
            ("email", "desc") => query.OrderByDescending(u => u.Email),
            ("email", _) => query.OrderBy(u => u.Email),
            ("nome", "desc") => query.OrderByDescending(u => u.Nome),
            ("nome", _) => query.OrderBy(u => u.Nome),
            ("createdatutc", "desc") => query.OrderByDescending(u => u.CreatedAtUtc),
            ("createdatutc", _) => query.OrderBy(u => u.CreatedAtUtc),
            ("lastloginatutc", "desc") => query.OrderByDescending(u => u.LastLoginAtUtc),
            ("lastloginatutc", _) => query.OrderBy(u => u.LastLoginAtUtc),
            _ => query.OrderBy(u => u.Id)
        };

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UtenteAdminDTO
            {
                Id = u.Id,
                Email = u.Email,
                Nome = u.Nome,
                Cognome = u.Cognome,
                Ruolo = u.Ruolo,
                IsDisabled = u.IsDisabled,
                EmailVerified = u.EmailVerified,
                LocalCredentialsEnabled = u.LocalCredentialsEnabled,
                LastLoginAtUtc = u.LastLoginAtUtc,
                CreatedAtUtc = u.CreatedAtUtc,
                ExternalLogins = u.ExternalLogins.Select(el => el.Provider).ToList(),
                HasPassword = u.PasswordHash != null,
                AuthVersion = u.AuthVersion
            })
            .ToListAsync();

        return new UtenteListResponseDTO
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<UtenteAdminDetailDTO?> GetUserDetailAsync(int userId)
    {
        var utente = await _db.Utenti
            .Include(u => u.ExternalLogins)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (utente is null) return null;

        var recentLogs = await _auditService.GetRecentLogsAsync(userId, 20);

        return new UtenteAdminDetailDTO
        {
            Id = utente.Id,
            Email = utente.Email,
            Nome = utente.Nome,
            Cognome = utente.Cognome,
            Telefono = utente.Telefono,
            Ruolo = utente.Ruolo,
            IsDisabled = utente.IsDisabled,
            EmailVerified = utente.EmailVerified,
            LocalCredentialsEnabled = utente.LocalCredentialsEnabled,
            AuthVersion = utente.AuthVersion,
            SecurityStamp = utente.SecurityStamp,
            LastLoginAtUtc = utente.LastLoginAtUtc,
            LastLoginProvider = utente.LastLoginProvider,
            PasswordChangedAtUtc = utente.PasswordChangedAtUtc,
            CreatedAtUtc = utente.CreatedAtUtc,
            CreditoPiattaforma = utente.CreditoPiattaforma,
            ExternalLogins = utente.ExternalLogins.Select(el => new ExternalLoginDTO
            {
                Id = el.Id,
                Provider = el.Provider,
                ProviderDisplayName = el.ProviderDisplayName,
                TenantId = el.TenantId,
                Email = el.Email,
                CreatedAtUtc = el.CreatedAtUtc
            }).ToList(),
            RecentAuditLog = recentLogs
        };
    }

    public async Task<(bool Success, string? Error)> ChangeUserRoleAsync(int userId, string nuovoRuolo,
        int currentUserId, string? ipAddress = null)
    {
        if (!RuoliValidi.Contains(nuovoRuolo))
            return (false, "Ruolo non valido");

        if (userId == currentUserId)
            return (false, "Non puoi modificare il tuo ruolo.");

        var utente = await _db.Utenti
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (utente is null) return (false, "Utente non trovato");

        var vecchioRuolo = utente.Ruolo;

        if (nuovoRuolo != RuoloUtente.Admin && utente.Ruolo == RuoloUtente.Admin)
        {
            var adminCount = await _db.Utenti.CountAsync(u => u.Ruolo == RuoloUtente.Admin && u.Id != userId);
            if (adminCount == 0)
                return (false, "Impossibile degradare l'ultimo amministratore.");
        }

        if ((nuovoRuolo == RuoloUtente.Admin || nuovoRuolo == RuoloUtente.PowerUser)
            && !utente.LocalCredentialsEnabled)
        {
            return (false, "Account social-only non promuovibile. L'utente deve prima impostare una password.");
        }

        utente.Ruolo = nuovoRuolo;
        utente.AuthVersion++;
        utente.RefreshToken = null;
        utente.RefreshTokenExpiryTime = null;
        await _db.SaveChangesAsync();

        await _auditService.LogEventAsync(userId, "RoleChanged", null, ipAddress, null,
            $"Ruolo cambiato da '{vecchioRuolo}' a '{nuovoRuolo}' da Admin {currentUserId}");
        _ = _emailService.SendRoleChangedEmail(utente.Email, utente.Nome, nuovoRuolo);

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DisableUserAsync(int userId, int currentUserId)
    {
        if (userId == currentUserId)
            return (false, "Non puoi disabilitare il tuo account.");

        var utente = await _db.Utenti.FindAsync(userId);
        if (utente is null) return (false, "Utente non trovato");

        if (utente.Ruolo == RuoloUtente.Admin)
        {
            var adminCount = await _db.Utenti.CountAsync(u => u.Ruolo == RuoloUtente.Admin && u.Id != userId);
            if (adminCount == 0)
                return (false, "Impossibile disabilitare l'ultimo amministratore.");
        }

        utente.IsDisabled = true;
        utente.AuthVersion++;
        utente.RefreshToken = null;
        utente.RefreshTokenExpiryTime = null;
        await _db.SaveChangesAsync();

        await _auditService.LogEventAsync(userId, "AccountDisabled", null, null, null,
            $"Disabilitato da Admin {currentUserId}");
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> EnableUserAsync(int userId)
    {
        var utente = await _db.Utenti.FindAsync(userId);
        if (utente is null) return (false, "Utente non trovato");

        utente.IsDisabled = false;
        utente.AuthVersion++;
        utente.FailedLoginAttempts = 0;
        utente.LockoutEndUtc = null;
        await _db.SaveChangesAsync();

        await _auditService.LogEventAsync(userId, "AccountEnabled");
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ForcePasswordResetAsync(int userId, int currentUserId)
    {
        var utente = await _db.Utenti.FindAsync(userId);
        if (utente is null) return (false, "Utente non trovato");

        if (!utente.LocalCredentialsEnabled)
            return (false, "L'utente non ha credenziali locali.");

        utente.AuthVersion++;
        utente.RefreshToken = null;
        utente.RefreshTokenExpiryTime = null;

        var tokenRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var tokenHash = ComputeSha256(tokenRaw);

        _db.AccountActionTokens.Add(new AccountActionToken
        {
            UtenteId = utente.Id,
            TokenHash = tokenHash,
            TokenType = "PasswordReset",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(GetTtl("ACCOUNT_TOKEN_PASSWORD_RESET_TTL_MINUTES", 60))
        });
        await _db.SaveChangesAsync();

        _ = _emailService.SendPasswordResetEmail(utente.Email, tokenRaw, utente.Nome);
        await _auditService.LogEventAsync(userId, "PasswordResetForced", null, null, null,
            $"Forzato da Admin {currentUserId}");

        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteUserAsync(int userId, int currentUserId)
    {
        if (userId == currentUserId)
            return (false, "Non puoi eliminare il tuo account.");

        var utente = await _db.Utenti.FindAsync(userId);
        if (utente is null) return (false, "Utente non trovato");

        if (utente.Ruolo == RuoloUtente.Admin)
        {
            var adminCount = await _db.Utenti.CountAsync(u => u.Ruolo == RuoloUtente.Admin && u.Id != userId);
            if (adminCount == 0)
                return (false, "Impossibile eliminare l'ultimo amministratore.");
        }

        await _auditService.LogEventAsync(userId, "AccountDeleted", null, null, null,
            $"Eliminato da Admin {currentUserId}. Dati: {JsonSerializer.Serialize(ToUtenteDto(utente))}");

        _db.Utenti.Remove(utente);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error, Utente? Utente)> InviteUserAsync(InviteUserDTO dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var normalizedEmail = email.ToUpperInvariant();

        if (!RuoliValidi.Contains(dto.Ruolo))
            return (false, "Ruolo non valido", null);

        if (dto.Ruolo == RuoloUtente.Utente)
            return (false, "Usa la registrazione normale per utenti standard", null);

        var exists = await _db.Utenti.AnyAsync(u => u.NormalizedEmail == normalizedEmail);
        if (exists)
            return (false, "Email gia registrata", null);

        var utente = new Utente
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = null,
            Nome = dto.Nome.Trim(),
            Cognome = dto.Cognome.Trim(),
            Ruolo = dto.Ruolo,
            LocalCredentialsEnabled = false,
            IsDisabled = true,
            AuthVersion = 1,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Utenti.Add(utente);
        await _db.SaveChangesAsync();

        if (dto.SendSetupEmail)
        {
            var tokenRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
            var tokenHash = ComputeSha256(tokenRaw);

            _db.AccountActionTokens.Add(new AccountActionToken
            {
                UtenteId = utente.Id,
                TokenHash = tokenHash,
                TokenType = "AdminInvite",
                ExpiresAtUtc = DateTime.UtcNow.AddHours(GetTtl("ACCOUNT_TOKEN_ADMIN_INVITE_TTL_HOURS", 72))
            });
            await _db.SaveChangesAsync();

            _ = _emailService.SendAdminInviteEmail(utente.Email, tokenRaw, utente.Nome, utente.Ruolo);
        }

        await _auditService.LogEventAsync(utente.Id, "AdminInvite", null, null, null,
            $"Invitato come {dto.Ruolo}");

        return (true, null, utente);
    }

    private async Task<(bool Success, string? Error, LoginResponseDTO Response)> GenerateLoginResponse(
        Utente utente, string provider, string? ipAddress, string? userAgent)
    {
        utente.LastLoginAtUtc = DateTime.UtcNow;
        utente.LastLoginProvider = provider;
        utente.FailedLoginAttempts = 0;
        utente.LockoutEndUtc = null;

        var accessToken = _jwtTokenService.GenerateAccessToken(utente);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        utente.RefreshToken = refreshToken;
        utente.RefreshTokenExpiryTime = _jwtTokenService.GetRefreshExpiryUtc();
        await _db.SaveChangesAsync();

        await _auditService.LogEventAsync(utente.Id, "LoginSuccess", provider, ipAddress, userAgent);

        return (true, null, new LoginResponseDTO
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            Utente = ToUtenteDto(utente)
        });
    }

    private int GetTtl(string key, int defaultMinutes)
    {
        return int.TryParse(_configuration[key], out var parsed) ? parsed : defaultMinutes;
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
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
            CinemaPreferitoId = utente.CinemaPreferitoId,
            LocalCredentialsEnabled = utente.LocalCredentialsEnabled,
            EmailVerified = utente.EmailVerified,
            IsDisabled = utente.IsDisabled,
            LastLoginAtUtc = utente.LastLoginAtUtc,
            CreatedAtUtc = utente.CreatedAtUtc,
            ExternalLogins = utente.ExternalLogins?.Select(el => el.Provider).ToList() ?? new List<string>()
        };
    }

    private static readonly HashSet<string> RuoliValidi = new()
    {
        Model.RuoloUtente.Admin,
        Model.RuoloUtente.PowerUser,
        Model.RuoloUtente.Utente
    };
}
