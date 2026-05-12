namespace FilmAPI.Model;

public class CouponUsage
{
    public int Id { get; set; }
    public int CouponId { get; set; }
    public Coupon Coupon { get; set; } = null!;
    public int UtenteId { get; set; }
    public Utente Utente { get; set; } = null!;
    public int CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public decimal ScontoApplicato { get; set; }
    public DateTime CreatoIl { get; set; } = DateTime.UtcNow;
}
