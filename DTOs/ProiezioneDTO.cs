using System;

namespace FilmAPI.DTOs;

public class ProiezioneDTO
{
    public int Id { get; set; }
    public DateTime Data { get; set; }
    public DateTime Ora { get; set; }
    public int FilmId { get; set; }
    public int CinemaId { get; set; }
    public int SalaId { get; set; }
    public string TipologiaSala { get; set; } = string.Empty;
    public decimal PrezzoBase { get; set; }
}
