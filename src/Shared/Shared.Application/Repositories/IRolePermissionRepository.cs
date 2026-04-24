using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.DTOs.Permissions;
using shop_back.src.Shared.Application.DTOs.Roles;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IRolePermissionRepository
    {
        // Existing methods
        Task<object[]> GetPermissionsByRoleIdAsync(Guid roleId);
        Task<object[]> GetRolesByPermissionIdAsync(Guid permissionId);
        Task<string[]> GetRoleNamesByUserIdAsync(Guid userId);
        Task<string[]> GetRolePermissionsByUserIdAsync(Guid userId);
        Task<string[]> GetModelPermissionsByUserIdAsync(Guid userId);
        Task<string[]> GetAllPermissionsByUserIdAsync(Guid userId);
        Task<string[]> GetAllRolesAsync();
        Task<string[]> GetAllPermissionsAsync();
        Task AssignRolesAsync(Guid userId, string[] roles);
        Task AssignPermissionsAsync(Guid userId, string[] permissions);
        Task SetRolesForUserAsync(Guid userId, IEnumerable<string> roleNames);
        Task<string[]> GetPermissionsByRolesAsync(IEnumerable<string> roleNames);
        Task SetPermissionsForUserAsync(Guid userId, IEnumerable<string> permissionNames);

        // Role CRUD methods
        Task<(IEnumerable<RoleDto> Roles, int TotalCount, int GrandTotalCount, int PageIndex, int PageSize)> GetFilteredRolesAsync(RolePermissionFilterRequest req);
        Task<Role?> GetRoleByIdAsync(Guid id);
        Task<Role?> GetRoleByIdIncludingDeletedAsync(Guid id);
        Task<bool> RoleExistsAsync(string name, Guid? ignoreId = null);
        Task<Role> CreateRoleAsync(Role role);
        void UpdateRole(Role role);
        Task DeleteRoleAsync(Guid id, bool permanent, Guid? deletedBy);
        Task<bool> RoleHasRelatedRecordsAsync(Guid roleId);
        Task AssignPermissionsToRoleAsync(Guid roleId, IEnumerable<string> permissionNames);
        Task<string[]> GetPermissionsByRoleNamesAsync(IEnumerable<string> roleNames);
        
        // Permission CRUD methods
        Task<(IEnumerable<PermissionDto> Permissions, int TotalCount, int GrandTotalCount, int PageIndex, int PageSize)> GetFilteredPermissionsAsync(RolePermissionFilterRequest req);
        Task<Permission?> GetPermissionByIdAsync(Guid id);
        Task<Permission?> GetPermissionByIdIncludingDeletedAsync(Guid id);
        Task<bool> PermissionExistsAsync(string name, Guid? ignoreId = null);
        Task<Permission> CreatePermissionAsync(Permission permission);
        void UpdatePermission(Permission permission);
        Task DeletePermissionAsync(Guid id, bool permanent, Guid? deletedBy);
        Task<bool> PermissionHasRelatedRecordsAsync(Guid permissionId);
        Task AssignRolesToPermissionAsync(Guid permissionId, IEnumerable<string> roleNames);
        Task<string[]> GetRolesByPermissionNamesAsync(IEnumerable<string> permissionNames);
        
        Task SaveChangesAsync();
    }
}