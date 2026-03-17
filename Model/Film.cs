using System;

namespace FilmAPI.Model;

public class Film
{
    public int Id { get; set; }
    public string Titolo { get; set; } = null!;
    public DateTime DataProduzione { get; set; }
    public int RegistaId { get; set; }
    public Regista? Regista { get; set; }
    public int Durata { get; set; }
    public string? CopertinaPath { get; set; }
    public string? FilmatoPath { get; set; }
}
