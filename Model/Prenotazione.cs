namespace FilmAPI.Model;

public class Prenotazione
{
    public int Id { get; set; }
    public int UtenteId { get; set; }
    public Utente Utente { get; set; } = null!;
    public int ProiezioneId { get; set; }
    public Proiezione Proiezione { get; set; } = null!;
    public DateTime DataPrenotazione { get; set; } = DateTime.UtcNow;
    public int NumeroPosti { get; set; }
    public string PostiSelezionati { get; set; } = string.Empty;
    public decimal TotalePrezzo { get; set; }
    public decimal ImportoCartaUsato { get; set; }
    public string? StripeSessionId { get; set; }
    public string CodiceAcquisto { get; set; } = string.Empty;
    public bool Validato { get; set; }
    public DateTime? ValidatoAtUtc { get; set; }
    public int? ValidatoDaUtenteId { get; set; }
    public int? CinemaValidazioneId { get; set; }
    public int? CartId { get; set; }
    public Cart? Cart { get; set; }
    public string Stato { get; set; } = "Confermata";
}
