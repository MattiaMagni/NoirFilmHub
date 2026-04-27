namespace FilmAPI.DTOs;

public class PrenotazioneDTO
{
    public int Id { get; set; }
    public int UtenteId { get; set; }
    public int ProiezioneId { get; set; }
    public int FilmId { get; set; }
    public string TitoloFilm { get; set; } = null!;
    public int CinemaId { get; set; }
    public string NomeCinema { get; set; } = null!;
    public int SalaId { get; set; }
    public string NomeSala { get; set; } = string.Empty;
    public string TipologiaSala { get; set; } = string.Empty;
    public DateTime Data { get; set; }
    public DateTime Ora { get; set; }
    public int NumeroPosti { get; set; }
    public string PostiSelezionati { get; set; } = string.Empty;
    public decimal TotalePrezzo { get; set; }
    public decimal ImportoCartaUsato { get; set; }
    public string CodiceAcquisto { get; set; } = string.Empty;
    public bool Validato { get; set; }
    public DateTime? ValidatoAtUtc { get; set; }
    public string Stato { get; set; } = null!;
    public DateTime DataPrenotazione { get; set; }
}
