namespace FilmAPI.DTOs;

public class CinemaDTO
{
    public string Nome { get; set; } = null!;
    public string Indirizzo { get; set; } = null!;
    public string Citta { get; set; } = null!;
    public int Capienza { get; set; }
    public double? Latitudine { get; set; }
    public double? Longitudine { get; set; }
    public string CodiceLocale { get; set; } = string.Empty;
    public bool Attivo { get; set; } = true;
}
