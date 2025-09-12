using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Enums;
using System.Security.Claims;

namespace shop_back.src.Shared.Application.Authorization
{
    public class PermissionFilter : IAsyncActionFilter
    {
        private readonly IRolePermissionRepository _rolePermissionRepo;
        private readonly List<string> _permissions;
        private readonly PermissionRelation _relation;

        public PermissionFilter(IRolePermissionRepository rolePermissionRepo, List<string> permissions, PermissionRelation relation)
        {
            _rolePermissionRepo = rolePermissionRepo;
            _permissions = permissions ?? new List<string>();
            _relation = relation;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userIdClaim = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                               ?? context.HttpContext.User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var allPermissions = await _rolePermissionRepo.GetAllPermissionsByUserIdAsync(userId);

            bool authorized = _relation switch
            {
                PermissionRelation.Or => _permissions.Any(p => allPermissions.Contains(p)),
                PermissionRelation.And => _permissions.All(p => allPermissions.Contains(p)),
                _ => false
            };

            if (!authorized)
            {
                context.Result = new ForbidResult();
                return;
            }

            await next();
        }
    }
}
