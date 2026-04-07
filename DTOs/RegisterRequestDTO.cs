namespace FilmAPI.DTOs;

public class RegisterRequestDTO
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public string Cognome { get; set; } = null!;
    public string Telefono { get; set; } = "";
}
