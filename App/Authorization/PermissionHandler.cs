using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using shop_back.App.Data;
using shop_back.App.Models;
using System.Linq;
using System.Threading.Tasks;

namespace shop_back.App.Authorization
{
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly AppDbContext _db;

        public PermissionHandler(AppDbContext db)
        {
            _db = db;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var userId = context.User.FindFirst("sub")?.Value;
            if (userId == null) return;

            var permissionChecks = requirement.PermissionNames.Select(async permissionName =>
            {
                var parts = permissionName.Split(':');
                var guard = parts.Length > 1 ? parts[0] : "default";
                var permission = parts.Length > 1 ? parts[1] : permissionName;

                // Check direct permissions
                bool hasDirect = await _db.ModelPermissions
                    .Include(mp => mp.Permission)
                    .AnyAsync(mp => mp.ModelId.ToString() == userId &&
                                    mp.Permission != null &&
                                    mp.Permission.Name == permission &&
                                    mp.Permission.GuardName == guard);

                if (hasDirect) return true;

                // Check role-based permissions
                bool hasRole = await _db.ModelRoles
                    .Include(mr => mr.Role)
                    .ThenInclude(r => r!.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                    .AnyAsync(mr => mr.ModelId.ToString() == userId &&
                                    mr.Role != null &&
                                    mr.Role.RolePermissions.Any(rp =>
                                        rp.Permission != null &&
                                        rp.Permission.Name == permission &&
                                        rp.Permission.GuardName == guard));

                return hasRole;
            });

            var results = await Task.WhenAll(permissionChecks);

            // Apply AND / OR logic dynamically
            if (requirement.Relation == PermissionRelation.Or && results.Any(r => r))
            {
                context.Succeed(requirement);
            }
            else if (requirement.Relation == PermissionRelation.And && results.All(r => r))
            {
                context.Succeed(requirement);
            }
        }
    }
}
