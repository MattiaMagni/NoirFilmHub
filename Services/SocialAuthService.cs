using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Services;

public class SocialAuthService
{
    private readonly FilmDbContext _db;
    private readonly JwtTokenService _jwtTokenService;
    private readonly SecurityAuditService _auditService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SocialAuthService(FilmDbContext db, JwtTokenService jwtTokenService,
        SecurityAuditService auditService, IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _db = db;
        _jwtTokenService = jwtTokenService;
        _auditService = auditService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<(string? AuthorizationUrl, string? Error)> InitiateAsync(string provider, string? returnUrl, string? mode, string baseUrl)
    {
        var state = Guid.NewGuid().ToString("N");
        var stateEntity = new ExternalAuthState
        {
            Id = state,
            ReturnUrl = IsValidReturnUrl(returnUrl) ? returnUrl : null,
            Provider = provider,
            Mode = mode,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        };
        _db.ExternalAuthStates.Add(stateEntity);
        await _db.SaveChangesAsync();

        var callbackUrl = $"{baseUrl.TrimEnd('/')}/auth/external/callback";

        var authUrl = provider.ToLowerInvariant() switch
        {
            "google" => BuildGoogleUrl(callbackUrl, state),
            "microsoft" => BuildMicrosoftUrl(callbackUrl, state),
            _ => null
        };

        if (authUrl == null) return (null, "Provider non supportato");
        return (authUrl, null);
    }

    public async Task<(bool Success, string? Error, string? RedirectUrl)> HandleCallbackAsync(
        string code, string state, string baseUrl, string? ipAddress, string? userAgent, string? frontendBaseUrl = null)
    {
        var stateEntity = await _db.ExternalAuthStates.FindAsync(state);
        if (stateEntity == null || stateEntity.ExpiresAtUtc < DateTime.UtcNow)
            return (false, "Sessione di login scaduta. Riprova.", null);

        var codeHash = ComputeSha256(code);
        var existingCode = await _db.ExternalAuthExchangeCodes
            .FirstOrDefaultAsync(e => e.CodeHash == codeHash);
        if (existingCode != null)
            return (false, "Codice di autorizzazione non valido.", null);

        _db.ExternalAuthExchangeCodes.Add(new ExternalAuthExchangeCode
        {
            CodeHash = codeHash,
            StateId = state
        });

        var (success, error, claims) = await ExchangeCodeAsync(stateEntity.Provider, code, baseUrl);
        if (!success) return (false, error, null);

        if (claims == null) return (false, "Claims provider non validi", null);
        var email = claims["email"];
        var emailVerified = claims.GetValueOrDefault("email_verified") == "true"
            || stateEntity.Provider == "microsoft";
        var providerKey = stateEntity.Provider == "microsoft" ? claims["oid"] : claims["sub"];
        var tenantId = stateEntity.Provider == "microsoft" ? claims.GetValueOrDefault("tid") : null;

        if (!emailVerified)
            return (false, "Email non verificata dal provider.", null);

        if (stateEntity.Provider == "microsoft")
        {
            var allowedMsDomains = _configuration["MICROSOFT_ALLOWED_DOMAINS"];
            if (!string.IsNullOrWhiteSpace(allowedMsDomains))
            {
                var domains = allowedMsDomains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var emailDomain = email.Split('@').LastOrDefault()?.ToLowerInvariant();
                if (emailDomain == null || !domains.Contains(emailDomain))
                    return (false, $"Accesso consentito solo con email @({string.Join(", ", domains)}).", null);
            }
        }

        Utente? utente;
        var mode = stateEntity.Mode;

        if (mode == "link")
        {
            return (false, "Linking deve essere fatto da utente autenticato", null);
        }

        var existingLogin = await _db.UserExternalLogins
            .Include(el => el.Utente)
            .FirstOrDefaultAsync(el => el.Provider == stateEntity.Provider && el.ProviderKey == providerKey);

        if (existingLogin != null)
        {
            utente = existingLogin.Utente;
            if (utente.IsDisabled)
                return (false, "Account disabilitato.", null);
        }
        else
        {
            var normalizedEmail = email.ToUpperInvariant();
            utente = await _db.Utenti.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

            if (utente != null)
            {
                if (utente.Ruolo == RuoloUtente.Admin || utente.Ruolo == RuoloUtente.PowerUser)
                    return (false, "Questo account richiede autenticazione locale. Usa email e password.", null);

                _db.UserExternalLogins.Add(new UserExternalLogin
                {
                    UtenteId = utente.Id,
                    Provider = stateEntity.Provider,
                    ProviderKey = providerKey,
                    TenantId = tenantId,
                    Email = email,
                    ProviderDisplayName = claims.GetValueOrDefault("name"),
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                utente = new Utente
                {
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    Nome = claims.GetValueOrDefault("given_name") ?? claims.GetValueOrDefault("name")?.Split(' ').FirstOrDefault() ?? "",
                    Cognome = claims.GetValueOrDefault("family_name") ?? claims.GetValueOrDefault("name")?.Split(' ').Skip(1).FirstOrDefault() ?? "",
                    Ruolo = RuoloUtente.Utente,
                    LocalCredentialsEnabled = false,
                    PasswordHash = null,
                    EmailVerified = true,
                    AuthVersion = 1,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.Utenti.Add(utente);
                await _db.SaveChangesAsync();

                _db.UserExternalLogins.Add(new UserExternalLogin
                {
                    UtenteId = utente.Id,
                    Provider = stateEntity.Provider,
                    ProviderKey = providerKey,
                    TenantId = tenantId,
                    Email = email,
                    ProviderDisplayName = claims.GetValueOrDefault("name"),
                    CreatedAtUtc = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();

                var tokenRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
                    .Replace("+", "-").Replace("/", "_").TrimEnd('=');
                var tokenHash = ComputeSha256(tokenRaw);

                _db.AccountActionTokens.Add(new AccountActionToken
                {
                    UtenteId = utente.Id,
                    TokenHash = tokenHash,
                    TokenType = "PasswordSetup",
                    ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
                });
                await _db.SaveChangesAsync();

                await _auditService.LogEventAsync(utente.Id, "SocialLoginCreated", stateEntity.Provider, ipAddress, userAgent);

                var setupFrontend = frontendBaseUrl ?? baseUrl.TrimEnd('/');
                var setupUrl = $"{setupFrontend}/setup-password.html" +
                    $"?token={Uri.EscapeDataString(tokenRaw)}" +
                    $"&email={Uri.EscapeDataString(email)}";
                return (true, null, setupUrl);
            }
        }

        await _db.SaveChangesAsync();

        utente.LastLoginAtUtc = DateTime.UtcNow;
        utente.LastLoginProvider = stateEntity.Provider;
        await _db.SaveChangesAsync();

        await _auditService.LogEventAsync(utente.Id, "LoginSuccess", stateEntity.Provider, ipAddress, userAgent);

        var accessToken = _jwtTokenService.GenerateAccessToken(utente);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        utente.RefreshToken = refreshToken;
        utente.RefreshTokenExpiryTime = _jwtTokenService.GetRefreshExpiryUtc();
        await _db.SaveChangesAsync();

        var userJson = System.Text.Json.JsonSerializer.Serialize(AuthService.ToUtenteDto(utente), JsonOptions);
        var returnUrl = IsValidReturnUrl(stateEntity.ReturnUrl) ? stateEntity.ReturnUrl : "/index.html";
        var frontendBase = frontendBaseUrl ?? baseUrl.TrimEnd('/');
        var redirectUrl = $"{frontendBase}/social-login-complete.html" +
            $"#access_token={Uri.EscapeDataString(accessToken)}" +
            $"&refresh_token={Uri.EscapeDataString(refreshToken)}" +
            $"&user={Uri.EscapeDataString(userJson)}" +
            $"&return_url={Uri.EscapeDataString(returnUrl ?? "")}";

        return (true, null, redirectUrl);
    }

    public async Task<bool> LinkExternalLoginAsync(int utenteId, string provider, string providerKey,
        string? tenantId, string email, string? providerDisplayName)
    {
        var exists = await _db.UserExternalLogins.AnyAsync(el =>
            el.Provider == provider && el.ProviderKey == providerKey);
        if (exists) return false;

        _db.UserExternalLogins.Add(new UserExternalLogin
        {
            UtenteId = utenteId,
            Provider = provider,
            ProviderKey = providerKey,
            TenantId = tenantId,
            Email = email,
            ProviderDisplayName = providerDisplayName,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ExternalLoginDTO>> GetExternalLoginsAsync(int utenteId)
    {
        return await _db.UserExternalLogins
            .Where(el => el.UtenteId == utenteId)
            .Select(el => new ExternalLoginDTO
            {
                Id = el.Id,
                Provider = el.Provider,
                ProviderDisplayName = el.ProviderDisplayName,
                TenantId = el.TenantId,
                Email = el.Email,
                CreatedAtUtc = el.CreatedAtUtc
            })
            .ToListAsync();
    }

    public async Task<bool> UnlinkExternalLoginAsync(int utenteId, int loginId)
    {
        var login = await _db.UserExternalLogins
            .FirstOrDefaultAsync(el => el.Id == loginId && el.UtenteId == utenteId);
        if (login == null) return false;

        var utente = await _db.Utenti.FindAsync(utenteId);
        if (utente == null) return false;

        if (!utente.LocalCredentialsEnabled)
        {
            var otherLogins = await _db.UserExternalLogins
                .CountAsync(el => el.UtenteId == utenteId && el.Id != loginId);
            if (otherLogins == 0)
                return false;
        }

        _db.UserExternalLogins.Remove(login);
        await _db.SaveChangesAsync();
        return true;
    }

    public static bool IsValidReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl)) return false;
        if (returnUrl.StartsWith("/")) return true;
        return false;
    }

    private string? BuildGoogleUrl(string callbackUrl, string state)
    {
        var clientId = _configuration["GOOGLE_CLIENT_ID"];
        if (string.IsNullOrWhiteSpace(clientId)) return null;
        return "https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
            "&response_type=code" +
            "&scope=openid%20email%20profile" +
            $"&state={Uri.EscapeDataString(state)}";
    }

    private string? BuildMicrosoftUrl(string callbackUrl, string state)
    {
        var clientId = _configuration["MICROSOFT_CLIENT_ID"];
        if (string.IsNullOrWhiteSpace(clientId)) return null;
        var tenantId = _configuration["MICROSOFT_TENANT_ID"] ?? "common";
        return $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize" +
            $"?client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
            "&response_type=code" +
            "&scope=openid%20email%20profile" +
            "&response_mode=query" +
            $"&state={Uri.EscapeDataString(state)}";
    }

    private async Task<(bool Success, string? Error, Dictionary<string, string>? Claims)> ExchangeCodeAsync(
        string provider, string code, string baseUrl)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var callbackUrl = $"{baseUrl.TrimEnd('/')}/auth/external/callback";

            var (tokenUrl, clientId, clientSecret) = provider.ToLowerInvariant() switch
            {
                "google" => ("https://oauth2.googleapis.com/token",
                    _configuration["GOOGLE_CLIENT_ID"] ?? "",
                    _configuration["GOOGLE_CLIENT_SECRET"] ?? ""),
                "microsoft" => ($"https://login.microsoftonline.com/{_configuration["MICROSOFT_TENANT_ID"] ?? "common"}/oauth2/v2.0/token",
                    _configuration["MICROSOFT_CLIENT_ID"] ?? "",
                    _configuration["MICROSOFT_CLIENT_SECRET"] ?? ""),
                _ => ("", "", "")
            };

            if (string.IsNullOrEmpty(clientId))
                return (false, $"Provider {provider} non configurato.", null);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = callbackUrl,
                ["grant_type"] = "authorization_code"
            });

            var response = await client.PostAsync(tokenUrl, content);
            if (!response.IsSuccessStatusCode)
                return (false, "Errore di comunicazione con il provider. Riprova.", null);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (json.TryGetProperty("id_token", out var idTokenElement))
            {
                var idToken = idTokenElement.GetString()!;
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(idToken);
                var claims = new Dictionary<string, string>();
                foreach (var claim in jwt.Claims)
                {
                    claims[claim.Type] = claim.Value;
                }
                return (true, null, claims);
            }

            return (false, "Token provider non valido", null);
        }
        catch (Exception)
        {
            return (false, "Errore di comunicazione con il provider.", null);
        }
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
