using Chores.Models;
using Microsoft.EntityFrameworkCore;

namespace Chores.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<FidoCredential> FidoCredentials => Set<FidoCredential>();
    public DbSet<Chore> Chores => Set<Chore>();
    public DbSet<CompletionRecord> CompletionRecords => Set<CompletionRecord>();
    public DbSet<Label> Labels => Set<Label>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.LoginName)
            .IsUnique();

        modelBuilder.Entity<FidoCredential>()
            .HasIndex(c => c.CredentialId)
            .IsUnique();

        // CompletionRecord is append-only; no cascade delete from Chore
        modelBuilder.Entity<CompletionRecord>()
            .HasOne(r => r.Chore)
            .WithMany(c => c.CompletionRecords)
            .HasForeignKey(r => r.ChoreId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Chore>()
            .HasMany(c => c.Labels)
            .WithMany(l => l.Chores)
            .UsingEntity(j => j.ToTable("ChoreLabels"));
    }
}
