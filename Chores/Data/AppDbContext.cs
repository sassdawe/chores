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
    public DbSet<HouseholdInvite> HouseholdInvites => Set<HouseholdInvite>();
    public DbSet<HouseholdMembership> HouseholdMemberships => Set<HouseholdMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.LoginName)
            .IsUnique();

        modelBuilder.Entity<FidoCredential>()
            .HasIndex(c => c.CredentialId)
            .IsUnique();

        modelBuilder.Entity<HouseholdMembership>()
            .HasIndex(m => new { m.UserId, m.HouseholdId })
            .IsUnique();

        modelBuilder.Entity<HouseholdMembership>()
            .HasOne(m => m.User)
            .WithMany(u => u.HouseholdMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HouseholdMembership>()
            .HasOne(m => m.Household)
            .WithMany(h => h.Memberships)
            .HasForeignKey(m => m.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HouseholdInvite>()
            .HasIndex(i => i.LoginName);

        modelBuilder.Entity<HouseholdInvite>()
            .HasIndex(i => new { i.HouseholdId, i.LoginName })
            .HasDatabaseName("IX_HouseholdInvites_PendingHouseholdLoginName")
            .HasFilter($"{nameof(HouseholdInvite.AcceptedAtUtc)} IS NULL AND {nameof(HouseholdInvite.DeclinedAtUtc)} IS NULL")
            .IsUnique();

        modelBuilder.Entity<HouseholdInvite>()
            .HasOne(i => i.InvitedByUser)
            .WithMany(u => u.SentInvites)
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

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
