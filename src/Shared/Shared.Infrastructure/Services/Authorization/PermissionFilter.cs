using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using shop_back.src.Shared.Application.Services.Authorization;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Enums;

namespace shop_back.src.Shared.Infrastructure.Services.Authorization
{
    public class PermissionFilter : IAsyncActionFilter
    {
        private readonly IPermissionFilter _permissionService;
        private readonly List<string> _permissions;
        private readonly PermissionRelation _relation;

        public PermissionFilter(IPermissionFilter permissionService, List<string> permissions, PermissionRelation relation)
        {
            _permissionService = permissionService;
            _permissions = permissions ?? new List<string>();
            _relation = relation;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userIdClaim = context.HttpContext.User.FindFirst("sub")?.Value
                              ?? context.HttpContext.User.FindFirst("nameid")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                context.Result = new UnauthorizedResult(); // ✅ requires Microsoft.AspNetCore.Mvc
                return;
            }

            bool authorized = await _permissionService.AuthorizeAsync(userId, _permissions, _relation);

            if (!authorized)
            {
                context.Result = new ForbidResult(); // ✅ requires Microsoft.AspNetCore.Mvc
                return;
            }

            await next();
        }
    }
}
