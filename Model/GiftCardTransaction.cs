namespace FilmAPI.Model;

public class GiftCardTransaction
{
    public int Id { get; set; }
    public int GiftCardId { get; set; }
    public GiftCard GiftCard { get; set; } = null!;
    public int? CartId { get; set; }
    public Cart? Cart { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Importo { get; set; }
    public decimal SaldoDopo { get; set; }
    public DateTime CreatoIl { get; set; } = DateTime.UtcNow;
}
