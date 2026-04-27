namespace FilmAPI.DTOs;

public class TmdbImportRequestDTO
{
    public List<int> TmdbMovieIds { get; set; } = new();
}
