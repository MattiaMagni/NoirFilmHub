namespace FilmAPI.DTOs;

public class UtenteAdminDetailDTO
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public string Cognome { get; set; } = null!;
    public string Telefono { get; set; } = "";
    public string Ruolo { get; set; } = null!;
    public bool IsDisabled { get; set; }
    public bool EmailVerified { get; set; }
    public bool LocalCredentialsEnabled { get; set; }
    public int AuthVersion { get; set; }
    public string SecurityStamp { get; set; } = null!;
    public DateTime? LastLoginAtUtc { get; set; }
    public string? LastLoginProvider { get; set; }
    public DateTime? PasswordChangedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public decimal CreditoPiattaforma { get; set; }
    public List<ExternalLoginDTO> ExternalLogins { get; set; } = new();
    public List<AuditLogEntryDTO> RecentAuditLog { get; set; } = new();
}

public class ExternalLoginDTO
{
    public int Id { get; set; }
    public string Provider { get; set; } = null!;
    public string? ProviderDisplayName { get; set; }
    public string? TenantId { get; set; }
    public string Email { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}

public class AuditLogEntryDTO
{
    public long Id { get; set; }
    public string EventType { get; set; } = null!;
    public string? Provider { get; set; }
    public string? IpAddress { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
