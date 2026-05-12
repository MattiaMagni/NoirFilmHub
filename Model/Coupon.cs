namespace FilmAPI.Model;

public class Coupon
{
    public int Id { get; set; }
    public string Codice { get; set; } = string.Empty;
    public string TipoSconto { get; set; } = "Fisso";
    public decimal ValoreSconto { get; set; }
    public decimal? ScontoMassimo { get; set; }
    public string TipoTarget { get; set; } = "Carrello";
    public int? TargetId { get; set; }
    public int QuantitaMinima { get; set; } = 1;
    public DateTime ValidoDal { get; set; }
    public DateTime ValidoAl { get; set; }
    public int MaxUtilizzi { get; set; }
    public int UtilizziAttuali { get; set; }
    public int MaxPerUtente { get; set; } = 1;
    public decimal? MinImportoCarrello { get; set; }
    public bool Stackable { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime CreatoIl { get; set; } = DateTime.UtcNow;

    public ICollection<CouponUsage> Utilizzi { get; set; } = new List<CouponUsage>();
}
