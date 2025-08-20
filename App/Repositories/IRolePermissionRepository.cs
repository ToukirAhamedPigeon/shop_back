using shop_back.App.Models;

namespace shop_back.App.Repositories
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
