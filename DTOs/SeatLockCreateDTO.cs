namespace FilmAPI.DTOs;

public class SeatLockCreateDTO
{
    public int ProiezioneId { get; set; }
    public List<string> Posti { get; set; } = new();
    public int? LockMinutes { get; set; }
}
