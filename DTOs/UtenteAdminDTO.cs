namespace FilmAPI.DTOs;

public class UtenteAdminDTO
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public string Cognome { get; set; } = null!;
    public string Ruolo { get; set; } = null!;
    public bool IsDisabled { get; set; }
    public bool EmailVerified { get; set; }
    public bool LocalCredentialsEnabled { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<string> ExternalLogins { get; set; } = new();
    public bool HasPassword { get; set; }
    public int AuthVersion { get; set; }
}
