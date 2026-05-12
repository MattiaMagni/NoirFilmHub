namespace FilmAPI.Model;

public class Cart
{
    public int Id { get; set; }
    public int? UtenteId { get; set; }
    public Utente? Utente { get; set; }
    public string? GuestToken { get; set; }
    public string CartType { get; set; } = "Mixed";
    public string Stato { get; set; } = "Active";
    public decimal Subtotale { get; set; }
    public decimal ScontoCoupon { get; set; }
    public decimal Totale { get; set; }
    public int? CouponId { get; set; }
    public Coupon? Coupon { get; set; }
    public string? GiftCardCode { get; set; }
    public decimal ImportoGiftCard { get; set; }
    public string? StripeSessionId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<InventoryReservation> InventoryReservations { get; set; } = new List<InventoryReservation>();
    public ICollection<Prenotazione> Prenotazioni { get; set; } = new List<Prenotazione>();
}
