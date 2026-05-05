namespace FilmAPI.Model;

public class SeatLock
{
    public int Id { get; set; }
    public int ProiezioneId { get; set; }
    public Proiezione Proiezione { get; set; } = null!;
    public int UtenteId { get; set; }
    public Utente Utente { get; set; } = null!;
    public string PostoCodice { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
}
