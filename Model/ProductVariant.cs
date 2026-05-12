namespace FilmAPI.Model;

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Nome { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal PrezzoExtra { get; set; }
    public int Stock { get; set; }
    public bool Attivo { get; set; } = true;
}
