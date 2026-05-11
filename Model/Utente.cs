namespace FilmAPI.Model;

public class Utente
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string NormalizedEmail { get; set; } = null!;
    public string? PasswordHash { get; set; }
    public string Nome { get; set; } = null!;
    public string Cognome { get; set; } = null!;
    public string Telefono { get; set; } = "";
    public string Ruolo { get; set; } = RuoloUtente.Utente;
    public int? CinemaPreferitoId { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public bool LocalCredentialsEnabled { get; set; } = true;
    public int AuthVersion { get; set; } = 1;
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime? PasswordChangedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public string? LastLoginProvider { get; set; }
    public bool IsDisabled { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public decimal CreditoPiattaforma { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEndUtc { get; set; }
    public ICollection<Prenotazione> Prenotazioni { get; set; } = new List<Prenotazione>();
    public ICollection<UserExternalLogin> ExternalLogins { get; set; } = new List<UserExternalLogin>();
}
