namespace FilmAPI.DTOs;

public class SalaCreateDTO
{
    public int CinemaId { get; set; }
    public int NumeroProgressivo { get; set; }
    public string Tipologia { get; set; } = "2D";
    public string Nome { get; set; } = string.Empty;
    public int NumeroFile { get; set; } = 10;
    public int PostiPerFila { get; set; } = 12;
    public string MappaPostiJson { get; set; } = string.Empty;
    public bool Attiva { get; set; } = true;
}
