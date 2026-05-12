namespace FilmAPI.Model;

public class GiftCard
{
    public int Id { get; set; }
    public string Codice { get; set; } = string.Empty;
    public decimal ImportoIniziale { get; set; }
    public decimal SaldoResiduo { get; set; }
    public int UtenteAcquirenteId { get; set; }
    public Utente UtenteAcquirente { get; set; } = null!;
    public string? EmailDestinatario { get; set; }
    public string? Messaggio { get; set; }
    public DateTime? Scadenza { get; set; }
    public string Stato { get; set; } = "Active";
    public DateTime CreatoIl { get; set; } = DateTime.UtcNow;

    public ICollection<GiftCardTransaction> Transazioni { get; set; } = new List<GiftCardTransaction>();
}
