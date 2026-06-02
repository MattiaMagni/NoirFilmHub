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
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();
    public DbSet<AccountActionToken> AccountActionTokens => Set<AccountActionToken>();
    public DbSet<ExternalAuthState> ExternalAuthStates => Set<ExternalAuthState>();
    public DbSet<ExternalAuthExchangeCode> ExternalAuthExchangeCodes => Set<ExternalAuthExchangeCode>();
    public DbSet<UserSecurityAuditLog> UserSecurityAuditLogs => Set<UserSecurityAuditLog>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Product> Prodotti => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<GiftCardTemplate> GiftCardTemplates => Set<GiftCardTemplate>();
    public DbSet<GiftCard> GiftCards => Set<GiftCard>();
    public DbSet<GiftCardTransaction> GiftCardTransactions => Set<GiftCardTransaction>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<NotificationSubscription> NotificationSubscriptions => Set<NotificationSubscription>();
    public DbSet<RitiroOrdine> RitiriOrdine => Set<RitiroOrdine>();

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
            .HasIndex(u => u.NormalizedEmail)
            .IsUnique();

        modelBuilder.Entity<Utente>()
            .HasIndex(u => u.Ruolo);

        modelBuilder.Entity<Utente>()
            .Property(u => u.Ruolo)
            .HasMaxLength(32);

        modelBuilder.Entity<Utente>()
            .Property(u => u.PasswordHash)
            .IsRequired(false);

        modelBuilder.Entity<Utente>()
            .Property(u => u.CreditoPiattaforma)
            .HasPrecision(10, 2);

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

        // --- Iteration 5: Identity & Security ---

        modelBuilder.Entity<UserExternalLogin>()
            .HasOne(el => el.Utente)
            .WithMany(u => u.ExternalLogins)
            .HasForeignKey(el => el.UtenteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserExternalLogin>()
            .HasIndex(el => new { el.Provider, el.ProviderKey })
            .IsUnique();

        modelBuilder.Entity<UserExternalLogin>()
            .HasIndex(el => new { el.Provider, el.TenantId, el.ProviderKey });

        modelBuilder.Entity<AccountActionToken>()
            .HasOne(t => t.Utente)
            .WithMany()
            .HasForeignKey(t => t.UtenteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AccountActionToken>()
            .HasIndex(t => t.TokenHash)
            .IsUnique();

        modelBuilder.Entity<ExternalAuthState>()
            .HasKey(s => s.Id);

        modelBuilder.Entity<ExternalAuthExchangeCode>()
            .HasIndex(e => e.CodeHash)
            .IsUnique();

        modelBuilder.Entity<UserSecurityAuditLog>()
            .HasOne(l => l.Utente)
            .WithMany()
            .HasForeignKey(l => l.UtenteId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<UserSecurityAuditLog>()
            .HasIndex(l => new { l.UtenteId, l.CreatedAtUtc });

        modelBuilder.Entity<UserSecurityAuditLog>()
            .HasIndex(l => new { l.EventType, l.CreatedAtUtc });

        modelBuilder.Entity<UserSecurityAuditLog>()
            .HasIndex(l => l.CreatedAtUtc);

        // --- Iteration 5.1: E-Commerce ---

        modelBuilder.Entity<Cart>()
            .HasIndex(c => new { c.UtenteId, c.Stato });
        modelBuilder.Entity<Cart>()
            .HasIndex(c => new { c.GuestToken, c.Stato });
        modelBuilder.Entity<Cart>()
            .HasIndex(c => c.ExpiresAtUtc);
        modelBuilder.Entity<Cart>()
            .HasOne(c => c.Utente)
            .WithMany()
            .HasForeignKey(c => c.UtenteId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Cart>()
            .HasOne(c => c.Coupon)
            .WithMany()
            .HasForeignKey(c => c.CouponId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Cart>()
            .Property(c => c.Subtotale).HasPrecision(10, 2);
        modelBuilder.Entity<Cart>()
            .Property(c => c.ScontoCoupon).HasPrecision(10, 2);
        modelBuilder.Entity<Cart>()
            .Property(c => c.Totale).HasPrecision(10, 2);

        modelBuilder.Entity<CartItem>()
            .HasOne(ci => ci.Cart)
            .WithMany(c => c.CartItems)
            .HasForeignKey(ci => ci.CartId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CartItem>()
            .Property(ci => ci.PrezzoUnitario).HasPrecision(10, 2);

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Sku).IsUnique();

        modelBuilder.Entity<ProductVariant>()
            .HasOne(v => v.Product)
            .WithMany(p => p.Varianti)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProductVariant>()
            .HasIndex(v => v.Sku).IsUnique();

        modelBuilder.Entity<GiftCard>()
            .HasIndex(gc => gc.Codice).IsUnique();
        modelBuilder.Entity<GiftCard>()
            .HasOne(gc => gc.UtenteAcquirente)
            .WithMany()
            .HasForeignKey(gc => gc.UtenteAcquirenteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GiftCardTransaction>()
            .HasOne(gt => gt.GiftCard)
            .WithMany(gc => gc.Transazioni)
            .HasForeignKey(gt => gt.GiftCardId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<GiftCardTransaction>()
            .HasOne(gt => gt.Cart)
            .WithMany()
            .HasForeignKey(gt => gt.CartId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GiftCardTransaction>()
            .Property(gt => gt.Importo).HasPrecision(10, 2);
        modelBuilder.Entity<GiftCardTransaction>()
            .Property(gt => gt.SaldoDopo).HasPrecision(10, 2);

        modelBuilder.Entity<Coupon>()
            .HasIndex(c => c.Codice).IsUnique();

        modelBuilder.Entity<CouponUsage>()
            .HasOne(cu => cu.Coupon)
            .WithMany(c => c.Utilizzi)
            .HasForeignKey(cu => cu.CouponId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CouponUsage>()
            .HasOne(cu => cu.Utente)
            .WithMany()
            .HasForeignKey(cu => cu.UtenteId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CouponUsage>()
            .HasOne(cu => cu.Cart)
            .WithMany()
            .HasForeignKey(cu => cu.CartId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CouponUsage>()
            .HasIndex(cu => new { cu.CouponId, cu.UtenteId, cu.CartId }).IsUnique();

        modelBuilder.Entity<InventoryReservation>()
            .HasOne(ir => ir.ProductVariant)
            .WithMany()
            .HasForeignKey(ir => ir.ProductVariantId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InventoryReservation>()
            .HasOne(ir => ir.Cart)
            .WithMany(c => c.InventoryReservations)
            .HasForeignKey(ir => ir.CartId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InventoryReservation>()
            .HasIndex(ir => ir.ExpiresAtUtc);

        modelBuilder.Entity<NotificationSubscription>()
            .HasOne(ns => ns.Utente)
            .WithMany()
            .HasForeignKey(ns => ns.UtenteId)
            .OnDelete(DeleteBehavior.Cascade);

        // SeatLock CartId FK
        modelBuilder.Entity<SeatLock>()
            .HasOne(s => s.Cart)
            .WithMany()
            .HasForeignKey(s => s.CartId)
            .OnDelete(DeleteBehavior.SetNull);

        // Prenotazione CartId FK
        modelBuilder.Entity<Prenotazione>()
            .HasOne(p => p.Cart)
            .WithMany(c => c.Prenotazioni)
            .HasForeignKey(p => p.CartId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<RitiroOrdine>()
            .HasIndex(r => r.CodiceRitiro)
            .IsUnique();

        modelBuilder.Entity<RitiroOrdine>()
            .HasOne(r => r.Cart)
            .WithMany()
            .HasForeignKey(r => r.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RitiroOrdine>()
            .HasOne(r => r.RitiratoDaUtente)
            .WithMany()
            .HasForeignKey(r => r.RitiratoDaUtenteId)
            .OnDelete(DeleteBehavior.SetNull);

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
