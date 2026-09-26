using Microsoft.EntityFrameworkCore;
using TheUno.Infrastructure.Entities;

namespace TheUno.Infrastructure;

public class UnoDbContext : DbContext
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<GameLog> GameLogs => Set<GameLog>();
    
    public UnoDbContext()
    {}

    public UnoDbContext(DbContextOptions<UnoDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Genera la base de datos automáticamente en la carpeta del ejecutable
            optionsBuilder.UseSqlite("Data Source=theuno.db");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configuración de Player
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(50);

            // Semilla de Datos (Seed Data) con los 3 integrantes del equipo
            entity.HasData(
                new Player { Id = 1, Name = "Rodri", CreatedAt = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc) },
                new Player { Id = 2, Name = "Tona", CreatedAt = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc) },
                new Player { Id = 3, Name = "Ale", CreatedAt = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc) }
            );
        });
        
        // Configuración de GameLog
        modelBuilder.Entity<GameLog>(entity =>
            {
                entity.HasKey(g => g.Id);

                entity.Property(g => g.ActionType).IsRequired().HasMaxLength(20);
                entity.Property(g => g.CardDetails).HasMaxLength(30);
                entity.Property(g => g.ColorChosen).HasMaxLength(10);

                // Relación de GameLog con Match
                entity.HasOne(g => g.Match).WithMany(m => m.GameLogs).HasForeignKey(g => g.MatchId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Relación de GameLog con Player
                entity.HasOne(g => g.Player).WithMany(p => p.GameLogs).HasForeignKey(g => g.PlayerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
    }
}

