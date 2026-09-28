using Newtonsoft.Json;
using shop_back.src.Shared.Application.DTOs.PermissionGroups;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Infrastructure.Helpers;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class PermissionGroupService : IPermissionGroupService
    {
        private readonly IPermissionGroupRepository _repo;
        private readonly AppDbContext _context;
        private readonly UserLogHelper _userLogHelper;

        public PermissionGroupService(IPermissionGroupRepository repo, AppDbContext context, UserLogHelper userLogHelper)
        {
            _repo = repo;
            _context = context;
            _userLogHelper = userLogHelper;
        }

        private static Guid? ParseUser(string? id) => Guid.TryParse(id, out var g) ? g : null;
        private static bool IsTrue(string? v) => v == null || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);

        public Task<List<PermissionGroupDto>> GetGroupsAsync(string? search, bool activeOnly) => _repo.GetAllAsync(search, activeOnly);

        public Task<PermissionGroupDto?> GetGroupAsync(Guid id) => _repo.GetDtoAsync(id);

        public async Task<(bool Success, string Message, Guid? Id)> CreateGroupAsync(SavePermissionGroupRequest request, string? currentUserId)
        {
            var name = request.Name?.Trim() ?? string.Empty;
            if (name.Length == 0) return (false, "Group name is required", null);
            if (await _repo.NameExistsAsync(name)) return (false, "A group with this name already exists", null);

            var by = ParseUser(currentUserId);
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var group = new PermissionGroup
                {
                    Name = name,
                    Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                    IsActive = IsTrue(request.IsActive),
                    CreatedBy = by,
                    UpdatedBy = by,
                };
                await _repo.AddAsync(group);
                await _repo.SaveChangesAsync();
                await _repo.SetPermissionsAsync(group.Id, request.Permissions);
                await _repo.SaveChangesAsync();

                await _userLogHelper.LogAsync(
                    userId: by ?? Guid.Empty,
                    actionType: "Create",
                    detail: $"Permission group '{group.Name}' created",
                    changes: JsonConvert.SerializeObject(new
                    {
                        before = (object?)null,
                        after = new { group.Name, group.Description, group.IsActive, Permissions = request.Permissions },
                    }),
                    modelName: "PermissionGroup",
                    modelId: group.Id.ToString());

                await transaction.CommitAsync();
                return (true, "Permission group created successfully", group.Id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error creating permission group: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message)> UpdateGroupAsync(Guid id, SavePermissionGroupRequest request, string? currentUserId)
        {
            var group = await _repo.GetByIdAsync(id);
            if (group == null) return (false, "Permission group not found");

            var name = request.Name?.Trim() ?? string.Empty;
            if (name.Length == 0) return (false, "Group name is required");
            if (await _repo.NameExistsAsync(name, id)) return (false, "A group with this name already exists");

            var before = await _repo.GetDtoAsync(id);
            var by = ParseUser(currentUserId);
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                group.Name = name;
                group.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
                group.IsActive = IsTrue(request.IsActive);
                group.UpdatedAt = DateTime.UtcNow;
                group.UpdatedBy = by;
                await _repo.SetPermissionsAsync(group.Id, request.Permissions);
                await _repo.SaveChangesAsync();

                await _userLogHelper.LogAsync(
                    userId: by ?? Guid.Empty,
                    actionType: "Update",
                    detail: $"Permission group '{group.Name}' was updated",
                    changes: JsonConvert.SerializeObject(new
                    {
                        before = before == null ? null : new { before.Name, before.Description, before.IsActive, before.Permissions },
                        after = new { group.Name, group.Description, group.IsActive, Permissions = request.Permissions },
                    }),
                    modelName: "PermissionGroup",
                    modelId: group.Id.ToString());

                await transaction.CommitAsync();
                return (true, "Permission group updated successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error updating permission group: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteGroupAsync(Guid id, string? currentUserId)
        {
            var group = await _repo.GetByIdAsync(id);
            if (group == null) return (false, "Permission group not found");

            var before = await _repo.GetDtoAsync(id);
            var by = ParseUser(currentUserId);
            // Links to permissions, roles and users go with it (ON DELETE CASCADE).
            _repo.Remove(group);
            await _repo.SaveChangesAsync();

            await _userLogHelper.LogAsync(
                userId: by ?? Guid.Empty,
                actionType: "Delete",
                detail: $"Permission group '{group.Name}' deleted",
                changes: JsonConvert.SerializeObject(new { before, after = (object?)null }),
                modelName: "PermissionGroup",
                modelId: group.Id.ToString());

            return (true, "Permission group deleted successfully");
        }
    }
}
