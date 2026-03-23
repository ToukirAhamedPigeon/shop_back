using System;
using System.Threading.Tasks;
using shop_back.src.Shared.Application.DTOs.Roles;
using shop_back.src.Shared.Application.DTOs.Common;

namespace shop_back.src.Shared.Application.Services
{
    public interface IRoleService
    {
        Task<object> GetRolesAsync(RolePermissionFilterRequest request);
        Task<RoleDto?> GetRoleAsync(Guid id);
        Task<RoleDto?> GetRoleForEditAsync(Guid id);
        Task<(bool Success, string Message)> CreateRoleAsync(CreateRoleRequest request, string? createdBy);
        Task<(bool Success, string Message)> UpdateRoleAsync(Guid id, UpdateRoleRequest request, string? updatedBy);
        Task<(bool Success, string Message, string DeleteType)> DeleteRoleAsync(Guid id, bool permanent, string? currentUserId);
        Task<(bool Success, string Message)> RestoreRoleAsync(Guid id, string? currentUserId);
        Task<(bool Success, string Message, bool CanBePermanent)> CheckDeleteEligibilityAsync(Guid id);
    }
}