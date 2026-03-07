using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Infrastructure.Repositories
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
                          join r in _context.Roles on rp.RoleId equals r.Id
                          where rp.RoleId == roleId
                                && p.IsActive && !p.IsDeleted
                                && r.IsActive && !r.IsDeleted
                          select new { p.Id, p.Name, p.GuardName })
                          .ToArrayAsync<object>();
        }

        // 2. Roles for a given Permission
        public async Task<object[]> GetRolesByPermissionIdAsync(Guid permissionId)
        {
            return await (from rp in _context.RolePermissions
                          join r in _context.Roles on rp.RoleId equals r.Id
                          join p in _context.Permissions on rp.PermissionId equals p.Id
                          where rp.PermissionId == permissionId
                                && r.IsActive && !r.IsDeleted
                                && p.IsActive && !p.IsDeleted
                          select new { r.Id, r.Name, r.GuardName })
                          .ToArrayAsync<object>();
        }

        // 3. Role names attached with User
        public async Task<string[]> GetRoleNamesByUserIdAsync(Guid userId)
        {
            return await (from mr in _context.ModelRoles
                          join r in _context.Roles on mr.RoleId equals r.Id
                          join u in _context.Users on mr.ModelId equals u.Id
                          where mr.ModelId == userId && mr.ModelName == "User"
                                && r.IsActive && !r.IsDeleted
                                && u.IsActive && !u.IsDeleted
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
                          join r in _context.Roles on mr.RoleId equals r.Id
                          join u in _context.Users on mr.ModelId equals u.Id
                          where mr.ModelId == userId && mr.ModelName == "User"
                                && p.IsActive && !p.IsDeleted
                                && r.IsActive && !r.IsDeleted
                                && u.IsActive && !u.IsDeleted
                          select p.Name)
                          .Distinct()
                          .ToArrayAsync();
        }

        // 5. Direct Permission names attached with User
        public async Task<string[]> GetModelPermissionsByUserIdAsync(Guid userId)
        {
            return await (from mp in _context.ModelPermissions
                          join p in _context.Permissions on mp.PermissionId equals p.Id
                          join u in _context.Users on mp.ModelId equals u.Id
                          where mp.ModelId == userId && mp.ModelName == "User"
                                && p.IsActive && !p.IsDeleted
                                && u.IsActive && !u.IsDeleted
                          select p.Name)
                          .Distinct()
                          .ToArrayAsync();
        }

        // 6. Merged unique Permissions (Role-based + Direct)
        public async Task<string[]> GetAllPermissionsByUserIdAsync(Guid userId)
        {
            var rolePermissions = await GetRolePermissionsByUserIdAsync(userId);
            var directPermissions = await GetModelPermissionsByUserIdAsync(userId);

            return rolePermissions.Concat(directPermissions).Distinct().ToArray();
        }
        public async Task<string[]> GetAllRolesAsync()
        {
            return await _context.Roles
                .Where(r => r.IsActive && !r.IsDeleted)
                .Select(r => r.Name)
                .Distinct()
                .ToArrayAsync();
        }

        public async Task<string[]> GetAllPermissionsAsync()
        {
            return await _context.Permissions
                .Where(r => r.IsActive && !r.IsDeleted)
                .Select(r => r.Name)
                .Distinct()
                .ToArrayAsync();
        }

        // Assign Roles to a User
        public async Task AssignRolesAsync(Guid userId, string[] roles)
        {
            // Remove old roles
            var existingRoles = _context.ModelRoles.Where(mr => mr.ModelId == userId && mr.ModelName == "User");
            _context.ModelRoles.RemoveRange(existingRoles);

            // Get role Ids from role names
            var roleEntities = await _context.Roles
                .Where(r => roles.Contains(r.Name) && r.IsActive && !r.IsDeleted)
                .ToListAsync();

            // Add new roles
            foreach (var role in roleEntities)
            {
                _context.ModelRoles.Add(new ModelRole
                {
                    Id = Guid.NewGuid(),
                    ModelId = userId,
                    ModelName = "User",
                    RoleId = role.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        // Assign Permissions to a User (directly)
        public async Task AssignPermissionsAsync(Guid userId, string[] permissions)
        {
            // Remove old direct permissions
            var existingPermissions = _context.ModelPermissions
                .Where(mp => mp.ModelId == userId && mp.ModelName == "User");
            _context.ModelPermissions.RemoveRange(existingPermissions);

            // Get permission Ids from permission names
            var permissionEntities = await _context.Permissions
                .Where(p => permissions.Contains(p.Name) && p.IsActive && !p.IsDeleted)
                .ToListAsync();

            // Add new permissions
            foreach (var permission in permissionEntities)
            {
                _context.ModelPermissions.Add(new ModelPermission
                {
                    Id = Guid.NewGuid(),
                    ModelId = userId,
                    ModelName = "User",
                    PermissionId = permission.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }
    
        public async Task SetRolesForUserAsync(Guid userId, IEnumerable<string> roleNames)
        {
            // Remove existing roles
            var existingRoles = _context.ModelRoles
                .Where(mr => mr.ModelId == userId && mr.ModelName == "User");
            _context.ModelRoles.RemoveRange(existingRoles);

            // Get valid role entities
            var roleEntities = await _context.Roles
                .Where(r => roleNames.Contains(r.Name) && r.IsActive && !r.IsDeleted)
                .ToListAsync();

            // Add new roles
            foreach (var role in roleEntities)
            {
                _context.ModelRoles.Add(new ModelRole
                {
                    Id = Guid.NewGuid(),
                    ModelId = userId,
                    ModelName = "User",
                    RoleId = role.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<string[]> GetPermissionsByRolesAsync(IEnumerable<string> roleNames)
        {
            return await (from r in _context.Roles
                        join rp in _context.RolePermissions on r.Id equals rp.RoleId
                        join p in _context.Permissions on rp.PermissionId equals p.Id
                        where roleNames.Contains(r.Name)
                                && r.IsActive && !r.IsDeleted
                                && p.IsActive && !p.IsDeleted
                        select p.Name)
                        .Distinct()
                        .ToArrayAsync();
        }

        public async Task SetPermissionsForUserAsync(Guid userId, IEnumerable<string> permissionNames)
        {
            // Remove existing direct permissions
            var existingPermissions = _context.ModelPermissions
                .Where(mp => mp.ModelId == userId && mp.ModelName == "User");
            _context.ModelPermissions.RemoveRange(existingPermissions);

            if (permissionNames.Any())
            {
                // Get valid permission entities
                var permissionEntities = await _context.Permissions
                    .Where(p => permissionNames.Contains(p.Name) && p.IsActive && !p.IsDeleted)
                    .ToListAsync();

                // Add new permissions
                foreach (var permission in permissionEntities)
                {
                    _context.ModelPermissions.Add(new ModelPermission
                    {
                        Id = Guid.NewGuid(),
                        ModelId = userId,
                        ModelName = "User",
                        PermissionId = permission.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
