using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Users;

namespace shop_back.src.Shared.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;
        
        private readonly IRolePermissionRepository _rolePermissionRepo;

        public UserRepository(AppDbContext context, IRolePermissionRepository rolePermissionRepo)
        {
            _context = context;
            _rolePermissionRepo = rolePermissionRepo;
        }

        public async Task<User?> GetByIdentifierAsync(string identifier)
        {
            // ✅ Correction:
            // Merged the `Where(u => !u.IsDeleted)` and `FirstOrDefaultAsync(...)`
            // into a single predicate so EF Core can fully translate to SQL
            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    !u.IsDeleted && 
                    (u.Username == identifier ||
                     u.Email == identifier ||
                     u.MobileNo == identifier));
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    !u.IsDeleted && 
                    u.Email == email);
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    !u.IsDeleted && 
                    u.Id == id);
        }

        public async Task<User?> GetByMobileNoAsync(string mobileNo)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    !u.IsDeleted && 
                    u.MobileNo == mobileNo);
        }

        public async Task<(IEnumerable<UserDto>, int)> GetFilteredAsync(UserFilterRequest req)
        {
            var query = _context.Users
                .Where(u => !u.IsDeleted)
                .AsQueryable();

            // 🔍 Search
            if (!string.IsNullOrWhiteSpace(req.Q))
            {
                query = query.Where(u =>
                    u.Name.Contains(req.Q) ||
                    u.Username.Contains(req.Q) ||
                    u.Email.Contains(req.Q));
            }

            // 🔘 Active filter
            if (req.IsActive.HasValue)
                query = query.Where(u => u.IsActive == req.IsActive.Value);

            // 📊 Total count (before pagination)
            int total = await query.CountAsync();

            // 🔃 Sorting
            bool desc = req.SortOrder?.ToLower() == "desc";

            query = req.SortBy?.ToLower() switch
            {
                "name" => desc ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
                "email" => desc ? query.OrderByDescending(x => x.Email) : query.OrderBy(x => x.Email),
                _ => desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt)
            };

            // 📄 Fetch users (paged)
            var users = await query
                .Skip((req.Page - 1) * req.Limit)
                .Take(req.Limit)
                .ToListAsync();

            // 🔥 Attach roles & permissions (SAFE + STANDARD)
            var result = new List<UserDto>();

            foreach (var user in users)
            {
                var roles = await _rolePermissionRepo
                    .GetRoleNamesByUserIdAsync(user.Id) ?? Array.Empty<string>();

                var permissions = await _rolePermissionRepo
                    .GetAllPermissionsByUserIdAsync(user.Id) ?? Array.Empty<string>();

                result.Add(new UserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Username = user.Username,
                    Email = user.Email,
                    MobileNo = user.MobileNo,
                    ProfileImage = user.ProfileImage,
                    Bio = user.Bio,
                    DateOfBirth = user.DateOfBirth,
                    Gender = user.Gender,
                    Address = user.Address,
                    QRCode = user.QRCode,
                    Timezone = user.Timezone,
                    Language = user.Language,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    Roles = roles,
                    Permissions = permissions
                });
            }

            return (result, total);
        }

        public async Task AddAsync(User user)
        {
            await _context.Users.AddAsync(user);
        }

        public Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
