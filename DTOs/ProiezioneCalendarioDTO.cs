namespace FilmAPI.DTOs;

public class ProiezioneCalendarioDTO
{
    public string TipologiaSala { get; set; } = string.Empty;
    public List<ProiezioneCalendarioItemDTO> Orari { get; set; } = new();
}

public class ProiezioneCalendarioItemDTO
{
    public int ProiezioneId { get; set; }
    public int SalaId { get; set; }
    public string Ora { get; set; } = string.Empty;
    public decimal Prezzo { get; set; }
}
