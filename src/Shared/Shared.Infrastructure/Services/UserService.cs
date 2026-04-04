using BCrypt.Net;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Users;
using shop_back.src.Shared.Application.DTOs.Auth;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Helpers; 
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Newtonsoft.Json;
using System.Text.RegularExpressions; 
using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Infrastructure.Data;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repo;
        private readonly IRolePermissionRepository _rolePermissionRepo;
        private readonly UserLogHelper _userLogHelper;
        private readonly IMailVerificationService _mailVerificationService;
        private readonly AppDbContext _context;
        private readonly IChangePasswordService _changePasswordService;

        public UserService(AppDbContext context, IUserRepository repo, IRolePermissionRepository rolePermissionRepo, UserLogHelper userLogHelper, IMailVerificationService mailVerificationService, IChangePasswordService changePasswordService)   
        {
            _context = context;
            _repo = repo;
            _rolePermissionRepo = rolePermissionRepo;
            _userLogHelper = userLogHelper;
            _mailVerificationService = mailVerificationService;
            _changePasswordService = changePasswordService;
        }

        public async Task<object> GetUsersAsync(UserFilterRequest request)
        {
            var (users, totalCount, grandTotalCount, pageIndex, pageSize) = await _repo.GetFilteredAsync(request);

            return new
            {
                users,
                totalCount,
                grandTotalCount,
                pageIndex,
                pageSize
            };
        }

        public async Task<UserDto?> GetUserAsync(Guid id)
        {
            var user = await _repo.GetByIdAsync(id);
            if (user == null) return null;

            var roles = await _rolePermissionRepo.GetRoleNamesByUserIdAsync(user.Id) ?? Array.Empty<string>();
            var permissions = await _rolePermissionRepo.GetAllPermissionsByUserIdAsync(user.Id) ?? Array.Empty<string>();

            return new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Username = user.Username,
                Email = user.Email,
                EmailVerifiedAt = user.EmailVerifiedAt,
                MobileNo = user.MobileNo,
                ProfileImage = user.ProfileImage,
                Bio = user.Bio,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                Address = user.Address,
                QRCode = user.QRCode,
                Timezone = user.Timezone,
                NID = user.NID,
                Language = user.Language,
                IsActive = user.IsActive,
                IsDeleted = user.IsDeleted,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                Roles = roles,
                Permissions = permissions
            };
        }

        public async Task<UserDto?> GetUserForEditAsync(Guid id)
        {
            var user = await _repo.GetByIdAsync(id);
            if (user == null) return null;

            var roles = await _rolePermissionRepo.GetRoleNamesByUserIdAsync(user.Id) ?? Array.Empty<string>();
            var allPermissions = await _rolePermissionRepo.GetAllPermissionsByUserIdAsync(user.Id) ?? Array.Empty<string>();
            var rolePermissions = await _rolePermissionRepo.GetPermissionsByRolesAsync(roles) ?? Array.Empty<string>();

            var directPermissions = allPermissions.Except(rolePermissions).ToArray();

            return new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Username = user.Username,
                Email = user.Email,
                EmailVerifiedAt = user.EmailVerifiedAt,
                MobileNo = user.MobileNo,
                ProfileImage = user.ProfileImage,
                Bio = user.Bio,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                Address = user.Address,
                QRCode = user.QRCode,
                Timezone = user.Timezone,
                NID = user.NID,
                Language = user.Language,
                IsActive = user.IsActive,
                IsDeleted = user.IsDeleted,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                CreatedByName = user.CreatedBy.HasValue
                    ? _context.Users.FirstOrDefault(u => u.Id == user.CreatedBy.Value)?.Name
                    : null,
                UpdatedByName = user.UpdatedBy.HasValue
                    ? _context.Users.FirstOrDefault(u => u.Id == user.UpdatedBy.Value)?.Name
                    : null,
                Roles = roles,
                Permissions = directPermissions
            };
        }
    
        public async Task<(bool Success, string Message)> CreateUserAsync(
            CreateUserRequest request,
            string? currentUserId)
        {
            // 🔹 1️⃣ Validation (service layer)
            if (string.IsNullOrWhiteSpace(request.Name))
                return (false, "Name is required");
            if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 4)
                return (false, "Username must be at least 4 characters");
            if (string.IsNullOrWhiteSpace(request.Email))
                return (false, "Email is required");

            var passwordRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{6,}$");
            if (!passwordRegex.IsMatch(request.Password))
                return (false, "Password must contain uppercase, lowercase, number and special character.");

            if (request.Password != request.ConfirmedPassword)
                return (false, "Passwords do not match");

            if (request.Roles == null || !request.Roles.Any())
                return (false, "At least one role must be selected");

            // 🔹 2️⃣ Parse Current UserId safely
            Guid? createdByGuid = null;
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsedGuid))
                createdByGuid = parsedGuid;

            // 🔹 3️⃣ Check uniqueness via repository
            if (await _repo.GetByIdentifierAsync(request.Username) != null)
                return (false, "Username already exists");
            if (await _repo.GetByEmailAsync(request.Email) != null)
                return (false, "Email already exists");
            if (!string.IsNullOrEmpty(request.NID) && await _repo.GetByIdentifierAsync(request.NID) != null)
                return (false, "NID already exists");

            // 🔹 4️⃣ Convert IsActive safely
            bool isActive = string.Equals(request.IsActive, "true", StringComparison.OrdinalIgnoreCase);

            // 🔹 5️⃣ Start transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 🔹 6️⃣ Hash password
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

                // 🔹 7️⃣ Map to domain entity
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name.Trim(),
                    Username = request.Username.Trim(),
                    Email = request.Email.Trim().ToLower(),
                    Password = hashedPassword,
                    MobileNo = request.MobileNo,
                    NID = request.NID,
                    Bio = request.Bio,
                    Address = request.Address,
                    Gender = request.Gender,
                    DateOfBirth = request.DateOfBirth,
                    IsActive = isActive,
                    IsDeleted = false,
                    CreatedBy = createdByGuid,
                    UpdatedBy = createdByGuid,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // 🔹 8️⃣ Handle profile image using FileHelper with resizing
                if (request.ProfileImage != null)
                {
                    try
                    {
                        var resizeOptions = new ImageResizeOptions
                        {
                            Enabled = true,
                            MaxWidth = 500,
                            MaxHeight = 500,
                            ResizeMode = ImageResizeMode.Max
                        };
                        
                        user.ProfileImage = await FileHelper.SaveFileAsync(
                            request.ProfileImage, 
                            "users", 
                            resizeOptions
                        );
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Profile image upload failed: {ex.Message}");
                    }
                }

                // 🔹 9️⃣ Generate QR code
                user.QRCode = GenerateQRCodeString(user.Id);

                // 🔹 10️⃣ Persist via repository
                await _repo.AddAsync(user);
                await _repo.SaveChangesAsync();

                // 🔹 11️⃣ Assign Roles & Permissions via rolePermissionRepo
                await _rolePermissionRepo.AssignRolesAsync(user.Id, request.Roles.ToArray());
                if (request.Permissions?.Any() == true)
                    await _rolePermissionRepo.AssignPermissionsAsync(user.Id, request.Permissions.ToArray());

                // 🔹 12️⃣ Log snapshot
                var afterSnapshot = new
                {
                    user.Id,
                    user.Username,
                    user.Name,
                    user.Email,
                    user.MobileNo,
                    user.NID,
                    user.Gender,
                    user.DateOfBirth,
                    user.Bio,
                    user.Address,
                    user.ProfileImage,
                    user.QRCode,
                    user.IsActive,
                    Roles = await _rolePermissionRepo.GetRoleNamesByUserIdAsync(user.Id),
                    Permissions = await _rolePermissionRepo.GetAllPermissionsByUserIdAsync(user.Id)
                };

                var changesJson = JsonConvert.SerializeObject(new { before = (object?)null, after = afterSnapshot });

                await _userLogHelper.LogAsync(
                    userId: createdByGuid ?? user.Id,
                    actionType: "Create",
                    detail: $"User '{user.Username}' was created",
                    changes: changesJson,
                    modelName: "User",
                    modelId: user.Id
                );

                // 🔹 13️⃣ Send verification email
                await _mailVerificationService.SendVerificationEmailAsync(user);

                // 🔹 14️⃣ Commit transaction
                await transaction.CommitAsync();

                return (true, "User created successfully. Verification email sent.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error: {ex.Message}");
            }
        }

        private static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        }

        private static string GenerateQRCodeString(Guid userId)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var random = Path.GetRandomFileName().Replace(".", "").Substring(0, 4);
            return $"{userId:N}-{timestamp}-{random}";
        }

        public async Task<UserDto?> RegenerateQrAsync(Guid id, string? currentUserId)
        {
            var user = await _repo.GetByIdAsync(id);
            if (user == null) return null;

            user.QRCode = GenerateQRCodeString(user.Id);
            user.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsed))
                user.UpdatedBy = parsed;

            await _repo.SaveChangesAsync();

            return await GetUserAsync(id);
        }
        
        public async Task<(bool Success, string Message)> UpdateUserAsync(
            Guid id,
            UpdateUserRequest request,
            string? currentUserId)
        {
            // 1️⃣ Fetch user
            var user = await _repo.GetByIdAsync(id);
            if (user == null) return (false, "User not found");

            // 2️⃣ Validate uniqueness
            if (await _repo.ExistsByUsernameAsync(request.Username, id))
                return (false, "Username already exists");
            if (await _repo.ExistsByEmailAsync(request.Email, id))
                return (false, "Email already exists");

            // 3️⃣ Validate roles exist
            var validRoles = await _context.Roles
                .Where(r => request.Roles.Contains(r.Name))
                .Select(r => r.Name)
                .ToListAsync();

            if (validRoles.Count != request.Roles.Count)
                return (false, "One or more roles are invalid");

            // 4️⃣ Check if email changed
            bool emailChanged = !string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase);

            // 5️⃣ Update basic fields
            user.Name = request.Name;
            user.Username = request.Username;
            user.Email = request.Email;
            user.IsActive = string.Equals(request.IsActive, "true", StringComparison.OrdinalIgnoreCase);
            user.MobileNo = request.MobileNo;
            user.NID = request.NID;
            user.Address = request.Address;

            // 6️⃣ Handle ProfileImage
            if (request.RemoveProfileImage)
            {
                if (!string.IsNullOrEmpty(user.ProfileImage))
                {
                    await FileHelper.DeleteFileAsync(user.ProfileImage);
                    user.ProfileImage = null;
                }
            }
            else if (request.ProfileImage != null)
            {
                if (!string.IsNullOrEmpty(user.ProfileImage))
                {
                    await FileHelper.DeleteFileAsync(user.ProfileImage);
                }
                
                var resizeOptions = new ImageResizeOptions
                {
                    Enabled = true,
                    MaxWidth = 500,
                    MaxHeight = 500,
                    ResizeMode = ImageResizeMode.Max
                };
                
                user.ProfileImage = await FileHelper.SaveFileAsync(
                    request.ProfileImage, 
                    "users", 
                    resizeOptions
                );
            }

            // 7️⃣ Update roles and permissions
            await _rolePermissionRepo.SetRolesForUserAsync(user.Id, request.Roles);
            
            var rolePermissions = await _rolePermissionRepo.GetPermissionsByRolesAsync(request.Roles);
            var filteredPermissions = request.Permissions?
                .Where(p => !rolePermissions.Contains(p))
                .ToList() ?? new List<string>();
            
            await _rolePermissionRepo.SetPermissionsForUserAsync(user.Id, filteredPermissions);

            // 8️⃣ Handle email verification if email changed
            if (emailChanged)
            {
                user.EmailVerifiedAt = null;
                await _mailVerificationService.SendVerificationEmailAsync(user);
            }

            // 9️⃣ Audit fields
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsedUserId))
            {
                user.UpdatedBy = parsedUserId;
            }
            user.UpdatedAt = DateTime.UtcNow;

            // 🔟 Save changes
            await _repo.UpdateAsync(user);
            await _repo.SaveChangesAsync();

            return (true, "User updated successfully");
        }
        
        public async Task<UserDto?> GetProfileAsync(Guid userId)
        {
            var user = await _repo.GetByIdAsync(userId);
            if (user == null) return null;

            var roles = await _rolePermissionRepo.GetRoleNamesByUserIdAsync(user.Id) ?? Array.Empty<string>();
            var permissions = await _rolePermissionRepo.GetAllPermissionsByUserIdAsync(user.Id) ?? Array.Empty<string>();

            return new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Username = user.Username,
                Email = user.Email,
                EmailVerifiedAt = user.EmailVerifiedAt,
                MobileNo = user.MobileNo,
                ProfileImage = user.ProfileImage,
                Bio = user.Bio,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                Address = user.Address,
                NID = user.NID,
                QRCode = user.QRCode,
                IsActive = user.IsActive,
                IsDeleted = user.IsDeleted, 
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                Roles = roles,
                Permissions = permissions
            };
        }

        public async Task<(bool Success, string Message)> UpdateProfileAsync(
            Guid userId, 
            UpdateProfileRequest request)
        {
            // 1️⃣ Fetch user
            var user = await _repo.GetByIdAsync(userId);
            if (user == null) return (false, "User not found");

            if (await _repo.ExistsByEmailAsync(request.Email, userId))
                return (false, "Email already exists");
            
            if (!string.IsNullOrEmpty(request.MobileNo) && 
                request.MobileNo != user.MobileNo && 
                await _repo.ExistsByMobileNoAsync(request.MobileNo, userId))
            {
                return (false, "Mobile number already exists");
            }

            if (!string.IsNullOrEmpty(request.NID) && 
                request.NID != user.NID && 
                await _repo.ExistsByNIDAsync(request.NID, userId))
            {
                return (false, "NID already exists");
            }

            bool emailChanged = !string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase);
            
            // 4️⃣ Update profile fields
            user.Name = request.Name;
            user.MobileNo = request.MobileNo;
            user.Email = request.Email;
            user.NID = request.NID;
            user.Address = request.Address;
            user.Bio = request.Bio;
            user.Gender = request.Gender;
            user.DateOfBirth = request.DateOfBirth;

            // 5️⃣ Handle Profile Image
            if (request.RemoveProfileImage)
            {
                if (!string.IsNullOrEmpty(user.ProfileImage))
                {
                    await FileHelper.DeleteFileAsync(user.ProfileImage);
                    user.ProfileImage = null;
                }
            }
            else if (request.ProfileImage != null)
            {
                if (!string.IsNullOrEmpty(user.ProfileImage))
                {
                    await FileHelper.DeleteFileAsync(user.ProfileImage);
                }
                
                var resizeOptions = new ImageResizeOptions
                {
                    Enabled = true,
                    MaxWidth = 500,
                    MaxHeight = 500,
                    ResizeMode = ImageResizeMode.Max
                };
                
                user.ProfileImage = await FileHelper.SaveFileAsync(
                    request.ProfileImage, 
                    "users", 
                    resizeOptions
                );
            }

            if (emailChanged)
            {
                user.EmailVerifiedAt = null;
                await _mailVerificationService.SendVerificationEmailAsync(user);
            }

            // 6️⃣ Audit fields
            user.UpdatedAt = DateTime.UtcNow;

            // 7️⃣ Save changes
            await _repo.UpdateAsync(user);
            await _repo.SaveChangesAsync();

            return (true, "Profile updated successfully");
        }

        public async Task<(bool Success, string Message)> RequestPasswordChangeAsync(
            Guid userId, 
            ChangePasswordRequestDto request)
        {
            try
            {
                var result = await _changePasswordService.RequestChangePasswordAsync(userId, request);
                return (true, result.Message);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string Message)> VerifyPasswordChangeAsync(string token)
        {
            try
            {
                await _changePasswordService.CompleteChangePasswordAsync(
                    new VerifyPasswordChangeDto { Token = token });
                return (true, "Password changed successfully");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
        
        public async Task<(bool Success, string Message, string DeleteType)> DeleteUserAsync(
            Guid id, 
            bool permanent, 
            string? currentUserId)
        {
            var user = await _repo.GetByIdAsync(id);
            if (user == null) 
                return (false, "User not found", "none");
            
            Guid? deletedBy = null;
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsed))
                deletedBy = parsed;
            
            if (user.IsDeleted)
                return (false, "User is already deleted", "none");
            
            string deleteType = "soft";
            
            if (permanent)
            {
                var hasRelatedRecords = await _repo.HasRelatedRecordsAsync(id);
                var hasVerifiedEmail = await _repo.HasVerifiedEmailAsync(id);
                
                if (!hasRelatedRecords && !hasVerifiedEmail)
                {
                    deleteType = "permanent";
                }
                else
                {
                    deleteType = "soft";
                }
            }
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                if (deleteType == "permanent")
                {
                    await _repo.HardDeleteAsync(id);
                    
                    await _userLogHelper.LogAsync(
                        userId: deletedBy ?? id,
                        actionType: "Delete",
                        detail: $"User '{user.Username}' was permanently deleted",
                        changes: JsonConvert.SerializeObject(new { 
                            before = new { user.Id, user.Username, user.Email },
                        }),
                        modelName: "User",
                        modelId: id
                    );
                }
                else
                {
                    await _repo.SoftDeleteAsync(id, deletedBy);
                    
                    await _userLogHelper.LogAsync(
                        userId: deletedBy ?? id,
                        actionType: "Delete",
                        detail: $"User '{user.Username}' was soft deleted",
                        changes: JsonConvert.SerializeObject(new { 
                            before = new { user.Id, user.Username, user.Email, IsDeleted = false },
                            after = new { IsDeleted = true, DeletedAt = DateTime.UtcNow }
                        }),
                        modelName: "User",
                        modelId: id
                    );
                }
                
                await _repo.SaveChangesAsync();
                await transaction.CommitAsync();
                
                return (true, $"User {(deleteType == "permanent" ? "permanently" : "soft")} deleted successfully", deleteType);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Error deleting user: {ex.Message}", "none");
            }
        }

        public async Task<(bool Success, string Message)> RestoreUserAsync(Guid id, string? currentUserId)
        {
            var user = await _context.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == id && u.IsDeleted);
            
            if (user == null)
                return (false, "User not found or not deleted");
            
            Guid? restoredBy = null;
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsed))
                restoredBy = parsed;
            
            user.IsDeleted = false;
            user.DeletedAt = null;
            user.UpdatedBy = restoredBy;
            user.UpdatedAt = DateTime.UtcNow;
            
            await _repo.UpdateAsync(user);
            await _repo.SaveChangesAsync();
            
            await _userLogHelper.LogAsync(
                userId: restoredBy ?? id,
                actionType: "Restore",
                detail: $"User '{user.Username}' was restored",
                changes: JsonConvert.SerializeObject(new { 
                    before = new { IsDeleted = true, DeletedAt = user.DeletedAt },
                    after = new { IsDeleted = false }
                }),
                modelName: "User",
                modelId: id
            );
            
            return (true, "User restored successfully");
        }

        public async Task<(bool Success, string Message, bool CanBePermanent)> CheckDeleteEligibilityAsync(Guid id)
        {
            var user = await _repo.GetByIdAsync(id);
            if (user == null)
                return (false, "User not found", false);
            
            if (user.IsDeleted)
                return (false, "User is already deleted", false);
            
            var hasRelatedRecords = await _repo.HasRelatedRecordsAsync(id);
            var hasVerifiedEmail = await _repo.HasVerifiedEmailAsync(id);
            
            bool canBePermanent = !hasRelatedRecords && !hasVerifiedEmail;
            string message = canBePermanent 
                ? "User can be permanently deleted" 
                : "User must be soft deleted due to existing related records";
            
            return (true, message, canBePermanent);
        }
    }
}