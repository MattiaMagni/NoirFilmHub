using System;

namespace FilmAPI.Model;

public class Film
{
    public int Id { get; set; }
    public string Titolo { get; set; } = null!;
    public string TitoloOriginale { get; set; } = string.Empty;
    public DateTime DataProduzione { get; set; }
    public DateTime? DataUscita { get; set; }
    public int RegistaId { get; set; }
    public Regista? Regista { get; set; }
    public int Durata { get; set; }
    public string? CopertinaPath { get; set; }
    public string? BackdropPath { get; set; }
    public string? FilmatoPath { get; set; }
    public string DescrizioneLunga { get; set; } = string.Empty;
    public string CastPrincipale { get; set; } = string.Empty;
    public int? TmdbMovieId { get; set; }
    public DateTime? UltimaSyncTmdbUtc { get; set; }
    public string TmdbSyncStato { get; set; } = "NotSynced";
    public ICollection<FilmCategoria> FilmCategorie { get; set; } = new List<FilmCategoria>();
    public ICollection<Proiezione> Proiezioni { get; set; } = new List<Proiezione>();
}
