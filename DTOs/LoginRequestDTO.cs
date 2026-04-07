namespace FilmAPI.DTOs;

public class LoginRequestDTO
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}
