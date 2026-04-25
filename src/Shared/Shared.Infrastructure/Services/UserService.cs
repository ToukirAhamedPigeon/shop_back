using BCrypt.Net;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Users;
using shop_back.src.Shared.Application.DTOs.Auth;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Application.DTOs.Common;
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
                    modelId: user.Id.ToString()
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
            // Use the new method that includes deleted users
            var user = await _repo.GetByIdIncludingDeletedAsync(id);
            if (user == null) 
                return (false, "User not found", "none");
            
            // Check if user has role "Developer" - cannot be deleted
            var userRoles = await _rolePermissionRepo.GetRoleNamesByUserIdAsync(id);
            if (userRoles.Contains("Developer", StringComparer.OrdinalIgnoreCase))
            {
                return (false, "Cannot delete user with Developer role. Please remove the Developer role first.", "none");
            }
            
            Guid? deletedBy = null;
            if (!string.IsNullOrEmpty(currentUserId) && Guid.TryParse(currentUserId, out var parsed))
                deletedBy = parsed;
            
            string deleteType = "soft";
            List<string> blockingTables = new();
            
            if (permanent)
            {
                if (user.IsDeleted)
                {
                    deleteType = "permanent";
                }
                else
                {
                    // Check only critical foreign key constraints
                    var (hasRelatedRecords, details) = await _repo.HasRelatedRecordsWithDetailsAsync(id);
                    var hasVerifiedEmail = await _repo.HasVerifiedEmailAsync(id);
                    
                    // Build blocking tables list
                    if (details.HasRefreshTokens) blockingTables.Add("Refresh Tokens");
                    if (details.HasPasswordResets) blockingTables.Add("Password Resets");
                    if (details.HasUserTableCombinations) blockingTables.Add("User Table Combinations");
                    if (hasVerifiedEmail) blockingTables.Add("Email Verification");
                    
                    if (!hasRelatedRecords && !hasVerifiedEmail)
                    {
                        deleteType = "permanent";
                    }
                    else
                    {
                        deleteType = "soft";
                        
                        var reasons = new List<string>();
                        if (hasVerifiedEmail) reasons.Add("verified email");
                        if (details.HasRefreshTokens) reasons.Add($"{details.RefreshTokensCount} refresh token(s)");
                        if (details.HasPasswordResets) reasons.Add($"{details.PasswordResetsCount} password reset(s)");
                        if (details.HasUserTableCombinations) reasons.Add($"{details.UserTableCombinationsCount} table combination(s)");
                        
                        await _userLogHelper.LogAsync(
                            userId: deletedBy ?? id,
                            actionType: "DeleteBlocked",
                            detail: $"Permanent delete blocked for user '{user.Username}'. Related records: {string.Join(", ", reasons)}",
                            changes: JsonConvert.SerializeObject(new { 
                                userId = id,
                                attemptedPermanent = true,
                                reasons = reasons,
                                blockingTables = blockingTables
                            }),
                            modelName: "User",
                            modelId: id.ToString()
                        );
                    }
                }
            }
            
            // Use a single transaction for the entire operation
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
                            before = new { user.Id, user.Username, user.Email, user.Name, user.IsDeleted },
                            deletedBy = deletedBy?.ToString()
                        }),
                        modelName: "User",
                        modelId: id.ToString()
                    );
                }
                else
                {
                    if (!user.IsDeleted)
                    {
                        await _repo.SoftDeleteAsync(id, deletedBy);
                    }
                    
                    await _userLogHelper.LogAsync(
                        userId: deletedBy ?? id,
                        actionType: "Delete",
                        detail: $"User '{user.Username}' was soft deleted",
                        changes: JsonConvert.SerializeObject(new { 
                            before = new { user.Id, user.Username, user.Email, IsDeleted = false },
                            after = new { IsDeleted = true, DeletedAt = DateTime.UtcNow, DeletedBy = deletedBy?.ToString() }
                        }),
                        modelName: "User",
                        modelId: id.ToString()
                    );
                }
                
                await _repo.SaveChangesAsync();
                await transaction.CommitAsync();
                
                string successMessage = deleteType == "permanent" 
                    ? "User permanently deleted successfully" 
                    : "User moved to trash successfully";
                
                return (true, successMessage, deleteType);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                
                string errorMessage = ex.Message;
                List<string> failedConstraints = new();
                
                if (ex.InnerException?.Message?.Contains("violates foreign key constraint") == true)
                {
                    var innerMessage = ex.InnerException.Message;
                    if (innerMessage.Contains("refresh_tokens_user_id_fkey"))
                    {
                        failedConstraints.Add("Refresh Tokens");
                    }
                    if (innerMessage.Contains("password_resets_user_id_fkey"))
                    {
                        failedConstraints.Add("Password Resets");
                    }
                    
                    errorMessage = $"Cannot delete user due to existing related records in: {string.Join(", ", failedConstraints)}";
                }
                
                return (false, errorMessage, "none");
            }
        }

        public async Task<DeleteEligibilityResponse> CheckDeleteEligibilityAsync(Guid id)
        {
            var user = await _repo.GetByIdIncludingDeletedAsync(id);
            if (user == null)
                return new DeleteEligibilityResponse 
                { 
                    Success = false, 
                    Message = "User not found", 
                    CanBePermanent = false 
                };
            
            if (user.IsDeleted)
                return new DeleteEligibilityResponse 
                { 
                    Success = true, 
                    Message = "User is in trash and can be permanently deleted", 
                    CanBePermanent = true,
                    HasRelatedRecords = false,
                    HasVerifiedEmail = false
                };
            
            var userRoles = await _rolePermissionRepo.GetRoleNamesByUserIdAsync(id);
            if (userRoles.Contains("Developer", StringComparer.OrdinalIgnoreCase))
            {
                return new DeleteEligibilityResponse 
                { 
                    Success = true, 
                    Message = "Cannot delete user with Developer role. Please remove the Developer role first.", 
                    CanBePermanent = false,
                    HasRelatedRecords = true
                };
            }
            
            var (hasRelatedRecords, details) = await _repo.HasRelatedRecordsWithDetailsAsync(id);
            var hasVerifiedEmail = await _repo.HasVerifiedEmailAsync(id);
            
            bool canBePermanent = !hasRelatedRecords && !hasVerifiedEmail;
            
            var blockingTables = new List<string>();
            if (details.HasRefreshTokens) blockingTables.Add("Refresh Tokens");
            if (details.HasPasswordResets) blockingTables.Add("Password Resets");
            if (details.HasUserTableCombinations) blockingTables.Add("User Table Combinations");
            if (hasVerifiedEmail) blockingTables.Add("Email Verification");
            
            string message;
            if (canBePermanent)
            {
                message = "User can be permanently deleted (no related records found)";
            }
            else
            {
                var reasons = new List<string>();
                if (hasVerifiedEmail) reasons.Add("has verified email");
                if (details.HasRefreshTokens) reasons.Add($"has {details.RefreshTokensCount} refresh token(s)");
                if (details.HasPasswordResets) reasons.Add($"has {details.PasswordResetsCount} password reset(s)");
                if (details.HasUserTableCombinations) reasons.Add($"has {details.UserTableCombinationsCount} table combination(s)");
                
                message = $"User must be soft deleted because they: {string.Join(", ", reasons)}";
            }
            
            return new DeleteEligibilityResponse
            {
                Success = true,
                Message = message,
                CanBePermanent = canBePermanent,
                HasRelatedRecords = hasRelatedRecords,
                HasVerifiedEmail = hasVerifiedEmail,
                RelatedRecordsDetails = details,
                BlockingTables = blockingTables
            };
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
                    after = new { IsDeleted = false, RestoredBy = restoredBy?.ToString(), RestoredAt = DateTime.UtcNow }
                }),
                modelName: "User",
                modelId: id.ToString()
            );
            
            return (true, "User restored successfully");
        }

        public async Task<BulkOperationResponse> BulkDeleteAsync(List<Guid> ids, bool permanent, string? currentUserId)
        {
            var response = new BulkOperationResponse
            {
                TotalCount = ids.Count,
                SuccessCount = 0,
                FailedCount = 0,
                Success = true
            };
            
            foreach (var id in ids)
            {
                var result = await DeleteUserAsync(id, permanent, currentUserId);
                
                if (result.Success)
                {
                    response.SuccessCount++;
                }
                else
                {
                    response.FailedCount++;
                    response.Errors.Add(new BulkOperationError
                    {
                        Id = id,
                        Error = result.Message
                    });
                    response.Success = false;
                }
            }
            
            response.Message = $"Processed {response.TotalCount} users. Success: {response.SuccessCount}, Failed: {response.FailedCount}";
            
            return response;
        }

        public async Task<BulkOperationResponse> BulkRestoreAsync(List<Guid> ids, string? currentUserId)
        {
            var response = new BulkOperationResponse
            {
                TotalCount = ids.Count,
                SuccessCount = 0,
                FailedCount = 0,
                Success = true
            };
            
            foreach (var id in ids)
            {
                var result = await RestoreUserAsync(id, currentUserId);
                
                if (result.Success)
                {
                    response.SuccessCount++;
                }
                else
                {
                    response.FailedCount++;
                    response.Errors.Add(new BulkOperationError
                    {
                        Id = id,
                        Error = result.Message
                    });
                    response.Success = false;
                }
            }
            
            response.Message = $"Processed {response.TotalCount} users. Success: {response.SuccessCount}, Failed: {response.FailedCount}";
            
            return response;
        }
    }
}