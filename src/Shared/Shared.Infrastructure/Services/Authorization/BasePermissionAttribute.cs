using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Application.Services.Authorization;
using shop_back.src.Shared.Domain.Enums;

namespace shop_back.src.Shared.Infrastructure.Services.Authorization
{
    public abstract class BasePermissionAttribute : TypeFilterAttribute, IPermissionAttribute
    {
        public IReadOnlyCollection<string> Permissions { get; }
        public PermissionRelation Relation { get; }

        protected BasePermissionAttribute(string[] permissions, PermissionRelation relation)
            : base(typeof(PermissionFilter))
        {
            Permissions = permissions == null ? new List<string>() : new List<string>(permissions);
            Relation = relation;

            // Pass arguments to PermissionFilter via TypeFilterAttribute
            Arguments = new object[]
            {
                Permissions,
                Relation
            };
        }
    }

    public class HasPermissionAnyAttribute : BasePermissionAttribute
    {
        public HasPermissionAnyAttribute(params string[] permissions)
            : base(permissions, PermissionRelation.Or) { }
    }

    public class HasPermissionAllAttribute : BasePermissionAttribute
    {
        public HasPermissionAllAttribute(params string[] permissions)
            : base(permissions, PermissionRelation.And) { }
    }
}
