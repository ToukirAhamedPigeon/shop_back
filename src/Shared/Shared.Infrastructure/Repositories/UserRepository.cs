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

        public async Task<(
            IEnumerable<UserDto> Users,
            int TotalCount,
            int GrandTotalCount,
            int PageIndex,
            int PageSize
        )> GetFilteredAsync(UserFilterRequest req)
        {
            var baseQuery = _context.Users
                .Where(u => !u.IsDeleted)
                .AsQueryable();

            // 🔍 Search (User fields + Roles + Permissions)
            if (!string.IsNullOrWhiteSpace(req.Q))
            {
                var q = req.Q.Trim();

                baseQuery = baseQuery.Where(u =>
                    u.Name.Contains(q) ||
                    u.Username.Contains(q) ||
                    u.Email.Contains(q) ||
                    (u.MobileNo != null && u.MobileNo.Contains(q)) ||
                    (u.Address != null && u.Address.Contains(q)) ||

                    // 🔥 Role search
                    _context.ModelRoles.Any(mr =>
                        mr.ModelId == u.Id &&
                        mr.ModelName == "User" &&
                        _context.Roles.Any(r =>
                            r.Id == mr.RoleId &&
                            r.Name.Contains(q)
                        )
                    ) ||

                    // 🔥 Permission search (from ModelPermission + RolePermission)
                    (
                        _context.ModelPermissions.Any(mp =>
                            mp.ModelId == u.Id &&
                            mp.ModelName == "User" &&
                            _context.Permissions.Any(p =>
                                p.Id == mp.PermissionId &&
                                p.Name.Contains(q)
                            )
                        ) ||

                        _context.RolePermissions.Any(rp =>
                            _context.ModelRoles.Any(mr =>
                                mr.ModelId == u.Id &&
                                mr.ModelName == "User" &&
                                mr.RoleId == rp.RoleId
                            ) &&
                            _context.Permissions.Any(p =>
                                p.Id == rp.PermissionId &&
                                p.Name.Contains(q)
                            )
                        )
                    )
                );
            }

            // 🔘 Active filter
            if (req.IsActive.HasValue)
                baseQuery = baseQuery.Where(u => u.IsActive == req.IsActive.Value);

            // 📊 Counts (before pagination)
            int totalCount = await baseQuery.CountAsync();
            int grandTotalCount = await _context.Users.CountAsync();

            bool desc = req.SortOrder?.ToLower() == "desc";
            var sortBy = req.SortBy?.ToLower();

            // 🔃 Sorting (User + Roles + Permissions)
            var query = sortBy switch
            {
                // 🔤 User text fields
                "name" => desc ? baseQuery.OrderByDescending(x => x.Name) : baseQuery.OrderBy(x => x.Name),
                "username" => desc ? baseQuery.OrderByDescending(x => x.Username) : baseQuery.OrderBy(x => x.Username),
                "email" => desc ? baseQuery.OrderByDescending(x => x.Email) : baseQuery.OrderBy(x => x.Email),
                "mobileno" => desc ? baseQuery.OrderByDescending(x => x.MobileNo) : baseQuery.OrderBy(x => x.MobileNo),
                "gender" => desc ? baseQuery.OrderByDescending(x => x.Gender) : baseQuery.OrderBy(x => x.Gender),
                "address" => desc ? baseQuery.OrderByDescending(x => x.Address) : baseQuery.OrderBy(x => x.Address),
                "timezone" => desc ? baseQuery.OrderByDescending(x => x.Timezone) : baseQuery.OrderBy(x => x.Timezone),
                "language" => desc ? baseQuery.OrderByDescending(x => x.Language) : baseQuery.OrderBy(x => x.Language),

                // 🔢 Boolean
                "isactive" => desc ? baseQuery.OrderByDescending(x => x.IsActive) : baseQuery.OrderBy(x => x.IsActive),

                // 📅 Dates
                "createdat" => desc ? baseQuery.OrderByDescending(x => x.CreatedAt) : baseQuery.OrderBy(x => x.CreatedAt),
                "updatedat" => desc ? baseQuery.OrderByDescending(x => x.UpdatedAt) : baseQuery.OrderBy(x => x.UpdatedAt),
                "dateofbirth" => desc ? baseQuery.OrderByDescending(x => x.DateOfBirth) : baseQuery.OrderBy(x => x.DateOfBirth),
                "lastloginat" => desc ? baseQuery.OrderByDescending(x => x.LastLoginAt) : baseQuery.OrderBy(x => x.LastLoginAt),

                // 🔥 ROLE SORT (alphabetically first role)
                "role" => desc
                    ? baseQuery.OrderByDescending(u =>
                        _context.ModelRoles
                            .Where(mr => mr.ModelId == u.Id && mr.ModelName == "User")
                            .Join(_context.Roles, mr => mr.RoleId, r => r.Id, (mr, r) => r.Name)
                            .Min()
                    )
                    : baseQuery.OrderBy(u =>
                        _context.ModelRoles
                            .Where(mr => mr.ModelId == u.Id && mr.ModelName == "User")
                            .Join(_context.Roles, mr => mr.RoleId, r => r.Id, (mr, r) => r.Name)
                            .Min()
                    ),

                // 🔥 PERMISSION SORT (alphabetically first permission from BOTH sources)
                "permission" => desc
                    ? baseQuery.OrderByDescending(u =>
                        _context.ModelPermissions
                            .Where(mp => mp.ModelId == u.Id && mp.ModelName == "User")
                            .Join(_context.Permissions, mp => mp.PermissionId, p => p.Id, (mp, p) => p.Name)
                            .Concat(
                                _context.RolePermissions
                                    .Where(rp =>
                                        _context.ModelRoles.Any(mr =>
                                            mr.ModelId == u.Id &&
                                            mr.ModelName == "User" &&
                                            mr.RoleId == rp.RoleId
                                        )
                                    )
                                    .Join(_context.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Name)
                            )
                            .Min()
                    )
                    : baseQuery.OrderBy(u =>
                        _context.ModelPermissions
                            .Where(mp => mp.ModelId == u.Id && mp.ModelName == "User")
                            .Join(_context.Permissions, mp => mp.PermissionId, p => p.Id, (mp, p) => p.Name)
                            .Concat(
                                _context.RolePermissions
                                    .Where(rp =>
                                        _context.ModelRoles.Any(mr =>
                                            mr.ModelId == u.Id &&
                                            mr.ModelName == "User" &&
                                            mr.RoleId == rp.RoleId
                                        )
                                    )
                                    .Join(_context.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Name)
                            )
                            .Min()
                    ),

                // 🧯 Default
                _ => desc ? baseQuery.OrderByDescending(x => x.CreatedAt) : baseQuery.OrderBy(x => x.CreatedAt)
            };

            // 📄 Pagination
            var users = await query
                .Skip((req.Page - 1) * req.Limit)
                .Take(req.Limit)
                .ToListAsync();

            // 🔥 Attach roles & permissions
            var result = new List<UserDto>();
            foreach (var user in users)
            {
                // Roles
                var roles = await _rolePermissionRepo
                    .GetRoleNamesByUserIdAsync(user.Id) ?? Array.Empty<string>();

                // Permissions from BOTH RolePermission + ModelPermission
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

            return (
                result,
                totalCount,
                grandTotalCount,
                req.Page - 1,
                req.Limit
            );
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
