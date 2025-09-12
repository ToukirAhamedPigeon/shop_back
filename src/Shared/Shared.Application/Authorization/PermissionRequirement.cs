using Microsoft.AspNetCore.Authorization;
using shop_back.src.Shared.Domain.Enums;

namespace shop_back.src.Shared.Application.Authorization
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public List<string> PermissionNames { get; }
        public PermissionRelation Relation { get; }

        public PermissionRequirement(List<string> permissionNames, PermissionRelation relation = PermissionRelation.Or)
        {
            PermissionNames = permissionNames ?? new List<string>();
            Relation = relation;
        }
    }
}
