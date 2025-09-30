using Microsoft.AspNetCore.Authorization;
using shop_back.src.Shared.Application.Services.Authorization;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Enums;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Infrastructure.Services.Authorization
{
    public class PermissionHandlerService : IPermissionHandlerService, IAuthorizationHandler
    {
        private readonly IRolePermissionRepository _rolePermissionRepo;

        public PermissionHandlerService(IRolePermissionRepository rolePermissionRepo)
        {
            _rolePermissionRepo = rolePermissionRepo;
        }

        // Application Layer method
        public async Task<bool> HandleAsync(Guid userId, IPermissionRequirement requirement)
        {
            var allPermissions = await _rolePermissionRepo.GetAllPermissionsByUserIdAsync(userId);

            return requirement.Relation switch
            {
                PermissionRelation.Or => requirement.PermissionNames.Any(p => allPermissions.Contains(p)),
                PermissionRelation.And => requirement.PermissionNames.All(p => allPermissions.Contains(p)),
                _ => false
            };
        }

        // ASP.NET Core AuthorizationHandler implementation
        public async Task HandleAsync(AuthorizationHandlerContext context)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? context.User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                context.Fail();
                return;
            }

            foreach (var requirement in context.PendingRequirements.OfType<PermissionRequirement>())
            {
                bool authorized = await HandleAsync(userId, requirement); // uses IPermissionRequirement interface
                if (authorized)
                    context.Succeed(requirement); // succeeds as IAuthorizationRequirement
            }
        }
    }
}
