using Microsoft.EntityFrameworkCore;
using QMSofts.Identity.Models;

namespace QMSofts.Identity.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AppEntitlement> AppEntitlements => Set<AppEntitlement>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuthAuditRecord> AuthAuditRecords => Set<AuthAuditRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("identity");

        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.NormalizedEmail).IsUnique();
            e.Property(u => u.Email).IsRequired();
            e.Property(u => u.NormalizedEmail).IsRequired();
            e.Property(u => u.Name).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
        });

        b.Entity<Role>(e =>
        {
            e.HasIndex(r => r.Name).IsUnique();
        });

        b.Entity<UserRole>(e =>
        {
            e.HasKey(ur => new { ur.UserId, ur.RoleId });
            e.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AppEntitlement>(e =>
        {
            e.HasIndex(a => new { a.UserId, a.AppKey }).IsUnique();
            e.HasOne(a => a.User)
                .WithMany(u => u.AppEntitlements)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(rt => rt.TokenHash);
            e.HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuthAuditRecord>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).UseIdentityByDefaultColumn();
            e.HasIndex(a => a.OccurredAt);
            e.HasIndex(a => a.UserId);
            e.HasIndex(a => a.EventType);
        });

        base.OnModelCreating(b);
    }
}
