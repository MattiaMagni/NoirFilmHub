using FilmAPI.Data;
using FilmAPI.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace FilmAPI.Endpoints;

public static class ShopEndpoints
{
    public static RouteGroupBuilder MapShop(this RouteGroupBuilder group)
    {
        group.MapGet("/products", async (FilmDbContext db) =>
        {
            var products = await db.Prodotti
                .Where(p => p.Attivo)
                .Include(p => p.Varianti.Where(v => v.Attivo))
                .OrderBy(p => p.Categoria)
                .ThenBy(p => p.Nome)
                .ToListAsync();

            return Results.Ok(products.Select(p => new
            {
                p.Id, p.Sku, p.Nome, p.Descrizione, p.Categoria, p.PrezzoBase, p.ImmaginePath,
                Varianti = p.Varianti.Select(v => new
                {
                    v.Id, v.Nome, v.Sku, v.PrezzoExtra, v.Stock,
                    PrezzoFinale = p.PrezzoBase + v.PrezzoExtra
                })
            }));
        });

        group.MapGet("/products/{id:int}", async (int id, FilmDbContext db) =>
        {
            var product = await db.Prodotti
                .Include(p => p.Varianti.Where(v => v.Attivo))
                .FirstOrDefaultAsync(p => p.Id == id && p.Attivo);

            if (product == null) return Results.NotFound();
            return Results.Ok(new
            {
                product.Id, product.Sku, product.Nome, product.Descrizione, product.Categoria, product.PrezzoBase, product.ImmaginePath,
                Varianti = product.Varianti.Select(v => new
                {
                    v.Id, v.Nome, v.Sku, v.PrezzoExtra, v.Stock,
                    PrezzoFinale = product.PrezzoBase + v.PrezzoExtra
                })
            });
        });

        group.MapGet("/giftcard-templates", async (FilmDbContext db) =>
        {
            var templates = await db.GiftCardTemplates
                .Where(t => t.Attivo)
                .OrderBy(t => t.Importo)
                .ToListAsync();

            return Results.Ok(templates.Select(t => new
            {
                t.Id, t.Nome, t.Importo, t.ImmaginePath
            }));
        });

        group.MapPost("/products", async (Product product, FilmDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(product.Sku) || string.IsNullOrWhiteSpace(product.Nome))
                return Results.BadRequest(new { error = "SKU e Nome sono obbligatori" });

            db.Prodotti.Add(product);
            await db.SaveChangesAsync();
            return Results.Created($"/shop/products/{product.Id}", product);
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/products/{id:int}", async (int id, Product updated, FilmDbContext db) =>
        {
            var product = await db.Prodotti.FindAsync(id);
            if (product == null) return Results.NotFound();

            product.Sku = updated.Sku;
            product.Nome = updated.Nome;
            product.Descrizione = updated.Descrizione;
            product.Categoria = updated.Categoria;
            product.PrezzoBase = updated.PrezzoBase;
            product.ImmaginePath = updated.ImmaginePath;
            product.Attivo = updated.Attivo;

            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        group.MapDelete("/products/{id:int}", async (int id, FilmDbContext db) =>
        {
            var product = await db.Prodotti.FindAsync(id);
            if (product == null) return Results.NotFound();
            product.Attivo = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        return group;
    }
}
