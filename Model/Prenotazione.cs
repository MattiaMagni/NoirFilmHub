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
    public string Stato { get; set; } = "Confermata";
}
