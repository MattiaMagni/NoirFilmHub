using Microsoft.EntityFrameworkCore;
using FilmAPI.Model;

namespace FilmAPI.Data;

public class FilmDbContext : DbContext
{
    public FilmDbContext(DbContextOptions<FilmDbContext> options) : base(options) { }

    public DbSet<Regista> Registi => Set<Regista>();
    public DbSet<Film> Films => Set<Film>();
    public DbSet<Cinema> Cinemas => Set<Cinema>();
    public DbSet<Proiezione> Proiezioni => Set<Proiezione>();
    public DbSet<Utente> Utenti => Set<Utente>();
    public DbSet<Categoria> Categorie => Set<Categoria>();
    public DbSet<FilmCategoria> FilmCategorie => Set<FilmCategoria>();
    public DbSet<Prenotazione> Prenotazioni => Set<Prenotazione>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Proiezione>()
            .HasIndex(p => new { p.CinemaId, p.FilmId, p.Data, p.Ora })
            .IsUnique();

        modelBuilder.Entity<Film>()
            .HasOne(f => f.Regista)
            .WithMany(r => r.Films)
            .HasForeignKey(f => f.RegistaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Proiezione>()
            .HasOne(p => p.Cinema)
            .WithMany(c => c.Proiezioni)
            .HasForeignKey(p => p.CinemaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Proiezione>()
            .HasOne(p => p.Film)
            .WithMany(f => f.Proiezioni)
            .HasForeignKey(p => p.FilmId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Utente>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Utente>()
            .Property(u => u.Ruolo)
            .HasMaxLength(32);

        modelBuilder.Entity<Categoria>()
            .HasIndex(c => c.Nome)
            .IsUnique();

        modelBuilder.Entity<FilmCategoria>()
            .HasKey(fc => new { fc.FilmId, fc.CategoriaId });

        modelBuilder.Entity<FilmCategoria>()
            .HasOne(fc => fc.Film)
            .WithMany(f => f.FilmCategorie)
            .HasForeignKey(fc => fc.FilmId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FilmCategoria>()
            .HasOne(fc => fc.Categoria)
            .WithMany(c => c.FilmCategorie)
            .HasForeignKey(fc => fc.CategoriaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Prenotazione>()
            .HasOne(p => p.Utente)
            .WithMany(u => u.Prenotazioni)
            .HasForeignKey(p => p.UtenteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Prenotazione>()
            .HasOne(p => p.Proiezione)
            .WithMany(pr => pr.Prenotazioni)
            .HasForeignKey(p => p.ProiezioneId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Categoria>().HasData(
            new Categoria { Id = 1, Nome = "Fantasy", Descrizione = "Film fantasy" },
            new Categoria { Id = 2, Nome = "Horror", Descrizione = "Film horror" },
            new Categoria { Id = 3, Nome = "Drammatico", Descrizione = "Film drammatici" },
            new Categoria { Id = 4, Nome = "Commedia", Descrizione = "Film commedia" },
            new Categoria { Id = 5, Nome = "Azione", Descrizione = "Film d'azione" },
            new Categoria { Id = 6, Nome = "Thriller", Descrizione = "Film thriller" }
        );

    }
}
