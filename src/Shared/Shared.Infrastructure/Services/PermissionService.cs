using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Permissions;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Helpers;
using shop_back.src.Shared.Infrastructure.Data;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly IRolePermissionRepository _repo;
        private readonly AppDbContext _context;
        private readonly UserLogHelper _userLogHelper;

        public PermissionService(IRolePermissionRepository repo, AppDbContext context, UserLogHelper userLogHelper)
        {
            _repo = repo;
            _context = context;
            _userLogHelper = userLogHelper;
        }

        public async Task<object> GetPermissionsAsync(RolePermissionFilterRequest request)
        {
            var (permissions, totalCount, grandTotalCount, pageIndex, pageSize) = await _repo.GetFilteredPermissionsAsync(request);
            
            return new
            {
                permissions,
                totalCount,
                grandTotalCount,
                pageIndex,
                pageSize
            };
        }

        public async Task<PermissionDto?> GetPermissionAsync(Guid id)
        {
            var permission = await _repo.GetPermissionByIdAsync(id);
            if (permission == null) return null;
            
            var roles = await _repo.GetRolesByPermissionIdAsync(permission.Id);
            
            return new PermissionDto
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
            };
        }

        public async Task<PermissionDto?> GetPermissionForEditAsync(Guid id)
        {
            return await GetPermissionAsync(id);
        }

        public async Task<(bool Success, string Message)> CreatePermissionAsync(CreatePermissionRequest request, string? createdBy)
        {
            if (string.IsNullOrWhiteSpace(request.Names))
                return (false, "Permission names are required");
            
            var permissionNames = NameExpander.ExpandNames(request.Names);
            
            if (!permissionNames.Any())
                return (false, "At least one valid permission name is required");
            
            if (permissionNames.Count != permissionNames.Distinct().Count())
                return (false, "Duplicate permission names found in request");
            
            var existingPermissions = new List<string>();
            var validPermissions = new List<string>();
            
            foreach (var permissionName in permissionNames)
            {
                if (await _repo.PermissionExistsAsync(permissionName))
                {
                    existingPermissions.Add(permissionName);
                }
                else
                {
                    validPermissions.Add(permissionName);
                }
            }
            
            if (existingPermissions.Any() && !validPermissions.Any())
            {
                return (false, $"Permission(s) already exist: {string.Join(", ", existingPermissions)}");
            }
            
            bool isActive = string.Equals(request.IsActive, "true", StringComparison.OrdinalIgnoreCase);
            
            Guid? createdByGuid = null;
            if (!string.IsNullOrEmpty(createdBy) && Guid.TryParse(createdBy, out var parsed))
                createdByGuid = parsed;
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var createdPermissions = new List<Permission>();
                
                foreach (var permissionName in validPermissions)
                {
                    var permission = new Permission
                    {
                        Id = Guid.NewGuid(),
                        Name = permissionName,
                        GuardName = request.GuardName,
                        IsActive = isActive,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = createdByGuid,
                        UpdatedBy = createdByGuid
                    };
                    
                    await _repo.CreatePermissionAsync(permission);
                    createdPermissions.Add(permission);
                    
                    if (request.Roles.Any())
                    {
                        var expandedRoles = NameExpander.ExpandNames(string.Join("=", request.Roles));
                        await _repo.AssignRolesToPermissionAsync(permission.Id, expandedRoles);
                    }
                }
                
                await _repo.SaveChangesAsync();
                
                var afterSnapshot = new
                {
                    Permissions = createdPermissions.Select(p => new { p.Id, p.Name, p.GuardName, p.IsActive }),
                    Roles = request.Roles
                };
                
                var changesJson = JsonConvert.SerializeObject(new { before = (object?)null, after = afterSnapshot });
                
                await _userLogHelper.LogAsync(
                    userId: createdByGuid ?? Guid.Empty,
                    actionType: "Create",
                    detail: $"{createdPermissions.Count} permission(s) created: {string.Join(", ", createdPermissions.Select(p => p.Name))}" +
                            (existingPermissions.Any() ? $" (Skipped existing: {string.Join(", ", existingPermissions)})" : ""),
                    changes: changesJson,
                    modelName: "Permission",
                    modelId: createdPermissions.First().Id.ToString()
                );
                
                await transaction.CommitAsync();
                
                var message = $"{createdPermissions.Count} permission(s) created successfully";
                if (existingPermissions.Any())
                    message += $" (Skipped existing: {string.Join(", ", existingPermissions)})";
                
                return (true, message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error creating permissions: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdatePermissionAsync(Guid id, UpdatePermissionRequest request, string? updatedBy)
        {
            var permission = await _repo.GetPermissionByIdAsync(id);
            if (permission == null)
                return (false, "Permission not found");
            
            if (permission.Name != request.Name && await _repo.PermissionExistsAsync(request.Name, id))
                return (false, "Permission name already exists");
            
            var beforeSnapshot = new
            {
                permission.Id,
                permission.Name,
                permission.GuardName,
                permission.IsActive,
                Roles = await _repo.GetRolesByPermissionIdAsync(permission.Id)
            };
            
            Guid? updatedByGuid = null;
            if (!string.IsNullOrEmpty(updatedBy) && Guid.TryParse(updatedBy, out var parsed))
                updatedByGuid = parsed;
            
            bool isActive = string.Equals(request.IsActive, "true", StringComparison.OrdinalIgnoreCase);
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                permission.Name = request.Name;
                permission.GuardName = request.GuardName;
                permission.IsActive = isActive;
                permission.UpdatedAt = DateTime.UtcNow;
                permission.UpdatedBy = updatedByGuid;
                
                _repo.UpdatePermission(permission);
                await _repo.AssignRolesToPermissionAsync(permission.Id, request.Roles);
                await _repo.SaveChangesAsync();
                
                var afterSnapshot = new
                {
                    permission.Id,
                    permission.Name,
                    permission.GuardName,
                    permission.IsActive,
                    Roles = request.Roles
                };
                
                var changesJson = JsonConvert.SerializeObject(new { before = beforeSnapshot, after = afterSnapshot });
                
                await _userLogHelper.LogAsync(
                    userId: updatedByGuid ?? Guid.Empty,
                    actionType: "Update",
                    detail: $"Permission '{permission.Name}' was updated",
                    changes: changesJson,
                    modelName: "Permission",
                    modelId: permission.Id.ToString()
                );
                
                await transaction.CommitAsync();
                
                return (true, "Permission updated successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error updating permission: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message, string DeleteType)> DeletePermissionAsync(Guid id, bool permanent, string? currentUserId)
        {
            var permission = await _repo.GetPermissionByIdIncludingDeletedAsync(id);
            if (permission == null)
                return (false, "Permission not found", "none");
            
            Guid? deletedBy = null;
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsed))
                deletedBy = parsed;
            
            string deleteType = "soft";
            
            if (permanent)
            {
                // No check for related records - always allow permanent delete
                deleteType = "permanent";
            }
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                await _repo.DeletePermissionAsync(id, deleteType == "permanent", deletedBy);
                await _repo.SaveChangesAsync();
                
                await _userLogHelper.LogAsync(
                    userId: deletedBy ?? Guid.Empty,
                    actionType: "Delete",
                    detail: $"Permission '{permission.Name}' was {(deleteType == "permanent" ? "permanently" : "soft")} deleted",
                    changes: JsonConvert.SerializeObject(new
                    {
                        before = new { permission.Id, permission.Name, permission.IsActive, permission.IsDeleted },
                        after = new { IsDeleted = true, DeletedAt = DateTime.UtcNow }
                    }),
                    modelName: "Permission",
                    modelId: permission.Id.ToString()
                );
                
                await transaction.CommitAsync();
                
                return (true, $"Permission {(deleteType == "permanent" ? "permanently" : "soft")} deleted successfully", deleteType);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error deleting permission: {ex.Message}", "none");
            }
        }

        public async Task<(bool Success, string Message)> RestorePermissionAsync(Guid id, string? currentUserId)
        {
            var permission = await _context.Permissions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted);
            
            if (permission == null)
                return (false, "Permission not found or not deleted");
            
            Guid? restoredBy = null;
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsed))
                restoredBy = parsed;
            
            permission.IsDeleted = false;
            permission.DeletedAt = null;
            permission.UpdatedBy = restoredBy;
            permission.UpdatedAt = DateTime.UtcNow;
            
            _repo.UpdatePermission(permission);
            await _repo.SaveChangesAsync();
            
            await _userLogHelper.LogAsync(
                userId: restoredBy ?? Guid.Empty,
                actionType: "Restore",
                detail: $"Permission '{permission.Name}' was restored",
                changes: JsonConvert.SerializeObject(new
                {
                    before = new { IsDeleted = true },
                    after = new { IsDeleted = false }
                }),
                modelName: "Permission",
                modelId: permission.Id.ToString()
            );
            
            return (true, "Permission restored successfully");
        }

        public async Task<DeleteEligibilityResponse> CheckDeleteEligibilityAsync(Guid id)
        {
            var permission = await _repo.GetPermissionByIdIncludingDeletedAsync(id);
            if (permission == null)
                return new DeleteEligibilityResponse 
                { 
                    Success = false, 
                    Message = "Permission not found", 
                    CanBePermanent = false,
                    HasRelatedRecords = false
                };
            
            if (permission.IsDeleted)
                return new DeleteEligibilityResponse 
                { 
                    Success = true, 
                    Message = "Permission is in trash and can be permanently deleted. All related role and user assignments will be removed automatically.", 
                    CanBePermanent = true,
                    HasRelatedRecords = false
                };
            
            // Always allow permanent delete with auto-cleanup of relations
            return new DeleteEligibilityResponse
            {
                Success = true,
                Message = "Permission can be permanently deleted. Any assigned roles and user assignments will be automatically removed.",
                CanBePermanent = true,
                HasRelatedRecords = false
            };
        }
        // Add these methods at the end of the class:

        public async Task<BulkOperationResponse> BulkDeletePermissionsAsync(List<Guid> ids, bool permanent, string? currentUserId)
        {
            Guid? deletedBy = null;
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsed))
                deletedBy = parsed;

            var result = await _repo.BulkDeletePermissionsAsync(ids, permanent, deletedBy);
            
            // Log the bulk operation
            if (result.SuccessCount > 0)
            {
                await _userLogHelper.LogAsync(
                    userId: deletedBy ?? Guid.Empty,
                    actionType: "BulkDelete",
                    detail: $"Bulk {(permanent ? "permanent" : "soft")} delete of {result.SuccessCount} permission(s). Failed: {result.FailedCount}",
                    changes: JsonConvert.SerializeObject(new
                    {
                        ids = ids,
                        permanent = permanent,
                        successCount = result.SuccessCount,
                        failedCount = result.FailedCount,
                        errors = result.Errors
                    }),
                    modelName: "Permission",
                    modelId: "bulk"
                );
            }
            
            return result;
        }

        public async Task<BulkOperationResponse> BulkRestorePermissionsAsync(List<Guid> ids, string? currentUserId)
        {
            Guid? restoredBy = null;
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsed))
                restoredBy = parsed;

            var result = await _repo.BulkRestorePermissionsAsync(ids, restoredBy);
            
            // Log the bulk operation
            if (result.SuccessCount > 0)
            {
                await _userLogHelper.LogAsync(
                    userId: restoredBy ?? Guid.Empty,
                    actionType: "BulkRestore",
                    detail: $"Bulk restore of {result.SuccessCount} permission(s). Failed: {result.FailedCount}",
                    changes: JsonConvert.SerializeObject(new
                    {
                        ids = ids,
                        successCount = result.SuccessCount,
                        failedCount = result.FailedCount,
                        errors = result.Errors
                    }),
                    modelName: "Permission",
                    modelId: "bulk"
                );
            }
            
            return result;
        }
    }
}