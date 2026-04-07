using System;

namespace FilmAPI.Model;

public class Proiezione
{
    public int Id { get; set; }
    public int CinemaId { get; set; }
    public Cinema Cinema { get; set; } = null!;
    public int FilmId { get; set; }
    public Film Film { get; set; } = null!;
    public DateTime Data { get; set; }
    public DateTime Ora { get; set; }
    public ICollection<Prenotazione> Prenotazioni { get; set; } = new List<Prenotazione>();
}
