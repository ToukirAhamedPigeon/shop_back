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
        public DbSet<Mail> Mails { get; set; } = null!;
        public DbSet<MailTemplate> MailTemplates { get; set; } = null!;
        public DbSet<MailAttachment> MailAttachments { get; set; } = null!;
        public DbSet<MailVerification> MailVerifications { get; set; } = null!;
        public DbSet<Option> Options { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Global query filter for soft delete
            modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<Option>().HasQueryFilter(o => !o.IsDeleted);

            // ============================================
            // UserLog configurations with proper foreign keys
            // ============================================
            var userLog = modelBuilder.Entity<UserLog>();
            userLog.ToTable("user_logs");
            userLog.HasKey(x => x.Id);
            userLog.Property(x => x.Changes).HasColumnType("jsonb");
            userLog.Property(x => x.CreatedAt).HasColumnName("created_at");
            userLog.Property(x => x.CreatedAtId).HasColumnName("created_at_id");
            userLog.Property(x => x.CreatedBy).HasColumnName("created_by");
            userLog.Property(x => x.ActionType).HasColumnName("action_type");
            userLog.Property(x => x.ModelName).HasColumnName("model_name");
            userLog.Property(x => x.ModelId).HasColumnName("model_id");
            userLog.Property(x => x.Detail).HasColumnName("detail");
            userLog.Property(x => x.IpAddress).HasColumnName("ip_address");
            userLog.Property(x => x.Browser).HasColumnName("browser");
            userLog.Property(x => x.Device).HasColumnName("device");
            userLog.Property(x => x.OperatingSystem).HasColumnName("os");
            userLog.Property(x => x.UserAgent).HasColumnName("user_agent");

            // Configure UserLog foreign key with Cascade Delete
            userLog.HasOne<User>()
                .WithMany()
                .HasForeignKey(ul => ul.CreatedBy)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================================
            // RefreshToken configuration - Make optional to avoid filter conflicts
            // ============================================
            modelBuilder.Entity<RefreshToken>()
                .HasOne(r => r.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false); // Make optional to avoid filter issues

            // ============================================
            // PasswordReset configuration - Make optional to avoid filter conflicts
            // ============================================
            modelBuilder.Entity<PasswordReset>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false); // Make optional to avoid filter issues

            // ============================================
            // MailVerification configuration - Make optional to avoid filter conflicts
            // ============================================
            modelBuilder.Entity<MailVerification>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // ============================================
            // Otp configuration - Make optional to avoid filter conflicts
            // ============================================
            modelBuilder.Entity<Otp>()
                .HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // ============================================
            // Mail configuration
            // ============================================
            modelBuilder.Entity<Mail>()
                .HasOne(m => m.CreatedByUser)
                .WithMany()
                .HasForeignKey(m => m.CreatedBy)
                .OnDelete(DeleteBehavior.SetNull);

            // MailTemplate configuration
            modelBuilder.Entity<MailTemplate>(entity =>
            {
                entity.ToTable("mail_templates");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Subject).IsRequired().HasMaxLength(500);
                entity.HasIndex(e => e.Name).IsUnique();
            });

            // MailAttachment configuration
            modelBuilder.Entity<MailAttachment>(entity =>
            {
                entity.ToTable("mail_attachments");
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Mail)
                    .WithMany()
                    .HasForeignKey(e => e.MailId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
    

            // ============================================
            // ModelRole configuration
            // ============================================
            modelBuilder.Entity<ModelRole>()
                .HasOne(mr => mr.Role)
                .WithMany(r => r.ModelRoles)
                .HasForeignKey(mr => mr.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================================
            // ModelPermission configuration
            // ============================================
            modelBuilder.Entity<ModelPermission>()
                .HasOne(mp => mp.Permission)
                .WithMany(p => p.ModelPermissions)
                .HasForeignKey(mp => mp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================================
            // RolePermission configuration
            // ============================================
            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================================
            // Role unique constraint
            // ============================================
            modelBuilder.Entity<Role>()
                .HasIndex(r => new { r.Name, r.GuardName })
                .IsUnique();

            // ============================================
            // Permission unique constraint
            // ============================================
            modelBuilder.Entity<Permission>()
                .HasIndex(p => new { p.Name, p.GuardName })
                .IsUnique();

            // ============================================
            // Translation configurations
            // ============================================
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

            // ============================================
            // UserTableCombination configuration
            // ============================================
            modelBuilder.Entity<UserTableCombination>()
                .Property(e => e.ShowColumnCombinations)
                .HasColumnType("text[]");

            // ============================================
            // Audit field mappings for Role
            // ============================================
            modelBuilder.Entity<Role>()
                .Property(r => r.CreatedBy)
                .HasColumnName("created_by");

            modelBuilder.Entity<Role>()
                .Property(r => r.UpdatedBy)
                .HasColumnName("updated_by");

            modelBuilder.Entity<Role>()
                .Property(r => r.DeletedAt)
                .HasColumnName("deleted_at");

            // ============================================
            // Audit field mappings for Permission
            // ============================================
            modelBuilder.Entity<Permission>()
                .Property(p => p.CreatedBy)
                .HasColumnName("created_by");

            modelBuilder.Entity<Permission>()
                .Property(p => p.UpdatedBy)
                .HasColumnName("updated_by");

            modelBuilder.Entity<Permission>()
                .Property(p => p.DeletedAt)
                .HasColumnName("deleted_at");

            // ============================================
            // Option configuration
            // ============================================
            modelBuilder.Entity<Option>()
                .HasIndex(o => new { o.Name, o.ParentId })
                .IsUnique();

            modelBuilder.Entity<Option>()
                .HasOne(o => o.Parent)
                .WithMany(o => o.Children)
                .HasForeignKey(o => o.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============================================
            // DateTime conversion to UTC for all DateTime properties
            // ============================================
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
        }
    }
}