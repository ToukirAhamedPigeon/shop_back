using Microsoft.EntityFrameworkCore;

namespace shop_back.src.Shared.Infrastructure.Data
{
    /// <summary>
    /// Creates the permission-group tables if they are missing. The schema is
    /// otherwise kept by hand (Sqls/init.sql), so this runs at startup to make
    /// the feature work on existing databases without a manual step. Every
    /// statement is idempotent. The same SQL is in Sqls/permission_groups.sql.
    /// </summary>
    public static class PermissionGroupSchema
    {
        public const string Sql = @"
CREATE TABLE IF NOT EXISTS permission_groups (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(150) NOT NULL UNIQUE,
    description TEXT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NULL,
    updated_by UUID NULL
);

CREATE TABLE IF NOT EXISTS permission_group_permissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    group_id UUID NOT NULL REFERENCES permission_groups(id) ON DELETE CASCADE,
    permission_id UUID NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (group_id, permission_id)
);

CREATE TABLE IF NOT EXISTS role_permission_groups (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    group_id UUID NOT NULL REFERENCES permission_groups(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (role_id, group_id)
);

CREATE TABLE IF NOT EXISTS model_permission_groups (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    model_id UUID NOT NULL,
    model_name VARCHAR(50) NOT NULL DEFAULT 'User',
    group_id UUID NOT NULL REFERENCES permission_groups(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (model_id, model_name, group_id)
);

CREATE INDEX IF NOT EXISTS ix_model_permission_groups_model ON model_permission_groups (model_id, model_name);
";

        public static async Task EnsureAsync(AppDbContext context, CancellationToken ct = default)
        {
            await context.Database.ExecuteSqlRawAsync(Sql, ct);
        }
    }
}
