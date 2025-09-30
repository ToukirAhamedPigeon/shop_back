using shop_back.src.Shared.Domain.Enums;

namespace shop_back.src.Shared.Application.Services.Authorization
{
    /// <summary>
    /// Abstraction for permission filters that can be applied
    /// to web requests (e.g., ASP.NET Core action filters).
    /// </summary>
    public interface IPermissionFilter
    {
        /// <summary>
        /// Authorize a user against a set of permissions and relation.
        /// </summary>
        /// <param name="userId">The ID of the user being authorized.</param>
        /// <param name="permissions">The permissions required.</param>
        /// <param name="relation">The relation type (And/Or).</param>
        /// <returns>True if authorized; otherwise false.</returns>
        Task<bool> AuthorizeAsync(Guid userId, IEnumerable<string> permissions, PermissionRelation relation);
    }
}
