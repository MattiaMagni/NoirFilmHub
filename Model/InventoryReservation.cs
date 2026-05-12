namespace FilmAPI.Model;

public class InventoryReservation
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public int CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public int Quantita { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatoIl { get; set; } = DateTime.UtcNow;
}
