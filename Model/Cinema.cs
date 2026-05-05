namespace FilmAPI.Model;

public class Cinema
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Indirizzo { get; set; } = null!;
    public string Citta { get; set; } = null!;
    public int Capienza { get; set; } = 120;
    public double? Latitudine { get; set; }
    public double? Longitudine { get; set; }
    public string CodiceLocale { get; set; } = string.Empty;
    public bool Attivo { get; set; } = true;
    public ICollection<Proiezione> Proiezioni { get; set; } = new List<Proiezione>();
    public ICollection<Sala> Sale { get; set; } = new List<Sala>();
}
