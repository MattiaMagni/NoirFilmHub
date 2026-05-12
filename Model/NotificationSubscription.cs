namespace FilmAPI.Model;

public class NotificationSubscription
{
    public int Id { get; set; }
    public int UtenteId { get; set; }
    public Utente Utente { get; set; } = null!;
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime CreatoIl { get; set; } = DateTime.UtcNow;
    public DateTime? UltimoInvio { get; set; }
}
