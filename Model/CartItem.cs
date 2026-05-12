namespace FilmAPI.Model;

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public string ItemType { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public int? VariantId { get; set; }
    public int Quantita { get; set; } = 1;
    public decimal PrezzoUnitario { get; set; }
    public string? DettaglioJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
