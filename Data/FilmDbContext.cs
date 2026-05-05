using Microsoft.EntityFrameworkCore;
using FilmAPI.Model;

namespace FilmAPI.Data;

public class FilmDbContext : DbContext
{
    public FilmDbContext(DbContextOptions<FilmDbContext> options) : base(options) { }

    public DbSet<Regista> Registi => Set<Regista>();
    public DbSet<Film> Films => Set<Film>();
    public DbSet<Cinema> Cinemas => Set<Cinema>();
    public DbSet<Sala> Sale => Set<Sala>();
    public DbSet<Proiezione> Proiezioni => Set<Proiezione>();
    public DbSet<Utente> Utenti => Set<Utente>();
    public DbSet<Categoria> Categorie => Set<Categoria>();
    public DbSet<FilmCategoria> FilmCategorie => Set<FilmCategoria>();
    public DbSet<Prenotazione> Prenotazioni => Set<Prenotazione>();
    public DbSet<SeatLock> SeatLocks => Set<SeatLock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Proiezione>()
            .HasIndex(p => new { p.SalaId, p.Data, p.Ora })
            .IsUnique();

        modelBuilder.Entity<Proiezione>()
            .HasIndex(p => new { p.CinemaId, p.FilmId, p.Data, p.Ora });

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

        modelBuilder.Entity<Sala>()
            .HasOne(s => s.Cinema)
            .WithMany(c => c.Sale)
            .HasForeignKey(s => s.CinemaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Sala>()
            .HasIndex(s => new { s.CinemaId, s.NumeroProgressivo })
            .IsUnique();

        modelBuilder.Entity<Proiezione>()
            .HasOne(p => p.Sala)
            .WithMany(s => s.Proiezioni)
            .HasForeignKey(p => p.SalaId)
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

        modelBuilder.Entity<Prenotazione>()
            .Property(p => p.TotalePrezzo)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Prenotazione>()
            .Property(p => p.ImportoCartaUsato)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Prenotazione>()
            .HasIndex(p => p.CodiceAcquisto)
            .IsUnique();

        modelBuilder.Entity<Prenotazione>()
            .HasIndex(p => p.StripeSessionId)
            .IsUnique();

        modelBuilder.Entity<Film>()
            .HasIndex(f => f.TmdbMovieId);

        modelBuilder.Entity<Cinema>()
            .HasIndex(c => c.CodiceLocale)
            .IsUnique();

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

        modelBuilder.Entity<SeatLock>()
            .HasOne(s => s.Proiezione)
            .WithMany()
            .HasForeignKey(s => s.ProiezioneId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SeatLock>()
            .HasOne(s => s.Utente)
            .WithMany()
            .HasForeignKey(s => s.UtenteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SeatLock>()
            .HasIndex(s => new { s.ProiezioneId, s.PostoCodice })
            .IsUnique();

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
