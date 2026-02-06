using shop_back.src.Shared.Application.DTOs.Users;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _repo;
        private readonly IRolePermissionRepository _rolePermissionRepo;

        public UserService(IUserRepository repo, IRolePermissionRepository rolePermissionRepo)
        {
            _repo = repo;
            _rolePermissionRepo = rolePermissionRepo;
        }

        public async Task<object> GetUsersAsync(UserFilterRequest request)
        {
            var (users, total) = await _repo.GetFilteredAsync(request);

            return new
            {
                users,
                totalCount = total,
                pageIndex = request.Page - 1,
                pageSize = request.Limit
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
    }
}
