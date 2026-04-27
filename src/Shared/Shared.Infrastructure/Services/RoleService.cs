using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Roles;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Helpers;
using shop_back.src.Shared.Infrastructure.Data;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class RoleService : IRoleService
    {
        private readonly IRolePermissionRepository _repo;
        private readonly AppDbContext _context;
        private readonly UserLogHelper _userLogHelper;

        public RoleService(IRolePermissionRepository repo, AppDbContext context, UserLogHelper userLogHelper)
        {
            _repo = repo;
            _context = context;
            _userLogHelper = userLogHelper;
        }

        public async Task<object> GetRolesAsync(RolePermissionFilterRequest request)
        {
            var (roles, totalCount, grandTotalCount, pageIndex, pageSize) = await _repo.GetFilteredRolesAsync(request);
            
            return new
            {
                roles,
                totalCount,
                grandTotalCount,
                pageIndex,
                pageSize
            };
        }

        public async Task<RoleDto?> GetRoleAsync(Guid id)
        {
            var role = await _repo.GetRoleByIdAsync(id);
            if (role == null) return null;
            
            var permissions = await _repo.GetPermissionsByRoleIdAsync(role.Id);
            
            return new RoleDto
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
            };
        }

        public async Task<RoleDto?> GetRoleForEditAsync(Guid id)
        {
            return await GetRoleAsync(id);
        }

        public async Task<(bool Success, string Message)> CreateRoleAsync(CreateRoleRequest request, string? createdBy)
        {
            if (string.IsNullOrWhiteSpace(request.Names))
                return (false, "Role names are required");
            
            var roleNames = NameExpander.ExpandNames(request.Names);
            
            if (!roleNames.Any())
                return (false, "At least one valid role name is required");
            
            if (roleNames.Count != roleNames.Distinct().Count())
                return (false, "Duplicate role names found in request");
            
            var existingRoles = new List<string>();
            var validRoles = new List<string>();
            
            foreach (var roleName in roleNames)
            {
                if (await _repo.RoleExistsAsync(roleName))
                {
                    existingRoles.Add(roleName);
                }
                else
                {
                    validRoles.Add(roleName);
                }
            }
            
            if (existingRoles.Any() && !validRoles.Any())
            {
                return (false, $"Role(s) already exist: {string.Join(", ", existingRoles)}");
            }
            
            bool isActive = string.Equals(request.IsActive, "true", StringComparison.OrdinalIgnoreCase);
            
            Guid? createdByGuid = null;
            if (!string.IsNullOrEmpty(createdBy) && Guid.TryParse(createdBy, out var parsed))
                createdByGuid = parsed;
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var createdRoles = new List<Role>();
                
                foreach (var roleName in validRoles)
                {
                    var role = new Role
                    {
                        Id = Guid.NewGuid(),
                        Name = roleName,
                        GuardName = request.GuardName,
                        IsActive = isActive,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = createdByGuid,
                        UpdatedBy = createdByGuid
                    };
                    
                    await _repo.CreateRoleAsync(role);
                    createdRoles.Add(role);
                    
                    if (request.Permissions.Any())
                    {
                        var expandedPermissions = NameExpander.ExpandPermissionNames(request.Permissions);
                        await _repo.AssignPermissionsToRoleAsync(role.Id, expandedPermissions);
                    }
                }
                
                await _repo.SaveChangesAsync();
                
                var afterSnapshot = new
                {
                    Roles = createdRoles.Select(r => new { r.Id, r.Name, r.GuardName, r.IsActive }),
                    Permissions = request.Permissions
                };
                
                var changesJson = JsonConvert.SerializeObject(new { before = (object?)null, after = afterSnapshot });
                
                await _userLogHelper.LogAsync(
                    userId: createdByGuid ?? Guid.Empty,
                    actionType: "Create",
                    detail: $"{createdRoles.Count} role(s) created: {string.Join(", ", createdRoles.Select(r => r.Name))}" + 
                            (existingRoles.Any() ? $" (Skipped existing: {string.Join(", ", existingRoles)})" : ""),
                    changes: changesJson,
                    modelName: "Role",
                    modelId: createdRoles.First().Id.ToString()
                );
                
                await transaction.CommitAsync();
                
                var message = $"{createdRoles.Count} role(s) created successfully";
                if (existingRoles.Any())
                    message += $" (Skipped existing: {string.Join(", ", existingRoles)})";
                
                return (true, message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error creating roles: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UpdateRoleAsync(Guid id, UpdateRoleRequest request, string? updatedBy)
        {
            var role = await _repo.GetRoleByIdAsync(id);
            if (role == null)
                return (false, "Role not found");
            
            if (role.Name != request.Name && await _repo.RoleExistsAsync(request.Name, id))
                return (false, "Role name already exists");
            
            var beforeSnapshot = new
            {
                role.Id,
                role.Name,
                role.GuardName,
                role.IsActive,
                Permissions = await _repo.GetPermissionsByRoleIdAsync(role.Id)
            };
            
            Guid? updatedByGuid = null;
            if (!string.IsNullOrEmpty(updatedBy) && Guid.TryParse(updatedBy, out var parsed))
                updatedByGuid = parsed;
            
            bool isActive = string.Equals(request.IsActive, "true", StringComparison.OrdinalIgnoreCase);
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                role.Name = request.Name;
                role.GuardName = request.GuardName;
                role.IsActive = isActive;
                role.UpdatedAt = DateTime.UtcNow;
                role.UpdatedBy = updatedByGuid;
                
                _repo.UpdateRole(role);
                await _repo.AssignPermissionsToRoleAsync(role.Id, request.Permissions);
                await _repo.SaveChangesAsync();
                
                var afterSnapshot = new
                {
                    role.Id,
                    role.Name,
                    role.GuardName,
                    role.IsActive,
                    Permissions = request.Permissions
                };
                
                var changesJson = JsonConvert.SerializeObject(new { before = beforeSnapshot, after = afterSnapshot });
                
                await _userLogHelper.LogAsync(
                    userId: updatedByGuid ?? Guid.Empty,
                    actionType: "Update",
                    detail: $"Role '{role.Name}' was updated",
                    changes: changesJson,
                    modelName: "Role",
                    modelId: role.Id.ToString()
                );
                
                await transaction.CommitAsync();
                
                return (true, "Role updated successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error updating role: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message, string DeleteType)> DeleteRoleAsync(Guid id, bool permanent, string? currentUserId)
        {
            var role = await _repo.GetRoleByIdIncludingDeletedAsync(id);
            if (role == null)
                return (false, "Role not found", "none");
            
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
                await _repo.DeleteRoleAsync(id, deleteType == "permanent", deletedBy);
                await _repo.SaveChangesAsync();
                
                await _userLogHelper.LogAsync(
                    userId: deletedBy ?? Guid.Empty,
                    actionType: "Delete",
                    detail: $"Role '{role.Name}' was {(deleteType == "permanent" ? "permanently" : "soft")} deleted",
                    changes: JsonConvert.SerializeObject(new
                    {
                        before = new { role.Id, role.Name, role.IsActive, role.IsDeleted },
                        after = new { IsDeleted = true, DeletedAt = DateTime.UtcNow }
                    }),
                    modelName: "Role",
                    modelId: role.Id.ToString()
                );
                
                await transaction.CommitAsync();
                
                return (true, $"Role {(deleteType == "permanent" ? "permanently" : "soft")} deleted successfully", deleteType);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error deleting role: {ex.Message}", "none");
            }
        }

        public async Task<(bool Success, string Message)> RestoreRoleAsync(Guid id, string? currentUserId)
        {
            var role = await _context.Roles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == id && r.IsDeleted);
            
            if (role == null)
                return (false, "Role not found or not deleted");
            
            Guid? restoredBy = null;
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsed))
                restoredBy = parsed;
            
            role.IsDeleted = false;
            role.DeletedAt = null;
            role.UpdatedBy = restoredBy;
            role.UpdatedAt = DateTime.UtcNow;
            
            _repo.UpdateRole(role);
            await _repo.SaveChangesAsync();
            
            await _userLogHelper.LogAsync(
                userId: restoredBy ?? Guid.Empty,
                actionType: "Restore",
                detail: $"Role '{role.Name}' was restored",
                changes: JsonConvert.SerializeObject(new
                {
                    before = new { IsDeleted = true },
                    after = new { IsDeleted = false }
                }),
                modelName: "Role",
                modelId: role.Id.ToString()
            );
            
            return (true, "Role restored successfully");
        }

        public async Task<DeleteEligibilityResponse> CheckDeleteEligibilityAsync(Guid id)
        {
            var role = await _repo.GetRoleByIdIncludingDeletedAsync(id);
            if (role == null)
                return new DeleteEligibilityResponse 
                { 
                    Success = false, 
                    Message = "Role not found", 
                    CanBePermanent = false,
                    HasRelatedRecords = false
                };
            
            if (role.IsDeleted)
                return new DeleteEligibilityResponse 
                { 
                    Success = true, 
                    Message = "Role is in trash and can be permanently deleted. All related permissions and user assignments will be removed automatically.", 
                    CanBePermanent = true,
                    HasRelatedRecords = false
                };
            
            // Always allow permanent delete with auto-cleanup of relations
            return new DeleteEligibilityResponse
            {
                Success = true,
                Message = "Role can be permanently deleted. Any assigned permissions and user assignments will be automatically removed.",
                CanBePermanent = true,
                HasRelatedRecords = false
            };
        }
        // Add these methods at the end of the class:

        public async Task<BulkOperationResponse> BulkDeleteRolesAsync(List<Guid> ids, bool permanent, string? currentUserId)
        {
            Guid? deletedBy = null;
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsed))
                deletedBy = parsed;

            var result = await _repo.BulkDeleteRolesAsync(ids, permanent, deletedBy);
            
            // Log the bulk operation
            if (result.SuccessCount > 0)
            {
                await _userLogHelper.LogAsync(
                    userId: deletedBy ?? Guid.Empty,
                    actionType: "BulkDelete",
                    detail: $"Bulk {(permanent ? "permanent" : "soft")} delete of {result.SuccessCount} role(s). Failed: {result.FailedCount}",
                    changes: JsonConvert.SerializeObject(new
                    {
                        ids = ids,
                        permanent = permanent,
                        successCount = result.SuccessCount,
                        failedCount = result.FailedCount,
                        errors = result.Errors
                    }),
                    modelName: "Role",
                    modelId: "bulk"
                );
            }
            
            return result;
        }

        public async Task<BulkOperationResponse> BulkRestoreRolesAsync(List<Guid> ids, string? currentUserId)
        {
            Guid? restoredBy = null;
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsed))
                restoredBy = parsed;

            var result = await _repo.BulkRestoreRolesAsync(ids, restoredBy);
            
            // Log the bulk operation
            if (result.SuccessCount > 0)
            {
                await _userLogHelper.LogAsync(
                    userId: restoredBy ?? Guid.Empty,
                    actionType: "BulkRestore",
                    detail: $"Bulk restore of {result.SuccessCount} role(s). Failed: {result.FailedCount}",
                    changes: JsonConvert.SerializeObject(new
                    {
                        ids = ids,
                        successCount = result.SuccessCount,
                        failedCount = result.FailedCount,
                        errors = result.Errors
                    }),
                    modelName: "Role",
                    modelId: "bulk"
                );
            }
            
            return result;
        }
    }
}