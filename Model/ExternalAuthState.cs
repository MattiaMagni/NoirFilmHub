namespace FilmAPI.Model;

public class ExternalAuthState
{
    public string Id { get; set; } = null!;
    public string? ReturnUrl { get; set; }
    public string Provider { get; set; } = null!;
    public string? Mode { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
}
