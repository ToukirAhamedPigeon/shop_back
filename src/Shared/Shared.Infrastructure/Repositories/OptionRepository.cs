using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Options;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class OptionRepository : IOptionRepository
    {
        private readonly AppDbContext _context;

        public OptionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(IEnumerable<OptionDto> Options, int TotalCount, int GrandTotalCount, int PageIndex, int PageSize)> 
        GetFilteredOptionsAsync(OptionFilterRequest req)
        {
            IQueryable<Option> baseQuery;
            
            // Handle deleted filter
            if (req.IsDeleted.HasValue && req.IsDeleted.Value)
            {
                baseQuery = _context.Options
                    .IgnoreQueryFilters()
                    .Include(o => o.Parent)
                    .Where(o => o.IsDeleted == true);
            }
            else
            {
                baseQuery = _context.Options
                    .Include(o => o.Parent)
                    .Where(o => !o.IsDeleted);
            }
            
            // Handle active filter
            if (req.IsActive.HasValue)
                baseQuery = baseQuery.Where(o => o.IsActive == req.IsActive.Value);
            
            // Handle parent filter - FIXED with proper null checking
            if (req.FilterByNullParent)
            {
                // Show only options with NO parent (parent_id IS NULL)
                baseQuery = baseQuery.Where(o => o.ParentId == null);
            }
            else 
            {
                var parentIdFilter = req.GetParentIdFilter();
                if (parentIdFilter.HasValue && parentIdFilter.Value != Guid.Empty)
                {
                    // Show only options with specific parent
                    baseQuery = baseQuery.Where(o => o.ParentId == parentIdFilter.Value);
                }
            }
            // If ParentId is "all" or null/empty, don't filter by parent at all
            
            // Handle date range filter
            if (req.CreatedFrom.HasValue)
                baseQuery = baseQuery.Where(o => o.CreatedAt >= req.CreatedFrom.Value);
            if (req.CreatedTo.HasValue)
                baseQuery = baseQuery.Where(o => o.CreatedAt <= req.CreatedTo.Value);
            
            // Handle search
            if (!string.IsNullOrWhiteSpace(req.Q))
            {
                var q = req.Q.Trim();
                baseQuery = baseQuery.Where(o => o.Name.Contains(q));
            }
            
            int totalCount = await baseQuery.CountAsync();
            int grandTotalCount = await _context.Options.IgnoreQueryFilters().CountAsync();
            
            // Handle sorting
            bool desc = req.SortOrder?.ToLower() == "desc";
            var sortBy = req.SortBy?.ToLower();
            
            IOrderedQueryable<Option> query;
            query = sortBy switch
            {
                "name" => desc ? baseQuery.OrderByDescending(x => x.Name) : baseQuery.OrderBy(x => x.Name),
                "haschild" => desc ? baseQuery.OrderByDescending(x => x.HasChild) : baseQuery.OrderBy(x => x.HasChild),
                "isactive" => desc ? baseQuery.OrderByDescending(x => x.IsActive) : baseQuery.OrderBy(x => x.IsActive),
                "createdat" => desc ? baseQuery.OrderByDescending(x => x.CreatedAt) : baseQuery.OrderBy(x => x.CreatedAt),
                _ => desc ? baseQuery.OrderByDescending(x => x.CreatedAt) : baseQuery.OrderBy(x => x.CreatedAt)
            };
            
            var options = await query
                .Skip((req.Page - 1) * req.Limit)
                .Take(req.Limit)
                .ToListAsync();
            
            // Get user names for audit fields - Fixed null handling
            var userIds = options
                .SelectMany(o => new[] { o.CreatedBy, o.UpdatedBy, o.DeletedBy })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            
            var users = new Dictionary<Guid, string>();
            if (userIds.Any())
            {
                users = await _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.Name);
            }
            
            var result = options.Select(o => new OptionDto
            {
                Id = o.Id,
                Name = o.Name,
                ParentId = o.ParentId,
                ParentName = o.Parent?.Name,
                HasChild = o.HasChild,
                IsActive = o.IsActive,
                IsDeleted = o.IsDeleted,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                CreatedByName = o.CreatedBy.HasValue && users.ContainsKey(o.CreatedBy.Value) ? users[o.CreatedBy.Value] : null,
                UpdatedByName = o.UpdatedBy.HasValue && users.ContainsKey(o.UpdatedBy.Value) ? users[o.UpdatedBy.Value] : null,
                DeletedByName = o.DeletedBy.HasValue && users.ContainsKey(o.DeletedBy.Value) ? users[o.DeletedBy.Value] : null
            }).ToList();
            
            return (result, totalCount, grandTotalCount, req.Page - 1, req.Limit);
        }

        public async Task<Option?> GetOptionByIdAsync(Guid id)
        {
            return await _context.Options
                .Include(o => o.Parent)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<bool> OptionExistsAsync(string name, Guid? parentId, Guid? ignoreId = null)
        {
            var query = _context.Options.Where(o => o.Name == name && o.ParentId == parentId && !o.IsDeleted);
            if (ignoreId.HasValue)
                query = query.Where(o => o.Id != ignoreId.Value);
            return await query.AnyAsync();
        }

        public async Task<Option> CreateOptionAsync(Option option)
        {
            await _context.Options.AddAsync(option);
            return option;
        }

        public void UpdateOption(Option option)
        {
            _context.Options.Update(option);
        }

        public async Task DeleteOptionAsync(Guid id, bool permanent, Guid? deletedBy)
        {
            var option = await _context.Options.FindAsync(id);
            if (option == null) return;
            
            if (permanent)
            {
                // First, update children to remove parent reference
                var children = await _context.Options
                    .Where(o => o.ParentId == id)
                    .ToListAsync();
                
                foreach (var child in children)
                {
                    child.ParentId = null;
                    child.HasChild = false;
                    child.UpdatedAt = DateTime.UtcNow;
                    child.UpdatedBy = deletedBy;
                }
                
                _context.Options.Remove(option);
            }
            else
            {
                option.IsDeleted = true;
                option.DeletedAt = DateTime.UtcNow;
                option.UpdatedAt = DateTime.UtcNow;
                option.UpdatedBy = deletedBy;
                option.DeletedBy = deletedBy;
            }
        }

        public async Task<bool> OptionHasChildrenAsync(Guid optionId)
        {
            return await _context.Options
                .AnyAsync(o => o.ParentId == optionId && !o.IsDeleted);
        }

        public async Task<int> GetChildrenCountAsync(Guid optionId)
        {
            return await _context.Options
                .CountAsync(o => o.ParentId == optionId && !o.IsDeleted);
        }

        public async Task<IEnumerable<Option>> GetParentOptionsAsync(bool onlyWithChildren = true)
        {
            var query = _context.Options
                .Where(o => !o.IsDeleted && o.IsActive);
            
            if (onlyWithChildren)
                query = query.Where(o => o.HasChild == true);
            
            return await query
                .OrderBy(o => o.Name)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}