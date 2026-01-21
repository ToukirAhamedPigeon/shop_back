using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using shop_back.src.Shared.Domain.Enums;

namespace shop_back.src.Shared.Infrastructure.Services.Authorization
{
    public abstract class BasePermissionAttribute : AuthorizeAttribute
    {
        protected BasePermissionAttribute(
            IEnumerable<string> permissions,
            PermissionRelation relation
        )
        {
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme;

            Policy = $"PERMISSION:{relation}:{string.Join(",", permissions ?? Array.Empty<string>())}";
        }
    }

    public sealed class HasPermissionAnyAttribute : BasePermissionAttribute
    {
        public HasPermissionAnyAttribute(params string[] permissions)
            : base(permissions, PermissionRelation.Or) { }
    }

    public sealed class HasPermissionAllAttribute : BasePermissionAttribute
    {
        public HasPermissionAllAttribute(params string[] permissions)
            : base(permissions, PermissionRelation.And) { }
    }
}
