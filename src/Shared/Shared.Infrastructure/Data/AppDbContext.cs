using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Shared entities
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<Otp> Otps { get; set; } = null!;
        public DbSet<PasswordReset> PasswordResets { get; set; } = null!;
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

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(
                            new ValueConverter<DateTime, DateTime>(
                                v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
                                v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
                            ));
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(
                            new ValueConverter<DateTime?, DateTime?>(
                                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v,
                                v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v
                            ));
                    }
                }
            }

            // ✅ No HasConversion needed for bool <-> PostgreSQL boolean

            // RefreshToken -> User relation
            modelBuilder.Entity<RefreshToken>()
                .HasOne(r => r.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Role unique constraint
            modelBuilder.Entity<Role>()
                .HasIndex(r => new { r.Name, r.GuardName })
                .IsUnique();

            // Permission unique constraint
            modelBuilder.Entity<Permission>()
                .HasIndex(p => new { p.Name, p.GuardName })
                .IsUnique();

            // RolePermission relationships
            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);

            // ModelRole relationships
            modelBuilder.Entity<ModelRole>()
                .HasOne(mr => mr.Role)
                .WithMany(r => r.ModelRoles)
                .HasForeignKey(mr => mr.RoleId);

            // ModelPermission relationships
            modelBuilder.Entity<ModelPermission>()
                .HasOne(mp => mp.Permission)
                .WithMany(p => p.ModelPermissions)
                .HasForeignKey(mp => mp.PermissionId);

            // TranslationKey unique index
            modelBuilder.Entity<TranslationKey>()
                .HasIndex(k => new { k.Module, k.Key })
                .IsUnique();

            // TranslationValue unique index
            modelBuilder.Entity<TranslationValue>()
                .HasIndex(v => new { v.KeyId, v.Lang })
                .IsUnique();

            // TranslationValue -> TranslationKey relationship
            modelBuilder.Entity<TranslationValue>()
                .HasOne(v => v.Key)
                .WithMany(k => k.Values)
                .HasForeignKey(v => v.KeyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Otp -> User relationship
            modelBuilder.Entity<Otp>()
                .HasOne(o => o.User)
                .WithMany() // No collection navigation in User (only single side)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // PasswordReset -> User relationship
            modelBuilder.Entity<PasswordReset>()
                .HasOne(pr => pr.User)
                .WithMany() // No collection navigation in User
                .HasForeignKey(pr => pr.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
