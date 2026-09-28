using shop_back.src.Shared.Application.DTOs.PermissionGroups;
using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IPermissionGroupRepository
    {
        Task<List<PermissionGroupDto>> GetAllAsync(string? search, bool activeOnly);
        Task<PermissionGroupDto?> GetDtoAsync(Guid id);
        Task<PermissionGroup?> GetByIdAsync(Guid id);
        Task<bool> NameExistsAsync(string name, Guid? ignoreId = null);
        Task AddAsync(PermissionGroup group);
        void Remove(PermissionGroup group);
        /// <summary>Replaces the group's permissions with these (unknown names are skipped).</summary>
        Task SetPermissionsAsync(Guid groupId, IEnumerable<string> permissionNames);

        Task<string[]> GetGroupNamesByRoleIdAsync(Guid roleId);
        Task SetGroupsForRoleAsync(Guid roleId, IEnumerable<string> groupNames);
        Task<string[]> GetGroupNamesByUserIdAsync(Guid userId);
        Task SetGroupsForUserAsync(Guid userId, IEnumerable<string> groupNames);

        Task SaveChangesAsync();
    }
}
