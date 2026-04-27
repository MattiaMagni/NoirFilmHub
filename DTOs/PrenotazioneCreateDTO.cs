namespace FilmAPI.DTOs;

public class PrenotazioneCreateDTO
{
    public int ProiezioneId { get; set; }
    public int NumeroPosti { get; set; }
    public string PostiSelezionati { get; set; } = string.Empty;
}
