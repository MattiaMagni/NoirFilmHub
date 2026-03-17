using System;

namespace FilmAPI.DTOs;

public class FilmDTO
{
    public string Titolo { get; set; } = null!;
    public DateTime DataProduzione { get; set; }
    public int RegistaId { get; set; }
    public int Durata { get; set; }
    public string? CopertinaPath { get; set; }
    public string? FilmatoPath { get; set; }
}
