namespace FilmAPI.DTOs;

public class StripeCheckoutSessionCreateDTO
{
    public int ProiezioneId { get; set; }
    public string PostiSelezionati { get; set; } = string.Empty;
}
