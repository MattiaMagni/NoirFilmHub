namespace FilmAPI.Model;

public class Cinema
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Indirizzo { get; set; } = null!;
    public string Citta { get; set; } = null!;
    public int Capienza { get; set; } = 120;
    public ICollection<Proiezione> Proiezioni { get; set; } = new List<Proiezione>();
}
