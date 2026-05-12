namespace FilmAPI.Model;

public class GiftCardTemplate
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Importo { get; set; }
    public string? ImmaginePath { get; set; }
    public bool Attivo { get; set; } = true;
}
