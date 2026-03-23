using System;
using System.Threading.Tasks;
using shop_back.src.Shared.Application.DTOs.Permissions;
using shop_back.src.Shared.Application.DTOs.Common;

namespace shop_back.src.Shared.Application.Services
{
    public interface IPermissionService
    {
        Task<object> GetPermissionsAsync(RolePermissionFilterRequest request);
        Task<PermissionDto?> GetPermissionAsync(Guid id);
        Task<PermissionDto?> GetPermissionForEditAsync(Guid id);
        Task<(bool Success, string Message)> CreatePermissionAsync(CreatePermissionRequest request, string? createdBy);
        Task<(bool Success, string Message)> UpdatePermissionAsync(Guid id, UpdatePermissionRequest request, string? updatedBy);
        Task<(bool Success, string Message, string DeleteType)> DeletePermissionAsync(Guid id, bool permanent, string? currentUserId);
        Task<(bool Success, string Message)> RestorePermissionAsync(Guid id, string? currentUserId);
        Task<(bool Success, string Message, bool CanBePermanent)> CheckDeleteEligibilityAsync(Guid id);
    }
}