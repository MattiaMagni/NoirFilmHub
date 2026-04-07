namespace FilmAPI.DTOs;

public class LoginResponseDTO
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public UtenteDTO Utente { get; set; } = null!;
}
