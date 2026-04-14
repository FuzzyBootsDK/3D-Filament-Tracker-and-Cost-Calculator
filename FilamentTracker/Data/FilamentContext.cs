using FilamentTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace FilamentTracker.Data;

public class FilamentContext : DbContext
{
    public FilamentContext(DbContextOptions<FilamentContext> options) : base(options)
    {
    }

    public DbSet<Filament> Filaments { get; set; }
    public DbSet<Spool> Spools { get; set; }
    public DbSet<ReusableSpool> ReusableSpools { get; set; }
    public DbSet<Brand> Brands { get; set; }
    public DbSet<AppSettings> AppSettings { get; set; }
    public DbSet<Printer> Printers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Filament>()
            .HasMany(f => f.Spools)
            .WithOne(s => s.Filament)
            .HasForeignKey(s => s.FilamentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure ReusableSpool → Spool relationship
        // When a Spool is deleted, set ReusableSpool.CurrentSpoolId to null
        modelBuilder.Entity<ReusableSpool>()
            .HasOne<Spool>()
            .WithMany()
            .HasForeignKey(rs => rs.CurrentSpoolId)
            .OnDelete(DeleteBehavior.SetNull);

        // Seed default brands if needed
        modelBuilder.Entity<Filament>()
            .Property(f => f.Diameter)
            .HasPrecision(4, 2);

        modelBuilder.Entity<Spool>()
            .Property(s => s.TotalWeight)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Spool>()
            .Property(s => s.WeightRemaining)
            .HasPrecision(10, 2);

        // Ensure only one printer can be marked as default
        modelBuilder.Entity<Printer>()
            .HasIndex(p => p.IsDefault)
            .IsUnique()
            .HasFilter("[IsDefault] = 1");
    }
}