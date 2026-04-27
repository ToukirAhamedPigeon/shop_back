using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Options;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Application.DTOs.Common;

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
            
            if (req.IsActive.HasValue)
                baseQuery = baseQuery.Where(o => o.IsActive == req.IsActive.Value);
            
            if (req.FilterByNullParent)
            {
                baseQuery = baseQuery.Where(o => o.ParentId == null);
            }
            else 
            {
                var parentIdFilter = req.GetParentIdFilter();
                if (parentIdFilter.HasValue && parentIdFilter.Value != Guid.Empty)
                {
                    baseQuery = baseQuery.Where(o => o.ParentId == parentIdFilter.Value);
                }
            }
            
            if (req.CreatedFrom.HasValue)
                baseQuery = baseQuery.Where(o => o.CreatedAt >= req.CreatedFrom.Value);
            if (req.CreatedTo.HasValue)
                baseQuery = baseQuery.Where(o => o.CreatedAt <= req.CreatedTo.Value);
            
            if (!string.IsNullOrWhiteSpace(req.Q))
            {
                var q = req.Q.Trim();
                baseQuery = baseQuery.Where(o => o.Name.Contains(q));
            }
            
            int totalCount = await baseQuery.CountAsync();
            int grandTotalCount = await _context.Options.IgnoreQueryFilters().CountAsync();
            
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
                .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
        }

        public async Task<Option?> GetOptionByIdIncludingDeletedAsync(Guid id)
        {
            return await _context.Options
                .IgnoreQueryFilters()
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
        // Add these methods at the end of the class:
        public async Task DeleteOptionAsync(Guid id, bool permanent, Guid? deletedBy)
        {
            // IMPORTANT: Use IgnoreQueryFilters to get even deleted records
            var option = await _context.Options
                .IgnoreQueryFilters()
                .Include(o => o.Children)
                .FirstOrDefaultAsync(o => o.Id == id);
                
            if (option == null) return;
            
            // Safely check for children - handle null collection
            var children = option.Children ?? new List<Option>();
            var hasActiveChildren = children.Any(c => !c.IsDeleted);
            
            if (permanent)
            {
                // For permanent delete, we must have no active children
                if (hasActiveChildren)
                {
                    var activeChildrenCount = children.Count(c => !c.IsDeleted);
                    throw new InvalidOperationException($"Cannot permanently delete option '{option.Name}' because it has {activeChildrenCount} active child option(s). Please delete all child options first.");
                }
                
                // Also handle soft-deleted children - optionally delete them too
                var hasSoftDeletedChildren = children.Any(c => c.IsDeleted);
                if (hasSoftDeletedChildren)
                {
                    // Delete all soft-deleted children permanently
                    var softDeletedChildren = children.Where(c => c.IsDeleted).ToList();
                    foreach (var child in softDeletedChildren)
                    {
                        _context.Options.Remove(child);
                    }
                }
                
                _context.Options.Remove(option);
            }
            else
            {
                // For soft delete, we can soft delete even with children
                option.IsDeleted = true;
                option.DeletedAt = DateTime.UtcNow;
                option.UpdatedAt = DateTime.UtcNow;
                option.UpdatedBy = deletedBy;
                option.DeletedBy = deletedBy;
                
                // Also update parent's HasChild flag
                if (option.ParentId.HasValue)
                {
                    var parent = await _context.Options
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(o => o.Id == option.ParentId.Value);
                    
                    if (parent != null)
                    {
                        var remainingActiveChildren = await _context.Options
                            .CountAsync(o => o.ParentId == parent.Id && !o.IsDeleted);
                        parent.HasChild = remainingActiveChildren > 0;
                        parent.UpdatedAt = DateTime.UtcNow;
                        parent.UpdatedBy = deletedBy;
                    }
                }
            }
        }

        // Updated BulkDeleteOptionsAsync with child check
        public async Task<BulkOperationResponse> BulkDeleteOptionsAsync(List<Guid> ids, bool permanent, Guid? deletedBy)
        {
            var response = new BulkOperationResponse
            {
                TotalCount = ids.Count,
                SuccessCount = 0,
                FailedCount = 0,
                Success = true
            };

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var id in ids)
                {
                    try
                    {
                        var option = await _context.Options
                            .Include(o => o.Children)
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(o => o.Id == id);

                        if (option == null)
                        {
                            response.FailedCount++;
                            response.Errors.Add(new BulkOperationError
                            {
                                Id = id,
                                Error = "Option not found"
                            });
                            response.Success = false;
                            continue;
                        }

                        // Check for children
                        var hasChildren = option.Children != null && option.Children.Any(c => !c.IsDeleted);
                        
                        if (permanent)
                        {
                            // For permanent delete, cannot have children
                            if (hasChildren)
                            {
                                response.FailedCount++;
                                response.Errors.Add(new BulkOperationError
                                {
                                    Id = id,
                                    Error = $"Cannot permanently delete option '{option.Name}' because it has child options. Please delete all child options first."
                                });
                                response.Success = false;
                                continue;
                            }
                            
                            _context.Options.Remove(option);
                        }
                        else
                        {
                            // Soft delete - allowed even with children
                            option.IsDeleted = true;
                            option.DeletedAt = DateTime.UtcNow;
                            option.UpdatedAt = DateTime.UtcNow;
                            option.UpdatedBy = deletedBy;
                            option.DeletedBy = deletedBy;
                        }

                        response.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        response.FailedCount++;
                        response.Errors.Add(new BulkOperationError
                        {
                            Id = id,
                            Error = ex.Message
                        });
                        response.Success = false;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                response.Message = $"Processed {response.TotalCount} options. Success: {response.SuccessCount}, Failed: {response.FailedCount}";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = $"Bulk operation failed: {ex.Message}";
            }

            return response;
        }

        public async Task<BulkOperationResponse> BulkRestoreOptionsAsync(List<Guid> ids, Guid? restoredBy)
        {
            var response = new BulkOperationResponse
            {
                TotalCount = ids.Count,
                SuccessCount = 0,
                FailedCount = 0,
                Success = true
            };

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var id in ids)
                {
                    try
                    {
                        var option = await _context.Options
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(o => o.Id == id && o.IsDeleted);

                        if (option == null)
                        {
                            response.FailedCount++;
                            response.Errors.Add(new BulkOperationError
                            {
                                Id = id,
                                Error = "Option not found or not deleted"
                            });
                            response.Success = false;
                            continue;
                        }

                        option.IsDeleted = false;
                        option.DeletedAt = null;
                        option.DeletedBy = null;
                        option.UpdatedBy = restoredBy;
                        option.UpdatedAt = DateTime.UtcNow;

                        response.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        response.FailedCount++;
                        response.Errors.Add(new BulkOperationError
                        {
                            Id = id,
                            Error = ex.Message
                        });
                        response.Success = false;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                response.Message = $"Processed {response.TotalCount} options. Success: {response.SuccessCount}, Failed: {response.FailedCount}";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = $"Bulk restore failed: {ex.Message}";
            }

            return response;
        }

    }
}