namespace FilmAPI.DTOs;

public class InviteUserDTO
{
    public string Email { get; set; } = null!;
    public string Ruolo { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public string Cognome { get; set; } = null!;
    public bool SendSetupEmail { get; set; } = true;
}
