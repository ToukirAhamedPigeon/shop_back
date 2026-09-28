using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.DTOs.PermissionGroups;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Data;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class PermissionGroupRepository : IPermissionGroupRepository
    {
        private readonly AppDbContext _context;

        public PermissionGroupRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PermissionGroupDto>> GetAllAsync(string? search, bool activeOnly)
        {
            var query = _context.PermissionGroups.AsNoTracking();
            if (activeOnly) query = query.Where(g => g.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(g => g.Name.ToLower().Contains(s) || (g.Description != null && g.Description.ToLower().Contains(s)));
            }
            var groups = await query.OrderBy(g => g.Name).ToListAsync();
            return await ToDtosAsync(groups);
        }

        public async Task<PermissionGroupDto?> GetDtoAsync(Guid id)
        {
            var group = await _context.PermissionGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
            if (group == null) return null;
            return (await ToDtosAsync(new List<PermissionGroup> { group })).First();
        }

        private async Task<List<PermissionGroupDto>> ToDtosAsync(List<PermissionGroup> groups)
        {
            var ids = groups.Select(g => g.Id).ToList();

            var permissions = await (from gp in _context.PermissionGroupPermissions
                                     join p in _context.Permissions on gp.PermissionId equals p.Id
                                     where ids.Contains(gp.GroupId) && !p.IsDeleted
                                     select new { gp.GroupId, p.Name }).ToListAsync();

            var roles = await (from rg in _context.RolePermissionGroups
                               join r in _context.Roles on rg.RoleId equals r.Id
                               where ids.Contains(rg.GroupId) && !r.IsDeleted
                               select new { rg.GroupId, r.Name }).ToListAsync();

            var userCounts = await _context.ModelPermissionGroups
                .Where(mg => ids.Contains(mg.GroupId) && mg.ModelName == "User")
                .GroupBy(mg => mg.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToListAsync();

            return groups.Select(g => new PermissionGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                IsActive = g.IsActive,
                Permissions = permissions.Where(p => p.GroupId == g.Id).Select(p => p.Name).Distinct().OrderBy(n => n).ToArray(),
                Roles = roles.Where(r => r.GroupId == g.Id).Select(r => r.Name).Distinct().OrderBy(n => n).ToArray(),
                UserCount = userCounts.FirstOrDefault(u => u.GroupId == g.Id)?.Count ?? 0,
                CreatedAt = g.CreatedAt,
                UpdatedAt = g.UpdatedAt,
            }).ToList();
        }

        public Task<PermissionGroup?> GetByIdAsync(Guid id) =>
            _context.PermissionGroups.FirstOrDefaultAsync(g => g.Id == id);

        public Task<bool> NameExistsAsync(string name, Guid? ignoreId = null)
        {
            var lower = name.Trim().ToLower();
            return _context.PermissionGroups.AnyAsync(g => g.Name.ToLower() == lower && (ignoreId == null || g.Id != ignoreId));
        }

        public async Task AddAsync(PermissionGroup group) => await _context.PermissionGroups.AddAsync(group);

        public void Remove(PermissionGroup group) => _context.PermissionGroups.Remove(group);

        public async Task SetPermissionsAsync(Guid groupId, IEnumerable<string> permissionNames)
        {
            var names = permissionNames.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
            _context.PermissionGroupPermissions.RemoveRange(_context.PermissionGroupPermissions.Where(gp => gp.GroupId == groupId));
            var ids = await _context.Permissions
                .Where(p => names.Contains(p.Name) && !p.IsDeleted)
                .Select(p => p.Id)
                .Distinct()
                .ToListAsync();
            foreach (var id in ids)
                _context.PermissionGroupPermissions.Add(new PermissionGroupPermission { GroupId = groupId, PermissionId = id });
        }

        public Task<string[]> GetGroupNamesByRoleIdAsync(Guid roleId) =>
            (from rg in _context.RolePermissionGroups
             join g in _context.PermissionGroups on rg.GroupId equals g.Id
             where rg.RoleId == roleId
             orderby g.Name
             select g.Name).ToArrayAsync();

        public async Task SetGroupsForRoleAsync(Guid roleId, IEnumerable<string> groupNames)
        {
            var ids = await IdsOfAsync(groupNames);
            _context.RolePermissionGroups.RemoveRange(_context.RolePermissionGroups.Where(rg => rg.RoleId == roleId));
            foreach (var id in ids)
                _context.RolePermissionGroups.Add(new RolePermissionGroup { RoleId = roleId, GroupId = id });
        }

        public Task<string[]> GetGroupNamesByUserIdAsync(Guid userId) =>
            (from mg in _context.ModelPermissionGroups
             join g in _context.PermissionGroups on mg.GroupId equals g.Id
             where mg.ModelId == userId && mg.ModelName == "User"
             orderby g.Name
             select g.Name).ToArrayAsync();

        public async Task SetGroupsForUserAsync(Guid userId, IEnumerable<string> groupNames)
        {
            var ids = await IdsOfAsync(groupNames);
            _context.ModelPermissionGroups.RemoveRange(_context.ModelPermissionGroups.Where(mg => mg.ModelId == userId && mg.ModelName == "User"));
            foreach (var id in ids)
                _context.ModelPermissionGroups.Add(new ModelPermissionGroup { ModelId = userId, ModelName = "User", GroupId = id });
        }

        private Task<List<Guid>> IdsOfAsync(IEnumerable<string> groupNames)
        {
            var names = groupNames.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
            return _context.PermissionGroups.Where(g => names.Contains(g.Name)).Select(g => g.Id).ToListAsync();
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
