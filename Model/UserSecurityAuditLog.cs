namespace FilmAPI.Model;

public class UserSecurityAuditLog
{
    public long Id { get; set; }
    public int? UtenteId { get; set; }
    public string EventType { get; set; } = null!;
    public string? Provider { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Utente? Utente { get; set; }
}
