namespace FilmAPI.Model;

public class Regista
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Cognome { get; set; } = null!;
    public string Nazionalita { get; set; } = null!;
    public ICollection<Film> Films { get; set; } = new List<Film>();
}
