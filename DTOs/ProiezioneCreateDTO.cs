using System;

namespace FilmAPI.DTOs;

public class ProiezioneCreateDTO
{
    public DateTime Data { get; set; }
    public DateTime Ora { get; set; }
    public int FilmId { get; set; }
    public int CinemaId { get; set; }
    public int SalaId { get; set; }
    public decimal PrezzoBase { get; set; } = 8.90m;
}
