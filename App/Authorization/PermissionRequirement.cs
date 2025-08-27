using Microsoft.AspNetCore.Authorization;

namespace shop_back.App.Authorization
{
    public enum PermissionRelation
    {
        And,
        Or
    }

    public class PermissionRequirement : IAuthorizationRequirement
    {
        public List<string> PermissionNames { get; set; } = new List<string>();
        public PermissionRelation Relation { get; set; } = PermissionRelation.Or;

        public PermissionRequirement(List<string> permissionNames, PermissionRelation relation = PermissionRelation.Or)
        {
            PermissionNames = permissionNames;
            Relation = relation;
        }
    }
}
