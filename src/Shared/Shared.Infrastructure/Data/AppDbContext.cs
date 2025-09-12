using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Infrastructure.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        // Shared entities
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<RolePermission> RolePermissions { get; set; } = null!;
        public DbSet<ModelRole> ModelRoles { get; set; } = null!;
        public DbSet<ModelPermission> ModelPermissions { get; set; } = null!;
        public DbSet<UserLog> UserLogs { get; set; } = null!;
        public DbSet<UserTableCombination> UserTableCombinations { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<TranslationKey> TranslationKeys { get; set; } = null!;
        public DbSet<TranslationValue> TranslationValues { get; set; } = null!;


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Composite unique constraint: Role + GuardName
            modelBuilder.Entity<Role>()
                .HasIndex(r => new { r.Name, r.GuardName })
                .IsUnique();

            modelBuilder.Entity<Permission>()
                .HasIndex(p => new { p.Name, p.GuardName })
                .IsUnique();

            // Relationships
            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);

            modelBuilder.Entity<ModelRole>()
                .HasOne(mr => mr.Role)
                .WithMany(r => r.ModelRoles)
                .HasForeignKey(mr => mr.RoleId);

            modelBuilder.Entity<ModelPermission>()
                .HasOne(mp => mp.Permission)
                .WithMany(p => p.ModelPermissions)
                .HasForeignKey(mp => mp.PermissionId);

            modelBuilder.Entity<TranslationKey>()
                .HasIndex(k => new { k.Module, k.Key })
                .IsUnique();

            modelBuilder.Entity<TranslationValue>()
                .HasIndex(v => new { v.KeyId, v.Lang })
                .IsUnique();

            modelBuilder.Entity<TranslationValue>()
                .HasOne(v => v.Key)
                .WithMany(k => k.Values)
                .HasForeignKey(v => v.KeyId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
