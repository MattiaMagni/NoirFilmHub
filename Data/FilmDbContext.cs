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
            .WithMany()
            .HasForeignKey(p => p.FilmId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
