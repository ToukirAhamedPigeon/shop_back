using Microsoft.AspNetCore.Authorization;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Enums;
using System.Security.Claims;

namespace shop_back.src.Shared.Infrastructure.Services.Authorization
{
    public class PermissionHandlerService
        : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IRolePermissionRepository _rolePermissionRepo;

        public PermissionHandlerService(IRolePermissionRepository rolePermissionRepo)
        {
            _rolePermissionRepo = rolePermissionRepo;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement
        )
        {
            if (!context.User.Identity?.IsAuthenticated ?? true)
                return;

            var userIdClaim =
                context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                context.User.FindFirst("sub")?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId))
                return;

            // 🔹 1️⃣ Try JWT claims first (FAST)
            var claimPermissions = context.User
                .FindAll("permission")
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool authorized;

            if (claimPermissions.Any())
            {
                authorized = requirement.Relation switch
                {
                    PermissionRelation.Or  => requirement.Permissions.Any(p => claimPermissions.Contains(p)),
                    PermissionRelation.And => requirement.Permissions.All(p => claimPermissions.Contains(p)),
                    _ => false
                };
            }
            else
            {
                // 🔹 2️⃣ Fallback to DB (SAFE)
                var dbPermissions =
                    await _rolePermissionRepo.GetAllPermissionsByUserIdAsync(userId)
                    ?? Array.Empty<string>();

                authorized = requirement.Relation switch
                {
                    PermissionRelation.Or  => requirement.Permissions.Any(p => dbPermissions.Contains(p)),
                    PermissionRelation.And => requirement.Permissions.All(p => dbPermissions.Contains(p)),
                    _ => false
                };
            }

            if (authorized)
                context.Succeed(requirement);
        }
    }
}
