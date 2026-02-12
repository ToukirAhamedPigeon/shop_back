using BCrypt.Net;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Users;
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

        public UserService(AppDbContext context, IUserRepository repo, IRolePermissionRepository rolePermissionRepo, UserLogHelper userLogHelper, IMailVerificationService mailVerificationService)   
        {
            _context = context;
            _repo = repo;
            _rolePermissionRepo = rolePermissionRepo;
            _userLogHelper = userLogHelper;
            _mailVerificationService = mailVerificationService;
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

            // 🔐 Fetch roles & permissions
            var roles = await _rolePermissionRepo
                .GetRoleNamesByUserIdAsync(user.Id) ?? Array.Empty<string>();

            var permissions = await _rolePermissionRepo
                .GetAllPermissionsByUserIdAsync(user.Id) ?? Array.Empty<string>();

            return new UserDto
            {
                Id = user.Id,

                // Identity
                Name = user.Name,
                Username = user.Username,
                Email = user.Email,
                EmailVerifiedAt = user.EmailVerifiedAt,
                MobileNo = user.MobileNo,

                // Profile
                ProfileImage = user.ProfileImage,
                Bio = user.Bio,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                Address = user.Address,

                // QR
                QRCode = user.QRCode,

                // Preferences
                Timezone = user.Timezone,
                NID = user.NID,
                        Language = user.Language,

                // Status
                IsActive = user.IsActive,

                // Audit
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,

                // 🔥 Authorization
                Roles = roles,
                Permissions = permissions
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
            if (!string.IsNullOrEmpty(request.MobileNo) && await _repo.GetByMobileNoAsync(request.MobileNo) != null)
                return (false, "Mobile number already exists");
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

                // 🔹 8️⃣ Handle profile image (service layer)
                if (request.ProfileImage != null)
                {
                    if (request.ProfileImage.Length > 5 * 1024 * 1024)
                        return (false, "Profile image must be less than 5MB");

                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
                    if (!allowedTypes.Contains(request.ProfileImage.ContentType))
                        return (false, "Only JPG, PNG, WEBP images are allowed");

                    var uploadPath = Path.Combine("wwwroot", "uploads", "users");
                    Directory.CreateDirectory(uploadPath);

                    var fileName = $"{Guid.NewGuid()}.png";
                    var fullPath = Path.Combine(uploadPath, fileName);

                    using var image = await Image.LoadAsync(request.ProfileImage.OpenReadStream());
                    image.Mutate(x => x.Resize(1000, 1000));
                    await image.SaveAsPngAsync(fullPath);

                    user.ProfileImage = $"/uploads/users/{fileName}";
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
            // Combine User ID, UTC timestamp, and a random 4-character string
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"); // precise milliseconds
            var random = Path.GetRandomFileName().Replace(".", "").Substring(0, 4); // short random string
            return $"{userId:N}-{timestamp}-{random}";
        }
    }
}
