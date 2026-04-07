namespace FilmAPI.Model;

public class Categoria
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string? Descrizione { get; set; }
    public ICollection<FilmCategoria> FilmCategorie { get; set; } = new List<FilmCategoria>();
}
