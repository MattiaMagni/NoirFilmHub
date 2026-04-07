namespace FilmAPI.DTOs;

public class CinemaDTO
{
    public string Nome { get; set; } = null!;
    public string Indirizzo { get; set; } = null!;
    public string Citta { get; set; } = null!;
    public int Capienza { get; set; }
}
