using shop_back.src.Shared.Application.DTOs.PermissionGroups;

namespace shop_back.src.Shared.Application.Services
{
    public interface IPermissionGroupService
    {
        Task<List<PermissionGroupDto>> GetGroupsAsync(string? search, bool activeOnly);
        Task<PermissionGroupDto?> GetGroupAsync(Guid id);
        Task<(bool Success, string Message, Guid? Id)> CreateGroupAsync(SavePermissionGroupRequest request, string? currentUserId);
        Task<(bool Success, string Message)> UpdateGroupAsync(Guid id, SavePermissionGroupRequest request, string? currentUserId);
        Task<(bool Success, string Message)> DeleteGroupAsync(Guid id, string? currentUserId);
    }
}
