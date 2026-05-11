using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using FilmAPI.Data;
using FilmAPI.DTOs;
using FilmAPI.Model;
using FilmAPI.Services;
using FluentAssertions;
using Xunit;

namespace FilmAPI.Tests.Unit;

public class AuthServiceTests : IAsyncLifetime
{
    private readonly FilmDbContext _db;
    private readonly AuthService _authService;
    private readonly PasswordService _passwordService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly SecurityAuditService _auditService;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<FilmDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new FilmDbContext(options);

        var configValues = new Dictionary<string, string?>
        {
            ["JWT_SECRET_KEY"] = "test-secret-key-for-unit-tests-64-chars-long-minimum-required-length!!",
            ["JWT_ISSUER"] = "TestIssuer",
            ["JWT_AUDIENCE"] = "TestAudience",
            ["JWT_ACCESS_TOKEN_EXPIRY_MINUTES"] = "15",
            ["JWT_REFRESH_TOKEN_EXPIRY_DAYS"] = "7",
            ["ACCOUNT_TOKEN_PASSWORD_RESET_TTL_MINUTES"] = "60",
            ["ACCOUNT_TOKEN_PASSWORD_SETUP_TTL_MINUTES"] = "1440",
            ["ACCOUNT_TOKEN_ADMIN_INVITE_TTL_HOURS"] = "72",
            ["APP_BASE_URL"] = "http://localhost:5001",
            ["SMTP_HOST"] = ""
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        _passwordService = new PasswordService();
        _jwtTokenService = new JwtTokenService(_configuration);

        var emailLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<EmailService>.Instance;
        _emailService = new EmailService(_configuration, emailLogger);
        _auditService = new SecurityAuditService(_db);
        _authService = new AuthService(_db, _passwordService, _jwtTokenService, _auditService, _emailService, _configuration);
    }

    public async Task InitializeAsync() => await Task.CompletedTask;
    public async Task DisposeAsync() => await _db.Database.EnsureDeletedAsync();

    private void EnsureEntitiesConfigured()
    {
    }

    [Fact]
    public async Task RegisterAsync_ValidData_CreatesUser()
    {
        var dto = new RegisterRequestDTO
        {
            Email = "test@example.com",
            Password = "TestPass123!",
            Nome = "Mario",
            Cognome = "Rossi"
        };

        var (success, error, utente) = await _authService.RegisterAsync(dto);

        success.Should().BeTrue();
        error.Should().BeNull();
        utente.Should().NotBeNull();
        utente!.Email.Should().Be("test@example.com");
        utente.NormalizedEmail.Should().Be("TEST@EXAMPLE.COM");
        utente.LocalCredentialsEnabled.Should().BeTrue();
        utente.Ruolo.Should().Be(RuoloUtente.Utente);
        utente.AuthVersion.Should().Be(1);
        utente.IsDisabled.Should().BeFalse();
        utente.PasswordHash.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsError()
    {
        var dto = new RegisterRequestDTO
        {
            Email = "dupe@example.com",
            Password = "TestPass123!",
            Nome = "Mario",
            Cognome = "Rossi"
        };
        await _authService.RegisterAsync(dto);

        var (success, error, _) = await _authService.RegisterAsync(dto);

        success.Should().BeFalse();
        error.Should().Be("Email gia registrata");
    }

    [Fact]
    public async Task RegisterAsync_WeakPassword_ReturnsError()
    {
        var dto = new RegisterRequestDTO
        {
            Email = "test@example.com",
            Password = "short",
            Nome = "Mario",
            Cognome = "Rossi"
        };

        var (success, error, _) = await _authService.RegisterAsync(dto);

        success.Should().BeFalse();
        error.Should().Contain("password");
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokens()
    {
        var dto = new RegisterRequestDTO
        {
            Email = "mario@example.com",
            Password = "TestPass123!",
            Nome = "Mario",
            Cognome = "Rossi"
        };
        await _authService.RegisterAsync(dto);

        var (success, error, response) = await _authService.LoginAsync(new LoginRequestDTO
        {
            Email = "mario@example.com",
            Password = "TestPass123!"
        });

        success.Should().BeTrue();
        error.Should().BeNull();
        response.Should().NotBeNull();
        response!.AccessToken.Should().NotBeNullOrEmpty();
        response.RefreshToken.Should().NotBeNullOrEmpty();
        response.Utente.Email.Should().Be("mario@example.com");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsError()
    {
        var dto = new RegisterRequestDTO
        {
            Email = "mario@example.com",
            Password = "TestPass123!",
            Nome = "Mario",
            Cognome = "Rossi"
        };
        await _authService.RegisterAsync(dto);

        var (success, error, _) = await _authService.LoginAsync(new LoginRequestDTO
        {
            Email = "mario@example.com",
            Password = "WrongPass123!"
        });

        success.Should().BeFalse();
        error.Should().Be("Credenziali non valide");
    }

    [Fact]
    public async Task LoginAsync_NonExistentEmail_ReturnsError()
    {
        var (success, error, _) = await _authService.LoginAsync(new LoginRequestDTO
        {
            Email = "ghost@example.com",
            Password = "TestPass123!"
        });

        success.Should().BeFalse();
        error.Should().Be("Credenziali non valide");
    }

    [Fact]
    public async Task LoginAsync_DisabledAccount_ReturnsError()
    {
        var dto = new RegisterRequestDTO
        {
            Email = "locked@example.com",
            Password = "TestPass123!",
            Nome = "Mario",
            Cognome = "Rossi"
        };
        var (_, _, utente) = await _authService.RegisterAsync(dto);
        utente!.IsDisabled = true;
        await _db.SaveChangesAsync();

        var (success, error, _) = await _authService.LoginAsync(new LoginRequestDTO
        {
            Email = "locked@example.com",
            Password = "TestPass123!"
        });

        success.Should().BeFalse();
        error.Should().Contain("disabilitato");
    }

    [Fact]
    public async Task ChangePassword_Valid_SucceedsAndInvalidatesTokens()
    {
        var dto = new RegisterRequestDTO
        {
            Email = "change@example.com",
            Password = "OldPass123!",
            Nome = "Mario",
            Cognome = "Rossi"
        };
        var (_, _, utente) = await _authService.RegisterAsync(dto);
        var oldAuthVersion = utente!.AuthVersion;

        var (success, error, response) = await _authService.ChangePasswordAsync(
            utente.Id, "OldPass123!", "NewPass456!");

        success.Should().BeTrue();
        error.Should().BeNull();
        response.Should().NotBeNull();
        response!.AccessToken.Should().NotBeNullOrEmpty();

        var updated = await _db.Utenti.FindAsync(utente.Id);
        updated!.AuthVersion.Should().BeGreaterThan(oldAuthVersion);
        updated.PasswordChangedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsError()
    {
        var dto = new RegisterRequestDTO
        {
            Email = "change@example.com",
            Password = "OldPass123!",
            Nome = "Mario",
            Cognome = "Rossi"
        };
        var (_, _, utente) = await _authService.RegisterAsync(dto);

        var (success, error, _) = await _authService.ChangePasswordAsync(
            utente!.Id, "WrongOldPass!", "NewPass456!");

        success.Should().BeFalse();
        error.Should().Be("Password corrente non valida");
    }

    [Fact]
    public async Task ForgotPassword_AlwaysReturnsSuccessMessage()
    {
        var (success, message) = await _authService.ForgotPasswordAsync("unknown@example.com");

        success.Should().BeTrue();
        message.Should().Contain("Se l'email");
    }

    [Fact]
    public async Task ForgotPassword_ExistingUser_CreatesToken()
    {
        var dto = new RegisterRequestDTO
        {
            Email = "reset@example.com",
            Password = "TestPass123!",
            Nome = "Mario",
            Cognome = "Rossi"
        };
        await _authService.RegisterAsync(dto);

        var (success, _) = await _authService.ForgotPasswordAsync("reset@example.com");

        success.Should().BeTrue();
        var tokens = await _db.AccountActionTokens.ToListAsync();
        tokens.Should().HaveCount(1);
        tokens[0].TokenType.Should().Be("PasswordReset");
    }

    [Fact]
    public async Task InviteUser_CreatesDisabledUser()
    {
        var (success, error, utente) = await _authService.InviteUserAsync(new InviteUserDTO
        {
            Email = "newadmin@example.com",
            Ruolo = RuoloUtente.PowerUser,
            Nome = "Nuovo",
            Cognome = "Admin",
            SendSetupEmail = false
        });

        success.Should().BeTrue();
        error.Should().BeNull();
        utente.Should().NotBeNull();
        utente!.IsDisabled.Should().BeTrue();
        utente.LocalCredentialsEnabled.Should().BeFalse();
        utente.PasswordHash.Should().BeNull();
        utente.Ruolo.Should().Be(RuoloUtente.PowerUser);
    }

    [Fact]
    public async Task InviteUser_ExistingEmail_ReturnsError()
    {
        await _authService.RegisterAsync(new RegisterRequestDTO
        {
            Email = "exists@example.com",
            Password = "TestPass123!",
            Nome = "Mario",
            Cognome = "Rossi"
        });

        var (success, error, _) = await _authService.InviteUserAsync(new InviteUserDTO
        {
            Email = "exists@example.com",
            Ruolo = RuoloUtente.PowerUser,
            Nome = "Mario",
            Cognome = "Rossi",
            SendSetupEmail = false
        });

        success.Should().BeFalse();
        error.Should().Be("Email gia registrata");
    }

    [Fact]
    public async Task ChangeUserRole_SocialOnlyCannotBePromoted()
    {
        var utente = new Utente
        {
            Email = "social@example.com",
            NormalizedEmail = "SOCIAL@EXAMPLE.COM",
            Nome = "Mario",
            Cognome = "Rossi",
            Ruolo = RuoloUtente.Utente,
            LocalCredentialsEnabled = false,
            PasswordHash = null,
            AuthVersion = 1,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Utenti.Add(utente);
        await _db.SaveChangesAsync();

        var (success, error) = await _authService.ChangeUserRoleAsync(
            utente.Id, RuoloUtente.PowerUser, 999);

        success.Should().BeFalse();
        error.Should().Contain("social-only");
    }

    [Fact]
    public async Task ChangeUserRole_CannotDegradeSelf()
    {
        var admin = new Utente
        {
            Email = "admin@example.com",
            NormalizedEmail = "ADMIN@EXAMPLE.COM",
            Nome = "Admin",
            Cognome = "Test",
            Ruolo = RuoloUtente.Admin,
            LocalCredentialsEnabled = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            AuthVersion = 1,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Utenti.Add(admin);
        await _db.SaveChangesAsync();

        var (success, error) = await _authService.ChangeUserRoleAsync(
            admin.Id, RuoloUtente.Utente, admin.Id);

        success.Should().BeFalse();
        error.Should().Be("Non puoi modificare il tuo ruolo.");
    }

    [Fact]
    public async Task DisableUser_InvalidatesTokens()
    {
        var utente = new Utente
        {
            Email = "todisable@example.com",
            NormalizedEmail = "TODISABLE@EXAMPLE.COM",
            Nome = "Mario",
            Cognome = "Rossi",
            Ruolo = RuoloUtente.Utente,
            LocalCredentialsEnabled = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!"),
            AuthVersion = 1,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow,
            RefreshToken = "some-refresh-token",
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7)
        };
        _db.Utenti.Add(utente);
        await _db.SaveChangesAsync();
        var oldAuthVersion = utente.AuthVersion;

        var (success, _) = await _authService.DisableUserAsync(utente.Id, 999);

        success.Should().BeTrue();
        var updated = await _db.Utenti.FindAsync(utente.Id);
        updated!.IsDisabled.Should().BeTrue();
        updated.AuthVersion.Should().BeGreaterThan(oldAuthVersion);
        updated.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task GetUsers_Pagination_Works()
    {
        for (var i = 0; i < 25; i++)
        {
            _db.Utenti.Add(new Utente
            {
                Email = $"user{i}@example.com",
                NormalizedEmail = $"USER{i}@EXAMPLE.COM",
                Nome = "User",
                Cognome = i.ToString(),
                Ruolo = RuoloUtente.Utente,
                LocalCredentialsEnabled = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!"),
                AuthVersion = 1,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        var result = await _authService.GetUsersAsync(null, null, null, null, 1, 10, "id", "asc");

        result.Items.Should().HaveCount(10);
        result.TotalCount.Should().Be(25);
        result.TotalPages.Should().Be(3);
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task Logout_AllDevices_InvalidatesAllSessions()
    {
        var utente = new Utente
        {
            Email = "logout@example.com",
            NormalizedEmail = "LOGOUT@EXAMPLE.COM",
            Nome = "Mario",
            Cognome = "Rossi",
            Ruolo = RuoloUtente.Utente,
            LocalCredentialsEnabled = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPass123!"),
            AuthVersion = 1,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow,
            RefreshToken = "token123",
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7)
        };
        _db.Utenti.Add(utente);
        await _db.SaveChangesAsync();
        var oldVersion = utente.AuthVersion;

        await _authService.LogoutAsync(utente.Id, allDevices: true);

        var updated = await _db.Utenti.FindAsync(utente.Id);
        updated!.AuthVersion.Should().BeGreaterThan(oldVersion);
        updated.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task ToUtenteDto_MapsAllRequiredFields()
    {
        var utente = new Utente
        {
            Id = 42,
            Email = "test@example.com",
            Nome = "Mario",
            Cognome = "Rossi",
            Telefono = "123456",
            Ruolo = RuoloUtente.Utente,
            LocalCredentialsEnabled = true,
            EmailVerified = true,
            IsDisabled = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        var dto = AuthService.ToUtenteDto(utente);

        dto.Id.Should().Be(42);
        dto.Email.Should().Be("test@example.com");
        dto.Nome.Should().Be("Mario");
        dto.Cognome.Should().Be("Rossi");
        dto.Ruolo.Should().Be(RuoloUtente.Utente);
        dto.LocalCredentialsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task SocialAuth_IsValidReturnUrl_BlocksExternalUrls()
    {
        SocialAuthService.IsValidReturnUrl("https://evil.com").Should().BeFalse();
        SocialAuthService.IsValidReturnUrl("/profile.html").Should().BeTrue();
        SocialAuthService.IsValidReturnUrl("/index.html").Should().BeTrue();
        SocialAuthService.IsValidReturnUrl(null).Should().BeFalse();
        SocialAuthService.IsValidReturnUrl("").Should().BeFalse();
    }

    [Fact]
    public async Task PasswordService_StrongPasswordCheck()
    {
        PasswordService.IsStrongPassword("short").Should().BeFalse();
        PasswordService.IsStrongPassword("lowercaseonly").Should().BeFalse();
        PasswordService.IsStrongPassword("UPPERCASEONLY").Should().BeFalse();
        PasswordService.IsStrongPassword("12345678").Should().BeFalse();
        PasswordService.IsStrongPassword("Abc123!@").Should().BeTrue();
        PasswordService.IsStrongPassword("MySecureP@ss1").Should().BeTrue();
    }

    [Fact]
    public async Task PasswordService_HashAndVerify()
    {
        var hash = _passwordService.HashPassword("TestPass123!");
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe("TestPass123!");

        _passwordService.VerifyPassword("TestPass123!", hash).Should().BeTrue();
        _passwordService.VerifyPassword("WrongPass123!", hash).Should().BeFalse();
    }
}
