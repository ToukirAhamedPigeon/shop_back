using System;
using System.Threading.Tasks;
using shop_back.src.Shared.Domain.Enums;

namespace shop_back.src.Shared.Application.Services.Authorization
{
    /// <summary>
    /// Interface abstraction for PermissionHandler.
    /// Infrastructure implementation will use IRolePermissionRepository.
    /// </summary>
    public interface IPermissionHandlerService
    {
        /// <summary>
        /// Checks if the given user satisfies the permission requirement.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="requirement">The permission requirement.</param>
        /// <returns>True if authorized, false otherwise.</returns>
        Task<bool> HandleAsync(Guid userId, IPermissionRequirement requirement);
    }
}
