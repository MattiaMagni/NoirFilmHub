namespace FilmAPI.Model;

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Descrizione { get; set; } = string.Empty;
    public string Categoria { get; set; } = "Gadget";
    public decimal PrezzoBase { get; set; }
    public string? ImmaginePath { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime CreatoIl { get; set; } = DateTime.UtcNow;

    public ICollection<ProductVariant> Varianti { get; set; } = new List<ProductVariant>();
}
