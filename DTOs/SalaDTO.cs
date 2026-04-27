namespace FilmAPI.DTOs;

public class SalaDTO
{
    public int Id { get; set; }
    public int CinemaId { get; set; }
    public int NumeroProgressivo { get; set; }
    public string Tipologia { get; set; } = "2D";
    public string Nome { get; set; } = string.Empty;
    public int NumeroFile { get; set; }
    public int PostiPerFila { get; set; }
    public string MappaPostiJson { get; set; } = string.Empty;
    public bool Attiva { get; set; }
}
