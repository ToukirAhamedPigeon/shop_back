using Microsoft.EntityFrameworkCore;
using shop_back.App.Data;
using shop_back.App.Models;

namespace shop_back.App.Repositories
{
    public class RolePermissionRepository : IRolePermissionRepository
    {
        private readonly AppDbContext _context;

        public RolePermissionRepository(AppDbContext context)
        {
            _context = context;
        }

        // 1. Permissions from a given Role
        public async Task<object[]> GetPermissionsByRoleIdAsync(Guid roleId)
        {
            return await (from rp in _context.RolePermissions
                        join p in _context.Permissions on rp.PermissionId equals p.Id
                        where rp.RoleId == roleId
                        select new { p.Id, p.Name, p.GuardName })
                        .ToArrayAsync<object>();
        }

        // 2. Roles for a given Permission
        public async Task<object[]> GetRolesByPermissionIdAsync(Guid permissionId)
        {
            return await (from rp in _context.RolePermissions
                        join r in _context.Roles on rp.RoleId equals r.Id
                        where rp.PermissionId == permissionId
                        select new { r.Id, r.Name, r.GuardName })
                        .ToArrayAsync<object>();
        }

        // 3. Role names attached with User
        public async Task<string[]> GetRoleNamesByUserIdAsync(Guid userId)
        {
            return await (from mr in _context.ModelRoles
                        join r in _context.Roles on mr.RoleId equals r.Id
                        where mr.ModelId == userId && mr.ModelName == "User"
                        select r.Name)
                        .Distinct()
                        .ToArrayAsync();
        }

        // 4. Permission names attached with User Roles
        public async Task<string[]> GetRolePermissionsByUserIdAsync(Guid userId)
        {
            return await (from mr in _context.ModelRoles
                        join rp in _context.RolePermissions on mr.RoleId equals rp.RoleId
                        join p in _context.Permissions on rp.PermissionId equals p.Id
                        where mr.ModelId == userId && mr.ModelName == "User"
                        select p.Name)
                        .Distinct()
                        .ToArrayAsync();
        }

        // 5. Direct Permission names attached with User
        public async Task<string[]> GetModelPermissionsByUserIdAsync(Guid userId)
        {
            return await (from mp in _context.ModelPermissions
                        join p in _context.Permissions on mp.PermissionId equals p.Id
                        where mp.ModelId == userId && mp.ModelName == "User"
                        select p.Name)
                        .Distinct()
                        .ToArrayAsync();
        }

        // 6. Merged unique Permissions (Role-based + Direct)
        public async Task<string[]> GetAllPermissionsByUserIdAsync(Guid userId)
        {
            var rolePermissions = await GetRolePermissionsByUserIdAsync(userId);
            var directPermissions = await GetModelPermissionsByUserIdAsync(userId);

            return [.. rolePermissions.Concat(directPermissions).Distinct()];
        }
    }

}
