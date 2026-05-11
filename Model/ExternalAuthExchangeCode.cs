namespace FilmAPI.Model;

public class ExternalAuthExchangeCode
{
    public int Id { get; set; }
    public string CodeHash { get; set; } = null!;
    public string StateId { get; set; } = null!;
    public DateTime? ConsumedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
