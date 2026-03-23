using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Roles;
using shop_back.src.Shared.Application.DTOs.Permissions;
using shop_back.src.Shared.Application.DTOs.Common;

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
        
        public async Task<(IEnumerable<RoleDto> Roles, int TotalCount, int GrandTotalCount, int PageIndex, int PageSize)> 
        GetFilteredRolesAsync(RolePermissionFilterRequest req)
        {
            IQueryable<Role> baseQuery;
            
            // Handle deleted filter
            if (req.IsDeleted.HasValue && req.IsDeleted.Value)
            {
                baseQuery = _context.Roles
                    .IgnoreQueryFilters()
                    .Where(r => r.IsDeleted == true);
            }
            else
            {
                baseQuery = _context.Roles
                    .Where(r => !r.IsDeleted);
            }
            
            // Handle active filter
            if (req.IsActive.HasValue)
                baseQuery = baseQuery.Where(r => r.IsActive == req.IsActive.Value);
            
            // Handle permissions filter - filter roles by associated permissions
            if (req.Permissions != null && req.Permissions.Any())
            {
                baseQuery = baseQuery.Where(r =>
                    _context.RolePermissions.Any(rp =>
                        rp.RoleId == r.Id &&
                        _context.Permissions.Any(p =>
                            p.Id == rp.PermissionId &&
                            req.Permissions.Contains(p.Name) &&
                            p.IsActive && !p.IsDeleted
                        )
                    )
                );
            }
            
            // Handle search
            if (!string.IsNullOrWhiteSpace(req.Q))
            {
                var q = req.Q.Trim();
                baseQuery = baseQuery.Where(r => 
                    r.Name.Contains(q) ||
                    r.GuardName.Contains(q));
            }
            
            // Get total count
            int totalCount = await baseQuery.CountAsync();
            int grandTotalCount = await _context.Roles.IgnoreQueryFilters().CountAsync();
            
            // Handle sorting
            bool desc = req.SortOrder?.ToLower() == "desc";
            var sortBy = req.SortBy?.ToLower();
            
            IOrderedQueryable<Role> query;
            query = sortBy switch
            {
                "name" => desc ? baseQuery.OrderByDescending(x => x.Name) : baseQuery.OrderBy(x => x.Name),
                "guardname" => desc ? baseQuery.OrderByDescending(x => x.GuardName) : baseQuery.OrderBy(x => x.GuardName),
                "isactive" => desc ? baseQuery.OrderByDescending(x => x.IsActive) : baseQuery.OrderBy(x => x.IsActive),
                "createdat" => desc ? baseQuery.OrderByDescending(x => x.CreatedAt) : baseQuery.OrderBy(x => x.CreatedAt),
                _ => desc ? baseQuery.OrderByDescending(x => x.CreatedAt) : baseQuery.OrderBy(x => x.CreatedAt)
            };
            
            // Pagination
            var roles = await query
                .Skip((req.Page - 1) * req.Limit)
                .Take(req.Limit)
                .ToListAsync();
            
            // Build DTOs with permissions
            var result = new List<RoleDto>();
            foreach (var role in roles)
            {
                var permissions = await GetPermissionsByRoleIdAsync(role.Id);
                result.Add(new RoleDto
                {
                    Id = role.Id,
                    Name = role.Name,
                    GuardName = role.GuardName,
                    IsActive = role.IsActive,
                    IsDeleted = role.IsDeleted,
                    CreatedAt = role.CreatedAt,
                    UpdatedAt = role.UpdatedAt,
                    Permissions = permissions.Select(p => 
                        p.GetType().GetProperty("Name")?.GetValue(p)?.ToString() ?? "").ToArray()
                });
            }
            
            return (result, totalCount, grandTotalCount, req.Page - 1, req.Limit);
        }

        public async Task<Role?> GetRoleByIdAsync(Guid id)
        {
            return await _context.Roles
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<bool> RoleExistsAsync(string name, Guid? ignoreId = null)
        {
            var query = _context.Roles.Where(r => r.Name == name && !r.IsDeleted);
            if (ignoreId.HasValue)
                query = query.Where(r => r.Id != ignoreId.Value);
            return await query.AnyAsync();
        }

        public async Task<Role> CreateRoleAsync(Role role)
        {
            await _context.Roles.AddAsync(role);
            return role;
        }

        public void UpdateRole(Role role)
        {
            _context.Roles.Update(role);
        }

        public async Task DeleteRoleAsync(Guid id, bool permanent, Guid? deletedBy)
        {
            var role = await _context.Roles.FindAsync(id);
            if (role == null) return;
            
            if (permanent)
            {
                // Delete related records first
                var rolePermissions = await _context.RolePermissions
                    .Where(rp => rp.RoleId == id)
                    .ToListAsync();
                _context.RolePermissions.RemoveRange(rolePermissions);
                
                var modelRoles = await _context.ModelRoles
                    .Where(mr => mr.RoleId == id)
                    .ToListAsync();
                _context.ModelRoles.RemoveRange(modelRoles);
                
                _context.Roles.Remove(role);
            }
            else
            {
                role.IsDeleted = true;
                role.DeletedAt = DateTime.UtcNow;
                role.UpdatedAt = DateTime.UtcNow;
                role.UpdatedBy = deletedBy;
            }
        }

        public async Task<bool> RoleHasRelatedRecordsAsync(Guid roleId)
        {
            var hasRolePermissions = await _context.RolePermissions
                .AnyAsync(rp => rp.RoleId == roleId);
            var hasModelRoles = await _context.ModelRoles
                .AnyAsync(mr => mr.RoleId == roleId);
            
            return hasRolePermissions || hasModelRoles;
        }

        public async Task AssignPermissionsToRoleAsync(Guid roleId, IEnumerable<string> permissionNames)
        {
            // Remove existing permissions
            var existingPermissions = _context.RolePermissions
                .Where(rp => rp.RoleId == roleId);
            _context.RolePermissions.RemoveRange(existingPermissions);
            
            if (permissionNames.Any())
            {
                // Get permission entities
                var permissionEntities = await _context.Permissions
                    .Where(p => permissionNames.Contains(p.Name) && p.IsActive && !p.IsDeleted)
                    .ToListAsync();
                
                // Add new permissions
                foreach (var permission in permissionEntities)
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        Id = Guid.NewGuid(),
                        RoleId = roleId,
                        PermissionId = permission.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            
            await _context.SaveChangesAsync();
        }

        public async Task<string[]> GetPermissionsByRoleNamesAsync(IEnumerable<string> roleNames)
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

        // Permission CRUD methods
        public async Task<(IEnumerable<PermissionDto> Permissions, int TotalCount, int GrandTotalCount, int PageIndex, int PageSize)> 
    GetFilteredPermissionsAsync(RolePermissionFilterRequest req)
    {
        IQueryable<Permission> baseQuery;
        
        // Handle deleted filter
        if (req.IsDeleted.HasValue && req.IsDeleted.Value)
        {
            baseQuery = _context.Permissions
                .IgnoreQueryFilters()
                .Where(p => p.IsDeleted == true);
        }
        else
        {
            baseQuery = _context.Permissions
                .Where(p => !p.IsDeleted);
        }
        
        // Handle active filter
        if (req.IsActive.HasValue)
            baseQuery = baseQuery.Where(p => p.IsActive == req.IsActive.Value);
        
        // Handle roles filter - filter permissions by associated roles
        if (req.Roles != null && req.Roles.Any())
        {
            baseQuery = baseQuery.Where(p =>
                _context.RolePermissions.Any(rp =>
                    rp.PermissionId == p.Id &&
                    _context.Roles.Any(r =>
                        r.Id == rp.RoleId &&
                        req.Roles.Contains(r.Name) &&
                        r.IsActive && !r.IsDeleted
                    )
                )
            );
        }
        
        // Handle search
        if (!string.IsNullOrWhiteSpace(req.Q))
        {
            var q = req.Q.Trim();
            baseQuery = baseQuery.Where(p => 
                p.Name.Contains(q) ||
                p.GuardName.Contains(q));
        }
        
        int totalCount = await baseQuery.CountAsync();
        int grandTotalCount = await _context.Permissions.IgnoreQueryFilters().CountAsync();
        
        bool desc = req.SortOrder?.ToLower() == "desc";
        var sortBy = req.SortBy?.ToLower();
        
        IOrderedQueryable<Permission> query;
        query = sortBy switch
        {
            "name" => desc ? baseQuery.OrderByDescending(x => x.Name) : baseQuery.OrderBy(x => x.Name),
            "guardname" => desc ? baseQuery.OrderByDescending(x => x.GuardName) : baseQuery.OrderBy(x => x.GuardName),
            "isactive" => desc ? baseQuery.OrderByDescending(x => x.IsActive) : baseQuery.OrderBy(x => x.IsActive),
            "createdat" => desc ? baseQuery.OrderByDescending(x => x.CreatedAt) : baseQuery.OrderBy(x => x.CreatedAt),
            _ => desc ? baseQuery.OrderByDescending(x => x.CreatedAt) : baseQuery.OrderBy(x => x.CreatedAt)
        };
        
        var permissions = await query
            .Skip((req.Page - 1) * req.Limit)
            .Take(req.Limit)
            .ToListAsync();
        
        var result = new List<PermissionDto>();
        foreach (var permission in permissions)
        {
            var roles = await GetRolesByPermissionIdAsync(permission.Id);
            result.Add(new PermissionDto
            {
                Id = permission.Id,
                Name = permission.Name,
                GuardName = permission.GuardName,
                IsActive = permission.IsActive,
                IsDeleted = permission.IsDeleted,
                CreatedAt = permission.CreatedAt,
                UpdatedAt = permission.UpdatedAt,
                Roles = roles.Select(r => 
                    r.GetType().GetProperty("Name")?.GetValue(r)?.ToString() ?? "").ToArray()
            });
        }
        
        return (result, totalCount, grandTotalCount, req.Page - 1, req.Limit);
    }

        public async Task<Permission?> GetPermissionByIdAsync(Guid id)
        {
            return await _context.Permissions
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<bool> PermissionExistsAsync(string name, Guid? ignoreId = null)
        {
            var query = _context.Permissions.Where(p => p.Name == name && !p.IsDeleted);
            if (ignoreId.HasValue)
                query = query.Where(p => p.Id != ignoreId.Value);
            return await query.AnyAsync();
        }

        public async Task<Permission> CreatePermissionAsync(Permission permission)
        {
            await _context.Permissions.AddAsync(permission);
            return permission;
        }

        public void UpdatePermission(Permission permission)
        {
            _context.Permissions.Update(permission);
        }

        public async Task DeletePermissionAsync(Guid id, bool permanent, Guid? deletedBy)
        {
            var permission = await _context.Permissions.FindAsync(id);
            if (permission == null) return;
            
            if (permanent)
            {
                var rolePermissions = await _context.RolePermissions
                    .Where(rp => rp.PermissionId == id)
                    .ToListAsync();
                _context.RolePermissions.RemoveRange(rolePermissions);
                
                var modelPermissions = await _context.ModelPermissions
                    .Where(mp => mp.PermissionId == id)
                    .ToListAsync();
                _context.ModelPermissions.RemoveRange(modelPermissions);
                
                _context.Permissions.Remove(permission);
            }
            else
            {
                permission.IsDeleted = true;
                permission.DeletedAt = DateTime.UtcNow;
                permission.UpdatedAt = DateTime.UtcNow;
                permission.UpdatedBy = deletedBy;
            }
        }

        public async Task<bool> PermissionHasRelatedRecordsAsync(Guid permissionId)
        {
            var hasRolePermissions = await _context.RolePermissions
                .AnyAsync(rp => rp.PermissionId == permissionId);
            var hasModelPermissions = await _context.ModelPermissions
                .AnyAsync(mp => mp.PermissionId == permissionId);
            
            return hasRolePermissions || hasModelPermissions;
        }

        public async Task AssignRolesToPermissionAsync(Guid permissionId, IEnumerable<string> roleNames)
        {
            // Remove existing role-permission associations
            var existingAssociations = _context.RolePermissions
                .Where(rp => rp.PermissionId == permissionId);
            _context.RolePermissions.RemoveRange(existingAssociations);
            
            if (roleNames.Any())
            {
                var roleEntities = await _context.Roles
                    .Where(r => roleNames.Contains(r.Name) && r.IsActive && !r.IsDeleted)
                    .ToListAsync();
                
                foreach (var role in roleEntities)
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        Id = Guid.NewGuid(),
                        RoleId = role.Id,
                        PermissionId = permissionId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            
            await _context.SaveChangesAsync();
        }

        public async Task<string[]> GetRolesByPermissionNamesAsync(IEnumerable<string> permissionNames)
        {
            return await (from p in _context.Permissions
                        join rp in _context.RolePermissions on p.Id equals rp.PermissionId
                        join r in _context.Roles on rp.RoleId equals r.Id
                        where permissionNames.Contains(p.Name)
                                && p.IsActive && !p.IsDeleted
                                && r.IsActive && !r.IsDeleted
                        select r.Name)
                        .Distinct()
                        .ToArrayAsync();
        }

        // SaveChanges methods
        public void SaveChanges()
        {
            _context.SaveChanges();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}