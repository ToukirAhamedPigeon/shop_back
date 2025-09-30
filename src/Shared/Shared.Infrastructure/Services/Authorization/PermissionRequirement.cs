using Microsoft.AspNetCore.Authorization;
using shop_back.src.Shared.Application.Services.Authorization;
using shop_back.src.Shared.Domain.Enums;
using System.Collections.Generic;

namespace shop_back.src.Shared.Infrastructure.Services.Authorization
{
    public class PermissionRequirement : IPermissionRequirement, IAuthorizationRequirement
    {
        public IReadOnlyCollection<string> PermissionNames { get; }
        public PermissionRelation Relation { get; }

        public PermissionRequirement(IEnumerable<string> permissionNames, PermissionRelation relation = PermissionRelation.Or)
        {
            PermissionNames = new List<string>(permissionNames ?? new List<string>());
            Relation = relation;
        }
    }
}
