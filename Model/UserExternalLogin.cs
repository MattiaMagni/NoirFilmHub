namespace FilmAPI.Model;

public class UserExternalLogin
{
    public int Id { get; set; }
    public int UtenteId { get; set; }
    public string Provider { get; set; } = null!;
    public string ProviderKey { get; set; } = null!;
    public string? ProviderDisplayName { get; set; }
    public string? TenantId { get; set; }
    public string Email { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Utente Utente { get; set; } = null!;
}
