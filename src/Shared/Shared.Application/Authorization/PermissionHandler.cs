using Microsoft.AspNetCore.Authorization;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Enums;

namespace shop_back.src.Shared.Application.Authorization
{
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IRolePermissionRepository _rolePermissionRepo;

        public PermissionHandler(IRolePermissionRepository rolePermissionRepo)
        {
            _rolePermissionRepo = rolePermissionRepo;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var userIdClaim = context.User.FindFirst("sub")?.Value
                              ?? context.User.FindFirst("nameid")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return;

            var allPermissions = await _rolePermissionRepo.GetAllPermissionsByUserIdAsync(userId);

            bool authorized = requirement.Relation switch
            {
                PermissionRelation.Or => requirement.PermissionNames.Any(p => allPermissions.Contains(p)),
                PermissionRelation.And => requirement.PermissionNames.All(p => allPermissions.Contains(p)),
                _ => false
            };

            if (authorized)
            {
                context.Succeed(requirement);
            }
        }
    }
}
