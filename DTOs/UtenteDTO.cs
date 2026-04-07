namespace FilmAPI.DTOs;

public class UtenteDTO
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public string Cognome { get; set; } = null!;
    public string Telefono { get; set; } = "";
    public string Ruolo { get; set; } = null!;
}
