using Microsoft.AspNetCore.Authorization;
using shop_back.src.Shared.Domain.Enums;
using System.Collections.Generic;

namespace shop_back.src.Shared.Infrastructure.Services.Authorization
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public IReadOnlyCollection<string> Permissions { get; }
        public PermissionRelation Relation { get; }

        public PermissionRequirement(
            IEnumerable<string> permissions,
            PermissionRelation relation
        )
        {
            Permissions = permissions?.ToList() ?? new List<string>();
            Relation = relation;
        }
    }
}
