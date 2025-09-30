using shop_back.src.Shared.Domain.Enums;

namespace shop_back.src.Shared.Application.Services.Authorization
{
    /// <summary>
    /// Contract for permission-based attributes (decorators).
    /// Keeps framework-specific logic in Infrastructure layer.
    /// </summary>
    public interface IPermissionAttribute
    {
        /// <summary>
        /// The permissions required for this attribute.
        /// </summary>
        IReadOnlyCollection<string> Permissions { get; }

        /// <summary>
        /// Defines whether all permissions are required (AND)
        /// or at least one (OR).
        /// </summary>
        PermissionRelation Relation { get; }
    }
}
