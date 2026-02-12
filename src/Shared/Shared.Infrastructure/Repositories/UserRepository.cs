using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Users;
using shop_back.src.Shared.Application.DTOs.Common;
using System.Reflection;

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

        public async Task<bool> ExistsByUsernameAsync(string username)
        {
            return await _context.Users
                .AnyAsync(u => !u.IsDeleted && u.Username == username);
        }

        public async Task<bool> ExistsByEmailAsync(string email)
        {
            return await _context.Users
                .AnyAsync(u => !u.IsDeleted && u.Email == email);
        }

        public async Task<bool> ExistsByMobileNoAsync(string mobileNo)
        {
            return await _context.Users
                .AnyAsync(u => !u.IsDeleted && u.MobileNo == mobileNo);
        }

        public async Task<bool> ExistsByNIDAsync(string nid)
        {
            return await _context.Users
                .AnyAsync(u => !u.IsDeleted && u.NID == nid);
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
            var baseQuery = _context.Users.AsQueryable();

            // IsDeleted filter (default false)
            if (req.IsDeleted.HasValue)
                baseQuery = baseQuery.Where(u => u.IsDeleted == req.IsDeleted.Value);
            else
                baseQuery = baseQuery.Where(u => !u.IsDeleted);

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

            if (req.Gender != null && req.Gender.Any())
            {
                baseQuery = baseQuery.Where(u =>
                    req.Gender.Contains(u.Gender ?? "")
                );
            }

            if (req.CreatedBy != null && req.CreatedBy.Any())
            {
                baseQuery = baseQuery.Where(u =>
                    u.CreatedBy.HasValue &&
                    req.CreatedBy.Contains(u.CreatedBy.Value)
                );
            }

            if (req.UpdatedBy != null && req.UpdatedBy.Any())
            {
                baseQuery = baseQuery.Where(u =>
                    u.UpdatedBy.HasValue &&
                    req.UpdatedBy.Contains(u.UpdatedBy.Value)
                );
            }
            if (req.Roles != null && req.Roles.Any())
            {
                baseQuery = baseQuery.Where(u =>
                    _context.ModelRoles.Any(mr =>
                        mr.ModelId == u.Id &&
                        mr.ModelName == "User" &&
                        _context.Roles.Any(r =>
                            r.Id == mr.RoleId &&
                            req.Roles.Contains(r.Name)
                        )
                    )
                );
            }
            if (req.Permissions != null && req.Permissions.Any())
            {
                baseQuery = baseQuery.Where(u =>
                    // Direct user permissions
                    _context.ModelPermissions.Any(mp =>
                        mp.ModelId == u.Id &&
                        mp.ModelName == "User" &&
                        _context.Permissions.Any(p =>
                            p.Id == mp.PermissionId &&
                            req.Permissions.Contains(p.Name)
                        )
                    )
                    ||
                    // Role based permissions
                    _context.RolePermissions.Any(rp =>
                        _context.ModelRoles.Any(mr =>
                            mr.ModelId == u.Id &&
                            mr.ModelName == "User" &&
                            mr.RoleId == rp.RoleId
                        ) &&
                        _context.Permissions.Any(p =>
                            p.Id == rp.PermissionId &&
                            req.Permissions.Contains(p.Name)
                        )
                    )
                );
            }
            if (
                req.DateType != null &&
                req.DateType.Any() &&
                (req.From.HasValue || req.To.HasValue)
            )
            {
                var from = req.From?.Date ?? DateTime.MinValue;
                var to = (req.To?.Date ?? req.From?.Date ?? DateTime.MaxValue)
                            .AddDays(1)
                            .AddTicks(-1);

                foreach (var col in req.DateType)
                {
                    baseQuery = col.ToLower() switch
                    {
                        "createdat" =>
                            baseQuery.Where(u => u.CreatedAt >= from && u.CreatedAt <= to),

                        "updatedat" =>
                            baseQuery.Where(u => u.UpdatedAt >= from && u.UpdatedAt <= to),

                        "dateofbirth" =>
                            baseQuery.Where(u =>
                                u.DateOfBirth.HasValue &&
                                u.DateOfBirth.Value >= from &&
                                u.DateOfBirth.Value <= to
                            ),
                        "emailverifiedat" =>
                            baseQuery.Where(u =>
                                u.EmailVerifiedAt.HasValue &&
                                u.EmailVerifiedAt.Value >= from &&
                                u.EmailVerifiedAt.Value <= to
                            ),
                        "lastloginat" =>
                            baseQuery.Where(u =>
                                u.LastLoginAt.HasValue &&
                                u.LastLoginAt.Value >= from &&
                                u.LastLoginAt.Value <= to
                            ),
                        "deletedat" =>
                            baseQuery.Where(u =>
                                u.DeletedAt.HasValue &&
                                u.DeletedAt.Value >= from &&
                                u.DeletedAt.Value <= to
                            ),

                        _ => baseQuery
                    };
                }
            }

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
                "nid" => desc ? baseQuery.OrderByDescending(x => x.NID) : baseQuery.OrderBy(x => x.NID),
                "language" => desc ? baseQuery.OrderByDescending(x => x.NID) : baseQuery.OrderBy(x => x.NID),   

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
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    CreatedByName = user.CreatedBy.HasValue
                        ? _context.Users.FirstOrDefault(u => u.Id == user.CreatedBy.Value)?.Name
                        : null,
                    UpdatedByName = user.UpdatedBy.HasValue
                        ? _context.Users.FirstOrDefault(u => u.Id == user.UpdatedBy.Value)?.Name
                        : null,
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
        public async Task<IEnumerable<SelectOptionDto>> GetDistinctCreatorsAsync(SelectRequestDto req)
        {
            var query = _context.Users
                .Join(_context.Users,
                    joinedUser => joinedUser.CreatedBy,
                    user => user.Id,
                    (joinedUser, user) => new { joinedUser, user })
                .AsQueryable();

            if (req.Where != null && req.Where.TryGetValue("CreatedByName", out var createdByNameNode))
            {
                var createdByName = createdByNameNode?.ToString() ?? "";
                if (!string.IsNullOrEmpty(createdByName))
                    query = query.Where(x => x.user.Name.Contains(createdByName));
            }

            if (!string.IsNullOrWhiteSpace(req.Search))
                query = query.Where(x => x.user.Name.Contains(req.Search));

            var result = await query
                .Select(x => new SelectOptionDto { Value = x.user.Id.ToString(), Label = x.user.Name })
                .Distinct()
                .OrderBy(x => x.Label)
                .Skip(req.Skip)
                .Take(req.Limit)
                .ToListAsync();

            return result;
        }

        public async Task<IEnumerable<SelectOptionDto>> GetDistinctUpdatersAsync(SelectRequestDto req)
        {
            var query = _context.Users
                .Join(_context.Users,
                    joinedUser => joinedUser.UpdatedBy,
                    user => user.Id,
                    (joinedUser, user) => new { joinedUser, user })
                .AsQueryable();

            if (req.Where != null && req.Where.TryGetValue("CreatedByName", out var createdByNameNode))
            {
                var createdByName = createdByNameNode?.ToString() ?? "";
                if (!string.IsNullOrEmpty(createdByName))
                    query = query.Where(x => x.user.Name.Contains(createdByName));
            }

            if (!string.IsNullOrWhiteSpace(req.Search))
                query = query.Where(x => x.user.Name.Contains(req.Search));

            var result = await query
                .Select(x => new SelectOptionDto { Value = x.user.Id.ToString(), Label = x.user.Name })
                .Distinct()
                .OrderBy(x => x.Label)
                .Skip(req.Skip)
                .Take(req.Limit)
                .ToListAsync();

            return result;
        }
        public async Task<IEnumerable<SelectOptionDto>> GetDistinctDateTypesAsync(SelectRequestDto req)
        {
            // Get all properties of User that are DateTime or DateTime?
            var dateProperties = typeof(User)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?))
                .Select(p => new SelectOptionDto
                {
                    Value = p.Name, // send property name as value
                    Label = p.Name  // optionally you can prettify it for frontend
                })
                .ToList();

            // Pagination
            var paged = dateProperties
                .Skip(req.Skip)
                .Take(req.Limit);

            return await Task.FromResult(paged);
        }
    }
}
