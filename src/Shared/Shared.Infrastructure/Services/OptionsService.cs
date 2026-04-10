using System.Text.Json;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.DTOs.Options;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Helpers;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class OptionsService : IOptionsService
    {
        private readonly IUserLogRepository _userLogRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRolePermissionRepository _rolePermissionRepository;
        private readonly ITranslationService _translationService;
        private readonly IOptionRepository _optionRepository;
        private readonly AppDbContext _context;
        private readonly UserLogHelper _userLogHelper;
        private readonly IDatabase _cache;
        private readonly IConnectionMultiplexer _redis;
        private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(1);

        public OptionsService(
            IUserLogRepository userLogRepository, 
            IUserRepository userRepository, 
            IRolePermissionRepository rolePermissionRepository,
            ITranslationService translationService,
            IOptionRepository optionRepository,
            AppDbContext context,
            UserLogHelper userLogHelper,
            IConnectionMultiplexer redis)
        {
            _userLogRepository = userLogRepository;
            _userRepository = userRepository;
            _rolePermissionRepository = rolePermissionRepository;
            _translationService = translationService;
            _optionRepository = optionRepository;
            _context = context;
            _userLogHelper = userLogHelper;
            _redis = redis;
            _cache = redis.GetDatabase();
        }

        public async Task<IEnumerable<SelectOptionDto>> GetOptionsAsync(string type, SelectRequestDto req)
        {
            req ??= new SelectRequestDto();

            string whereJson = req.Where != null ? System.Text.Json.JsonSerializer.Serialize(req.Where) : "";
            string cacheKey = $"Options:{type}:{req.Search}:{req.Skip}:{req.Limit}:{req.SortBy}:{req.SortOrder}:{whereJson}";

            var cached = await _cache.StringGetAsync(cacheKey);
            List<SelectOptionDto> result;

            if (cached.HasValue)
            {
                result = System.Text.Json.JsonSerializer.Deserialize<List<SelectOptionDto>>(cached!) ?? new List<SelectOptionDto>();
                return result;
            }

            switch (type.ToLower())
            {
                case "userlogcollections":
                    result = (await _userLogRepository.GetDistinctModelNamesAsync(req)).ToList();
                    break;
                    
                case "userlogactiontypes":
                    result = (await _userLogRepository.GetDistinctActionTypesAsync(req)).ToList();
                    break;
                    
                case "userlogcreators":
                    result = (await _userLogRepository.GetDistinctCreatorsAsync(req)).ToList();
                    break;
                    
                case "usercreators":
                    result = (await _userRepository.GetDistinctCreatorsAsync(req)).ToList();
                    break;
                    
                case "userupdaters":
                    result = (await _userRepository.GetDistinctUpdatersAsync(req)).ToList();
                    break;
                    
                case "userdatetypes":
                    result = (await _userRepository.GetDistinctDateTypesAsync(req)).ToList();
                    break;
                    
                case "roles":
                    var roles = await _rolePermissionRepository.GetAllRolesAsync();
                    result = roles.Select(r => new SelectOptionDto { Value = r, Label = r }).ToList();
                    break;
                    
                case "permissions":
                    var permissions = await _rolePermissionRepository.GetAllPermissionsAsync();
                    result = permissions.Select(p => new SelectOptionDto { Value = p, Label = p }).ToList();
                    break;

                case "translationmodules":
                    var translationModules = await _translationService.GetModulesForOptionsAsync();
                    result = translationModules.ToList();
                    break;
                    
                default:
                    result = new List<SelectOptionDto>();
                    break;
            }

            foreach (var item in result)
            {
                item.Label = LabelFormatter.ToReadable(item.Label);
            }

            await _cache.StringSetAsync(cacheKey, System.Text.Json.JsonSerializer.Serialize(result), _cacheTtl);

            return result;
        }

        // CRUD Operations for Options
        public async Task<object> GetOptionsAsync(OptionFilterRequest request)
        {
            var (options, totalCount, grandTotalCount, pageIndex, pageSize) = await _optionRepository.GetFilteredOptionsAsync(request);
            
            return new
            {
                options,
                totalCount,
                grandTotalCount,
                pageIndex,
                pageSize
            };
        }

        public async Task<OptionDto?> GetOptionAsync(Guid id)
        {
            var option = await _optionRepository.GetOptionByIdAsync(id);
            if (option == null) return null;
            
            // Get user names
            var userIds = new[] { option.CreatedBy, option.UpdatedBy, option.DeletedBy }
                .Where(uid => uid.HasValue)
                .Select(uid => uid!.Value)
                .ToList();
            
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name);
            
            return new OptionDto
            {
                Id = option.Id,
                Name = option.Name,
                ParentId = option.ParentId,
                ParentName = option.Parent?.Name,
                HasChild = option.HasChild,
                IsActive = option.IsActive,
                IsDeleted = option.IsDeleted,
                CreatedAt = option.CreatedAt,
                UpdatedAt = option.UpdatedAt,
                CreatedByName = option.CreatedBy.HasValue && users.ContainsKey(option.CreatedBy.Value) ? users[option.CreatedBy.Value] : null,
                UpdatedByName = option.UpdatedBy.HasValue && users.ContainsKey(option.UpdatedBy.Value) ? users[option.UpdatedBy.Value] : null,
                DeletedByName = option.DeletedBy.HasValue && users.ContainsKey(option.DeletedBy.Value) ? users[option.DeletedBy.Value] : null
            };
        }

        public async Task<OptionDto?> GetOptionForEditAsync(Guid id)
        {
            return await GetOptionAsync(id);
        }

        public async Task<(bool Success, string Message)> CreateOptionAsync(CreateOptionRequest request, string? createdBy)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(request.Names))
                return (false, "Option names are required");
            
            // Parse multiple option names separated by "=" - preserve case
            var optionNames = request.Names.Split('=', StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
            
            if (!optionNames.Any())
                return (false, "At least one valid option name is required");
            
            // Check for duplicates in request
            if (optionNames.Count != optionNames.Distinct().Count())
                return (false, "Duplicate option names found in request");
            
            // Parse HasChild
            bool hasChild = string.Equals(request.HasChild, "true", StringComparison.OrdinalIgnoreCase);
            
            // Parse IsActive
            bool isActive = string.Equals(request.IsActive, "true", StringComparison.OrdinalIgnoreCase);
            
            // Parse created by
            Guid? createdByGuid = null;
            if (!string.IsNullOrEmpty(createdBy) && Guid.TryParse(createdBy, out var parsed))
                createdByGuid = parsed;
            
            // Validate parent if provided
            if (request.ParentId.HasValue)
            {
                var parent = await _optionRepository.GetOptionByIdAsync(request.ParentId.Value);
                if (parent == null)
                    return (false, "Parent option not found");
                if (!parent.HasChild)
                    return (false, "Selected parent option does not allow children");
            }
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var createdOptions = new List<Option>();
                var existingOptions = new List<string>();
                
                foreach (var optionName in optionNames)
                {
                    // Check for existing option with same name and parent
                    if (await _optionRepository.OptionExistsAsync(optionName, request.ParentId))
                    {
                        existingOptions.Add(optionName);
                        continue;
                    }
                    
                    var option = new Option
                    {
                        Id = Guid.NewGuid(),
                        Name = optionName, // Preserve original case
                        ParentId = request.ParentId,
                        HasChild = hasChild,
                        IsActive = isActive,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = createdByGuid,
                        UpdatedBy = createdByGuid
                    };
                    
                    await _optionRepository.CreateOptionAsync(option);
                    createdOptions.Add(option);
                    
                    // If this option has a parent, update parent's HasChild flag if needed
                    if (request.ParentId.HasValue)
                    {
                        var parent = await _optionRepository.GetOptionByIdAsync(request.ParentId.Value);
                        if (parent != null && !parent.HasChild)
                        {
                            parent.HasChild = true;
                            parent.UpdatedAt = DateTime.UtcNow;
                            parent.UpdatedBy = createdByGuid;
                            _optionRepository.UpdateOption(parent);
                        }
                    }
                }
                
                await _optionRepository.SaveChangesAsync();
                
                // Clear cache after create
                await ClearOptionsCacheAsync();
                
                // Log the action
                if (createdOptions.Any())
                {
                    var afterSnapshot = new
                    {
                        Options = createdOptions.Select(o => new { o.Id, o.Name, o.ParentId, o.HasChild, o.IsActive }),
                        ParentId = request.ParentId,
                        HasChild = hasChild
                    };
                    
                    var changesJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { before = (object?)null, after = afterSnapshot });
                    
                    await _userLogHelper.LogAsync(
                        userId: createdByGuid ?? Guid.Empty,
                        actionType: "Create",
                        detail: $"{createdOptions.Count} option(s) created: {string.Join(", ", createdOptions.Select(o => o.Name))}" +
                                (existingOptions.Any() ? $" (Skipped existing: {string.Join(", ", existingOptions)})" : ""),
                        changes: changesJson,
                        modelName: "Option",
                        modelId: createdOptions.First().Id.ToString()
                    );
                }
                
                await transaction.CommitAsync();
                
                var message = $"{createdOptions.Count} option(s) created successfully";
                if (existingOptions.Any())
                    message += $" (Skipped existing: {string.Join(", ", existingOptions)})";
                
                return (true, message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error creating options: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateOptionAsync(Guid id, UpdateOptionRequest request, string? updatedBy)
        {
            var option = await _optionRepository.GetOptionByIdAsync(id);
            if (option == null)
                return (false, "Option not found");
            
            // Check uniqueness (excluding current option)
            if (option.Name != request.Name && await _optionRepository.OptionExistsAsync(request.Name, request.ParentId, id))
                return (false, "Option name already exists with the same parent");
            
            // Validate parent if provided
            if (request.ParentId.HasValue)
            {
                // Cannot set parent to itself
                if (request.ParentId.Value == id)
                    return (false, "Cannot set an option as its own parent");
                
                // Check for circular reference
                if (await WouldCreateCircularReference(id, request.ParentId.Value))
                    return (false, "This would create a circular reference in the option hierarchy");
                
                var parent = await _optionRepository.GetOptionByIdAsync(request.ParentId.Value);
                if (parent == null)
                    return (false, "Parent option not found");
                if (!parent.HasChild && request.HasChild == "true")
                    return (false, "Selected parent option does not allow children");
            }
            
            // Get current state for logging
            var beforeSnapshot = new
            {
                option.Id,
                option.Name,
                option.ParentId,
                ParentName = option.Parent?.Name,
                option.HasChild,
                option.IsActive
            };
            
            // Parse updated by
            Guid? updatedByGuid = null;
            if (!string.IsNullOrEmpty(updatedBy) && Guid.TryParse(updatedBy, out var parsed))
                updatedByGuid = parsed;
            
            // Parse HasChild and IsActive
            bool hasChild = string.Equals(request.HasChild, "true", StringComparison.OrdinalIgnoreCase);
            bool isActive = string.Equals(request.IsActive, "true", StringComparison.OrdinalIgnoreCase);
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Store old parent for later cleanup
                var oldParentId = option.ParentId;
                
                // Update option
                option.Name = request.Name;
                option.ParentId = request.ParentId;
                option.HasChild = hasChild;
                option.IsActive = isActive;
                option.UpdatedAt = DateTime.UtcNow;
                option.UpdatedBy = updatedByGuid;
                
                _optionRepository.UpdateOption(option);
                
                // Handle parent relationships
                // If parent changed, update old parent's HasChild flag
                if (oldParentId != request.ParentId)
                {
                    if (oldParentId.HasValue)
                    {
                        var oldParent = await _optionRepository.GetOptionByIdAsync(oldParentId.Value);
                        if (oldParent != null)
                        {
                            var remainingChildren = await _optionRepository.GetChildrenCountAsync(oldParentId.Value);
                            oldParent.HasChild = remainingChildren > 0;
                            oldParent.UpdatedAt = DateTime.UtcNow;
                            oldParent.UpdatedBy = updatedByGuid;
                            _optionRepository.UpdateOption(oldParent);
                        }
                    }
                    
                    // Update new parent's HasChild flag
                    if (request.ParentId.HasValue)
                    {
                        var newParent = await _optionRepository.GetOptionByIdAsync(request.ParentId.Value);
                        if (newParent != null && !newParent.HasChild)
                        {
                            newParent.HasChild = true;
                            newParent.UpdatedAt = DateTime.UtcNow;
                            newParent.UpdatedBy = updatedByGuid;
                            _optionRepository.UpdateOption(newParent);
                        }
                    }
                }
                
                await _optionRepository.SaveChangesAsync();
                
                // Clear cache after update
                await ClearOptionsCacheAsync();
                
                // Log the action
                var afterSnapshot = new
                {
                    option.Id,
                    option.Name,
                    option.ParentId,
                    ParentName = option.Parent?.Name,
                    option.HasChild,
                    option.IsActive
                };
                
                var changesJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { before = beforeSnapshot, after = afterSnapshot });
                
                await _userLogHelper.LogAsync(
                    userId: updatedByGuid ?? Guid.Empty,
                    actionType: "Update",
                    detail: $"Option '{option.Name}' was updated",
                    changes: changesJson,
                    modelName: "Option",
                    modelId: option.Id.ToString()
                );
                
                await transaction.CommitAsync();
                
                return (true, "Option updated successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error updating option: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message, string DeleteType)> DeleteOptionAsync(Guid id, bool permanent, string? currentUserId)
        {
            var option = await _optionRepository.GetOptionByIdAsync(id);
            if (option == null)
                return (false, "Option not found", "none");
            
            if (option.IsDeleted)
                return (false, "Option is already deleted", "none");
            
            // Check if option has children
            var hasChildren = await _optionRepository.OptionHasChildrenAsync(id);
            var childrenCount = await _optionRepository.GetChildrenCountAsync(id);
            
            // Check if option can be permanently deleted
            string deleteType = "soft";
            if (permanent)
            {
                if (hasChildren)
                {
                    return (false, "Cannot permanently delete an option that has children. Please delete or reassign children first.", "none");
                }
                deleteType = "permanent";
            }
            
            Guid? deletedBy = null;
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsed))
                deletedBy = parsed;
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                await _optionRepository.DeleteOptionAsync(id, deleteType == "permanent", deletedBy);
                await _optionRepository.SaveChangesAsync();
                
                // If soft delete and this option had a parent, update parent's HasChild flag
                if (deleteType == "soft" && option.ParentId.HasValue)
                {
                    var parent = await _optionRepository.GetOptionByIdAsync(option.ParentId.Value);
                    if (parent != null)
                    {
                        var remainingChildren = await _optionRepository.GetChildrenCountAsync(option.ParentId.Value);
                        if (remainingChildren == 0)
                        {
                            parent.HasChild = false;
                            parent.UpdatedAt = DateTime.UtcNow;
                            parent.UpdatedBy = deletedBy;
                            _optionRepository.UpdateOption(parent);
                            await _optionRepository.SaveChangesAsync();
                        }
                    }
                }
                
                // Clear cache after delete
                await ClearOptionsCacheAsync();
                
                // Log the action
                await _userLogHelper.LogAsync(
                    userId: deletedBy ?? Guid.Empty,
                    actionType: "Delete",
                    detail: $"Option '{option.Name}' was {(deleteType == "permanent" ? "permanently" : "soft")} deleted. Children affected: {childrenCount}",
                    changes: Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        before = new { option.Id, option.Name, option.ParentId, option.HasChild, option.IsActive, option.IsDeleted },
                        after = new { IsDeleted = true, DeletedAt = DateTime.UtcNow }
                    }),
                    modelName: "Option",
                    modelId: option.Id.ToString()
                );
                
                await transaction.CommitAsync();
                
                return (true, $"Option {(deleteType == "permanent" ? "permanently" : "soft")} deleted successfully", deleteType);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error deleting option: {ex.Message}", "none");
            }
        }

        public async Task<(bool Success, string Message)> RestoreOptionAsync(Guid id, string? currentUserId)
        {
            var option = await _context.Options.IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == id && o.IsDeleted);
            
            if (option == null)
                return (false, "Option not found or not deleted");
            
            Guid? restoredBy = null;
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsed))
                restoredBy = parsed;
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                option.IsDeleted = false;
                option.DeletedAt = null;
                option.DeletedBy = null;
                option.UpdatedBy = restoredBy;
                option.UpdatedAt = DateTime.UtcNow;
                
                // If this option has a parent, update parent's HasChild flag
                if (option.ParentId.HasValue)
                {
                    var parent = await _optionRepository.GetOptionByIdAsync(option.ParentId.Value);
                    if (parent != null && !parent.HasChild)
                    {
                        parent.HasChild = true;
                        parent.UpdatedAt = DateTime.UtcNow;
                        parent.UpdatedBy = restoredBy;
                        _optionRepository.UpdateOption(parent);
                    }
                }
                
                _optionRepository.UpdateOption(option);
                await _optionRepository.SaveChangesAsync();
                
                // Clear cache after restore
                await ClearOptionsCacheAsync();
                
                await _userLogHelper.LogAsync(
                    userId: restoredBy ?? Guid.Empty,
                    actionType: "Restore",
                    detail: $"Option '{option.Name}' was restored",
                    changes: Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        before = new { IsDeleted = true },
                        after = new { IsDeleted = false }
                    }),
                    modelName: "Option",
                    modelId: option.Id.ToString()
                );
                
                await transaction.CommitAsync();
                
                return (true, "Option restored successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error restoring option: {ex.Message}");
            }
        }

        public async Task<DeleteOptionInfoDto> CheckDeleteEligibilityAsync(Guid id)
        {
            var option = await _optionRepository.GetOptionByIdAsync(id);
            if (option == null)
                return new DeleteOptionInfoDto { CanBePermanent = false, Message = "Option not found", HasChildren = false, ChildrenCount = 0 };
            
            if (option.IsDeleted)
                return new DeleteOptionInfoDto { CanBePermanent = false, Message = "Option is already deleted", HasChildren = false, ChildrenCount = 0 };
            
            var hasChildren = await _optionRepository.OptionHasChildrenAsync(id);
            var childrenCount = await _optionRepository.GetChildrenCountAsync(id);
            
            bool canBePermanent = !hasChildren;
            string message = canBePermanent
                ? "Option can be permanently deleted"
                : "Option must be soft deleted because it has child options. Only Developer type users can delete options with children.";
            
            return new DeleteOptionInfoDto
            {
                CanBePermanent = canBePermanent,
                Message = message,
                HasChildren = hasChildren,
                ChildrenCount = childrenCount
            };
        }

        public async Task<IEnumerable<SelectOptionDto>> GetParentOptionsAsync(SelectRequestDto? req = null)
        {
            req ??= new SelectRequestDto();
            
            string cacheKey = $"ParentOptions:{req.Search}:{req.Skip}:{req.Limit}";
            
            var cached = await _cache.StringGetAsync(cacheKey);
            if (cached.HasValue)
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<SelectOptionDto>>(cached!) ?? new List<SelectOptionDto>();
            }
            
            var parents = await _optionRepository.GetParentOptionsAsync(true);
            
            var query = parents.AsQueryable();
            
            if (!string.IsNullOrWhiteSpace(req.Search))
            {
                query = query.Where(o => o.Name.Contains(req.Search, StringComparison.OrdinalIgnoreCase));
            }
            
            var result = query
                .Skip(req.Skip)
                .Take(req.Limit > 0 ? req.Limit : int.MaxValue)
                .Select(o => new SelectOptionDto
                {
                    Value = o.Id.ToString(),
                    Label = o.Name
                })
                .ToList();
            
            await _cache.StringSetAsync(cacheKey, System.Text.Json.JsonSerializer.Serialize(result), _cacheTtl);
            
            return result;
        }
        
        private async Task ClearOptionsCacheAsync()
        {
            try
            {
                var endpoints = _redis.GetEndPoints();
                foreach (var endpoint in endpoints)
                {
                    var server = _redis.GetServer(endpoint);
                    
                    // Get all keys using the server directly
                    var optionsKeys = server.Keys(pattern: "Options:*").ToList();
                    if (optionsKeys.Any())
                    {
                        await _cache.KeyDeleteAsync(optionsKeys.ToArray());
                        Console.WriteLine($"Cleared {optionsKeys.Count} Options cache keys");
                    }
                    
                    var parentKeys = server.Keys(pattern: "ParentOptions:*").ToList();
                    if (parentKeys.Any())
                    {
                        await _cache.KeyDeleteAsync(parentKeys.ToArray());
                        Console.WriteLine($"Cleared {parentKeys.Count} ParentOptions cache keys");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing options cache: {ex.Message}");
                
                // Last resort: Try to use string pattern deletion with Lua
                try
                {
                    var luaScript = @"
                        local keys = redis.call('keys', ARGV[1])
                        if #keys > 0 then
                            for i=1,#keys do
                                redis.call('del', keys[i])
                            end
                        end
                        return #keys
                    ";
                    
                    var result = await _cache.ScriptEvaluateAsync(luaScript, values: new RedisValue[] { "Options:*" });
                    Console.WriteLine($"Cleared {result} Options cache keys via Lua");
                    
                    result = await _cache.ScriptEvaluateAsync(luaScript, values: new RedisValue[] { "ParentOptions:*" });
                    Console.WriteLine($"Cleared {result} ParentOptions cache keys via Lua");
                }
                catch (Exception innerEx)
                {
                    Console.WriteLine($"Lua script fallback failed: {innerEx.Message}");
                }
            }
        }
        
        private async Task<bool> WouldCreateCircularReference(Guid optionId, Guid newParentId)
        {
            var currentParentId = newParentId;
            var visitedIds = new HashSet<Guid> { optionId };
            
            while (currentParentId != Guid.Empty)
            {
                if (visitedIds.Contains(currentParentId))
                    return true;
                
                visitedIds.Add(currentParentId);
                
                var parent = await _optionRepository.GetOptionByIdAsync(currentParentId);
                if (parent == null || !parent.ParentId.HasValue)
                    break;
                
                currentParentId = parent.ParentId.Value;
            }
            
            return false;
        }
    }
}