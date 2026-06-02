namespace FilmAPI.Model;

public class RitiroOrdine
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public string CodiceRitiro { get; set; } = string.Empty;
    public string Stato { get; set; } = "In Attesa";
    public DateTime CreatoIl { get; set; } = DateTime.UtcNow;
    public DateTime? RitiratoIl { get; set; }
    public int? RitiratoDaUtenteId { get; set; }
    public Utente? RitiratoDaUtente { get; set; }
}
