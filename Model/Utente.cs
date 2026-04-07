namespace FilmAPI.Model;

public class Utente
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public string Cognome { get; set; } = null!;
    public string Telefono { get; set; } = "";
    public string Ruolo { get; set; } = RuoloUtente.Utente;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public ICollection<Prenotazione> Prenotazioni { get; set; } = new List<Prenotazione>();
}
