using shop_back.src.Shared.Domain.Enums;
using System.Collections.Generic;

namespace shop_back.src.Shared.Application.Services.Authorization
{
    /// <summary>
    /// Interface abstraction for a permission requirement.
    /// </summary>
    public interface IPermissionRequirement
    {
        IReadOnlyCollection<string> PermissionNames { get; }
        PermissionRelation Relation { get; }
    }
}
