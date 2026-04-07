namespace FilmAPI.Model;

public class FilmCategoria
{
    public int FilmId { get; set; }
    public Film Film { get; set; } = null!;
    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;
}
