using Microsoft.AspNetCore.Mvc;
using shop_back.src.Shared.Domain.Enums;
using System.Collections.Generic;

namespace shop_back.src.Shared.Application.Authorization
{
    public abstract class BasePermissionAttribute : TypeFilterAttribute
    {
        protected BasePermissionAttribute(string[] permissions, PermissionRelation relation)
            : base(typeof(PermissionFilter))
        {
            Arguments = new object[]
            {
                permissions == null ? new List<string>() : new List<string>(permissions),
                relation
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
