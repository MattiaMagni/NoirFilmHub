using System;

namespace FilmAPI.DTOs;

public class FilmDTO
{
    public string Titolo { get; set; } = null!;
    public string TitoloOriginale { get; set; } = string.Empty;
    public DateTime DataProduzione { get; set; }
    public DateTime? DataUscita { get; set; }
    public int RegistaId { get; set; }
    public int Durata { get; set; }
    public string? CopertinaPath { get; set; }
    public string? BackdropPath { get; set; }
    public string? FilmatoPath { get; set; }
    public string DescrizioneLunga { get; set; } = string.Empty;
    public string CastPrincipale { get; set; } = string.Empty;
    public int? TmdbMovieId { get; set; }
    public List<int> CategorieIds { get; set; } = new();
}
