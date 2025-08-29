using Microsoft.EntityFrameworkCore;
using shop_back.App.Models;

namespace shop_back.App.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<ModelRole> ModelRoles { get; set; }
        public DbSet<ModelPermission> ModelPermissions { get; set; }
        public DbSet<UserLog> UserLogs { get; set; }
        public DbSet<UserTableCombination> UserTableCombinations { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<TranslationKey> TranslationKeys { get; set; }
        public DbSet<TranslationValue> TranslationValues { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 👇 Composite unique constraint: Role + GuardName
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
