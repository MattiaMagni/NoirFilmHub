namespace FilmAPI.Model;

public class AccountActionToken
{
    public int Id { get; set; }
    public int UtenteId { get; set; }
    public string TokenHash { get; set; } = null!;
    public string TokenType { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Utente Utente { get; set; } = null!;
}
