using System;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IRolePermissionRepository
    {
        Task<object[]> GetPermissionsByRoleIdAsync(Guid roleId);
        Task<object[]> GetRolesByPermissionIdAsync(Guid permissionId);
        Task<string[]> GetRoleNamesByUserIdAsync(Guid userId);
        Task<string[]> GetRolePermissionsByUserIdAsync(Guid userId);
        Task<string[]> GetModelPermissionsByUserIdAsync(Guid userId);
        Task<string[]> GetAllPermissionsByUserIdAsync(Guid userId);
    }
}
