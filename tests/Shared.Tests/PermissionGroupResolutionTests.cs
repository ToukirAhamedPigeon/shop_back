using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Infrastructure.Repositories;
using Xunit;

namespace Shared.Tests;

/// <summary>
/// Checks how permission groups reach a user, against a real PostgreSQL
/// database. Runs only when SHOP_TEST_PG holds a connection string to a
/// throwaway database (it is dropped and recreated); otherwise each test
/// returns straight away.
/// </summary>
public class PermissionGroupResolutionTests
{
    private static readonly string? Conn = Environment.GetEnvironmentVariable("SHOP_TEST_PG");

    private static AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Conn).Options);

    private record Seed(Guid UserId, Guid RoleId, Guid GroupViaRole, Guid GroupDirect, Guid GroupInactive);

    /// <summary>
    /// Fresh schema from the EF model, then the group tables are dropped and
    /// rebuilt from PermissionGroupSchema, so the hand-written SQL is what the
    /// test runs against.
    /// </summary>
    private static async Task<Seed> ResetAsync()
    {
        await using (var db = NewContext())
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync(
                "DROP TABLE model_permission_groups, role_permission_groups, permission_group_permissions, permission_groups;");
            await PermissionGroupSchema.EnsureAsync(db);
            await PermissionGroupSchema.EnsureAsync(db); // idempotent
        }

        await using var ctx = NewContext();
        Permission P(string name) => new() { Name = name };
        var p = new Dictionary<string, Permission>
        {
            ["read-admin-mails"] = P("read-admin-mails"),
            ["create-admin-mails"] = P("create-admin-mails"),
            ["read-admin-users"] = P("read-admin-users"),
            ["read-admin-backups"] = P("read-admin-backups"),
            ["read-admin-roles"] = P("read-admin-roles"),
        };
        ctx.Permissions.AddRange(p.Values);

        var user = new User { Name = "Rahim", Username = "rahim", Email = "rahim@shop.test", Password = "x", MobileNo = "01700000000" };
        var role = new Role { Name = "support" };
        ctx.Users.Add(user);
        ctx.Roles.Add(role);
        ctx.ModelRoles.Add(new ModelRole { ModelId = user.Id, RoleId = role.Id });
        // The role's own permission.
        ctx.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = p["read-admin-roles"].Id });

        var mail = new PermissionGroup { Name = "Mail manager" };
        var users = new PermissionGroup { Name = "User viewer" };
        var backups = new PermissionGroup { Name = "Backups", IsActive = false };
        ctx.PermissionGroups.AddRange(mail, users, backups);
        await ctx.SaveChangesAsync();

        var repo = new PermissionGroupRepository(ctx);
        await repo.SetPermissionsAsync(mail.Id, new[] { "read-admin-mails", "create-admin-mails", "no-such-permission" });
        await repo.SetPermissionsAsync(users.Id, new[] { "read-admin-users" });
        await repo.SetPermissionsAsync(backups.Id, new[] { "read-admin-backups" });
        await repo.SetGroupsForRoleAsync(role.Id, new[] { "Mail manager" });
        await repo.SetGroupsForUserAsync(user.Id, new[] { "User viewer", "Backups" });
        await repo.SaveChangesAsync();

        return new Seed(user.Id, role.Id, mail.Id, users.Id, backups.Id);
    }

    [Fact]
    public async Task User_gets_permissions_from_role_groups_and_direct_groups_but_not_inactive_ones()
    {
        if (string.IsNullOrEmpty(Conn)) return;
        var seed = await ResetAsync();

        await using var ctx = NewContext();
        var perms = await new RolePermissionRepository(ctx).GetAllPermissionsByUserIdAsync(seed.UserId);

        Assert.Equal(
            new[] { "create-admin-mails", "read-admin-mails", "read-admin-roles", "read-admin-users" },
            perms.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Role_permissions_include_its_groups()
    {
        if (string.IsNullOrEmpty(Conn)) return;
        await ResetAsync();

        await using var ctx = NewContext();
        var perms = await new RolePermissionRepository(ctx).GetPermissionsByRolesAsync(new[] { "support" });

        Assert.Equal(new[] { "create-admin-mails", "read-admin-mails", "read-admin-roles" }, perms.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Editing_a_group_changes_everyone_who_has_it()
    {
        if (string.IsNullOrEmpty(Conn)) return;
        var seed = await ResetAsync();

        await using (var ctx = NewContext())
        {
            var repo = new PermissionGroupRepository(ctx);
            await repo.SetPermissionsAsync(seed.GroupViaRole, new[] { "read-admin-mails" });
            await repo.SaveChangesAsync();
        }

        await using var check = NewContext();
        var perms = await new RolePermissionRepository(check).GetAllPermissionsByUserIdAsync(seed.UserId);
        Assert.Contains("read-admin-mails", perms);
        Assert.DoesNotContain("create-admin-mails", perms);
    }

    [Fact]
    public async Task Inactive_role_or_user_gets_nothing_through_groups()
    {
        if (string.IsNullOrEmpty(Conn)) return;
        var seed = await ResetAsync();

        await using (var ctx = NewContext())
        {
            var role = await ctx.Roles.FirstAsync(r => r.Id == seed.RoleId);
            role.IsActive = false;
            await ctx.SaveChangesAsync();
            var perms = await new RolePermissionRepository(ctx).GetGroupPermissionsByUserIdAsync(seed.UserId);
            Assert.Equal(new[] { "read-admin-users" }, perms);
        }

        await using (var ctx = NewContext())
        {
            var user = await ctx.Users.FirstAsync(u => u.Id == seed.UserId);
            user.IsActive = false;
            await ctx.SaveChangesAsync();
            Assert.Empty(await new RolePermissionRepository(ctx).GetGroupPermissionsByUserIdAsync(seed.UserId));
        }
    }

    [Fact]
    public async Task Deleting_a_group_removes_its_links_and_the_access_it_gave()
    {
        if (string.IsNullOrEmpty(Conn)) return;
        var seed = await ResetAsync();

        await using (var ctx = NewContext())
        {
            var repo = new PermissionGroupRepository(ctx);
            var group = await repo.GetByIdAsync(seed.GroupDirect);
            repo.Remove(group!);
            await repo.SaveChangesAsync();
        }

        await using var check = NewContext();
        Assert.False(await check.ModelPermissionGroups.AnyAsync(m => m.GroupId == seed.GroupDirect));
        Assert.False(await check.PermissionGroupPermissions.AnyAsync(m => m.GroupId == seed.GroupDirect));
        var perms = await new RolePermissionRepository(check).GetAllPermissionsByUserIdAsync(seed.UserId);
        Assert.DoesNotContain("read-admin-users", perms);
    }

    [Fact]
    public async Task Group_list_reports_permissions_roles_and_direct_users()
    {
        if (string.IsNullOrEmpty(Conn)) return;
        await ResetAsync();

        await using var ctx = NewContext();
        var repo = new PermissionGroupRepository(ctx);
        var groups = await repo.GetAllAsync(null, activeOnly: false);
        var mail = groups.Single(g => g.Name == "Mail manager");
        Assert.Equal(new[] { "create-admin-mails", "read-admin-mails" }, mail.Permissions);
        Assert.Equal(new[] { "support" }, mail.Roles);
        Assert.Equal(0, mail.UserCount);
        Assert.Equal(1, groups.Single(g => g.Name == "User viewer").UserCount);

        Assert.DoesNotContain(await repo.GetAllAsync(null, activeOnly: true), g => g.Name == "Backups");
        Assert.Single(await repo.GetAllAsync("mail", activeOnly: false));
        Assert.True(await repo.NameExistsAsync("mail MANAGER"));
    }
}
