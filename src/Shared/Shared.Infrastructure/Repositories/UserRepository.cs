using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Infrastructure.Helpers;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Users;
using shop_back.src.Shared.Application.DTOs.Common;
using System.Reflection;
using Newtonsoft.Json;

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
                .FirstOrDefaultAsync(u =>u.Id == id);
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
            // Start with base query
            IQueryable<User> baseQuery;
            
            Console.WriteLine($"req: {JsonConvert.SerializeObject(req)}");

            // ============================================
            // 1️⃣ IsDeleted filter - Handle this FIRST
            // ============================================
            if (req.IsDeleted.HasValue && req.IsDeleted.Value)
            {
                // Show ONLY deleted users - ignore global filter
                baseQuery = _context.Users
                    .IgnoreQueryFilters()
                    .Where(u => u.IsDeleted == true);
            }
            else
            {
                // Show ONLY non-deleted users (default) - global filter applies
                baseQuery = _context.Users
                    .Where(u => !u.IsDeleted);
            }

            // ============================================
            // 2️⃣ IsActive filter
            // ============================================
            if (req.IsActive.HasValue)
                baseQuery = baseQuery.Where(u => u.IsActive == req.IsActive.Value);

            // ============================================
            // 3️⃣ Search (Q) - User fields + Roles + Permissions
            // ============================================
            if (!string.IsNullOrWhiteSpace(req.Q))
            {
                var q = req.Q.Trim();

                baseQuery = baseQuery.Where(u =>
                    u.Name.Contains(q) ||
                    u.Username.Contains(q) ||
                    u.Email.Contains(q) ||
                    (u.MobileNo != null && u.MobileNo.Contains(q)) ||
                    (u.Address != null && u.Address.Contains(q)) ||

                    // Role search
                    _context.ModelRoles.Any(mr =>
                        mr.ModelId == u.Id &&
                        mr.ModelName == "User" &&
                        _context.Roles.Any(r =>
                            r.Id == mr.RoleId &&
                            r.Name.Contains(q)
                        )
                    ) ||

                    // Permission search
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

            // ============================================
            // 4️⃣ Gender filter
            // ============================================
            if (req.Gender != null && req.Gender.Any())
            {
                baseQuery = baseQuery.Where(u =>
                    u.Gender != null && req.Gender.Contains(u.Gender)
                );
            }

            // ============================================
            // 5️⃣ Created By filter
            // ============================================
            if (req.CreatedBy != null && req.CreatedBy.Any())
            {
                baseQuery = baseQuery.Where(u =>
                    u.CreatedBy.HasValue &&
                    req.CreatedBy.Contains(u.CreatedBy.Value)
                );
            }

            // ============================================
            // 6️⃣ Updated By filter
            // ============================================
            if (req.UpdatedBy != null && req.UpdatedBy.Any())
            {
                baseQuery = baseQuery.Where(u =>
                    u.UpdatedBy.HasValue &&
                    req.UpdatedBy.Contains(u.UpdatedBy.Value)
                );
            }

            // ============================================
            // 7️⃣ Roles filter
            // ============================================
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

            // ============================================
            // 8️⃣ Permissions filter
            // ============================================
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
                    ) ||
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

            // ============================================
            // 9️⃣ Date range filters
            // ============================================
            if (req.DateType != null && req.DateType.Any() && (req.From.HasValue || req.To.HasValue))
            {
                var from = req.From?.Date ?? DateTime.MinValue;
                var to = req.To?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue;

                // Build OR condition for multiple date types
                var dateFilterPredicate = PredicateBuilder.False<User>();
                
                foreach (var col in req.DateType)
                {
                    switch (col.ToLower())
                    {
                        case "createdat":
                            dateFilterPredicate = dateFilterPredicate.Or(u => u.CreatedAt >= from && u.CreatedAt <= to);
                            break;
                        case "updatedat":
                            dateFilterPredicate = dateFilterPredicate.Or(u => u.UpdatedAt >= from && u.UpdatedAt <= to);
                            break;
                        case "dateofbirth":
                            dateFilterPredicate = dateFilterPredicate.Or(u => 
                                u.DateOfBirth.HasValue && u.DateOfBirth.Value >= from && u.DateOfBirth.Value <= to);
                            break;
                        case "emailverifiedat":
                            dateFilterPredicate = dateFilterPredicate.Or(u => 
                                u.EmailVerifiedAt.HasValue && u.EmailVerifiedAt.Value >= from && u.EmailVerifiedAt.Value <= to);
                            break;
                        case "lastloginat":
                            dateFilterPredicate = dateFilterPredicate.Or(u => 
                                u.LastLoginAt.HasValue && u.LastLoginAt.Value >= from && u.LastLoginAt.Value <= to);
                            break;
                        case "deletedat":
                            dateFilterPredicate = dateFilterPredicate.Or(u => 
                                u.DeletedAt.HasValue && u.DeletedAt.Value >= from && u.DeletedAt.Value <= to);
                            break;
                    }
                }

                baseQuery = baseQuery.Where(dateFilterPredicate);
            }

            // 📊 Counts (before pagination)
            int totalCount = await baseQuery.CountAsync();
            
            // Grand total should count ALL users (including deleted)
            int grandTotalCount = await _context.Users.IgnoreQueryFilters().CountAsync();

            bool desc = req.SortOrder?.ToLower() == "desc";
            var sortBy = req.SortBy?.ToLower();

            // 🔃 Sorting
            IOrderedQueryable<User> query;
            
            query = sortBy switch
            {
                "name" => desc ? baseQuery.OrderByDescending(x => x.Name) : baseQuery.OrderBy(x => x.Name),
                "username" => desc ? baseQuery.OrderByDescending(x => x.Username) : baseQuery.OrderBy(x => x.Username),
                "email" => desc ? baseQuery.OrderByDescending(x => x.Email) : baseQuery.OrderBy(x => x.Email),
                "mobileno" => desc ? baseQuery.OrderByDescending(x => x.MobileNo) : baseQuery.OrderBy(x => x.MobileNo),
                "gender" => desc ? baseQuery.OrderByDescending(x => x.Gender) : baseQuery.OrderBy(x => x.Gender),
                "address" => desc ? baseQuery.OrderByDescending(x => x.Address) : baseQuery.OrderBy(x => x.Address),
                "timezone" => desc ? baseQuery.OrderByDescending(x => x.Timezone) : baseQuery.OrderBy(x => x.Timezone),
                "nid" => desc ? baseQuery.OrderByDescending(x => x.NID) : baseQuery.OrderBy(x => x.NID),
                "language" => desc ? baseQuery.OrderByDescending(x => x.Language) : baseQuery.OrderBy(x => x.Language),
                "isactive" => desc ? baseQuery.OrderByDescending(x => x.IsActive) : baseQuery.OrderBy(x => x.IsActive),
                "createdat" => desc ? baseQuery.OrderByDescending(x => x.CreatedAt) : baseQuery.OrderBy(x => x.CreatedAt),
                "updatedat" => desc ? baseQuery.OrderByDescending(x => x.UpdatedAt) : baseQuery.OrderBy(x => x.UpdatedAt),
                "dateofbirth" => desc ? baseQuery.OrderByDescending(x => x.DateOfBirth) : baseQuery.OrderBy(x => x.DateOfBirth),
                "lastloginat" => desc ? baseQuery.OrderByDescending(x => x.LastLoginAt) : baseQuery.OrderBy(x => x.LastLoginAt),
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
                        ? await _context.Users
                            .IgnoreQueryFilters()
                            .Where(u => u.Id == user.CreatedBy.Value)
                            .Select(u => u.Name)
                            .FirstOrDefaultAsync()
                        : null,
                    UpdatedByName = user.UpdatedBy.HasValue
                        ? await _context.Users
                            .IgnoreQueryFilters()
                            .Where(u => u.Id == user.UpdatedBy.Value)
                            .Select(u => u.Name)
                            .FirstOrDefaultAsync()
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
            // Console.WriteLine($"========== UserRepository.GetDistinctCreatorsAsync ==========");
            // Console.WriteLine($"req: {JsonConvert.SerializeObject(req)}");
            
            // Get distinct creator IDs from ALL users (including deleted)
            var creatorIds = await _context.Users
                .IgnoreQueryFilters()  
                .Where(u => u.CreatedBy != null)
                .Select(u => u.CreatedBy.GetValueOrDefault())
                .Distinct()
                .ToListAsync();

            // Console.WriteLine($"Found {creatorIds.Count} distinct creator IDs (including deleted)");

            if (!creatorIds.Any())
            {
                Console.WriteLine("No creators found");
                return new List<SelectOptionDto>();
            }

            // Now get the actual user details for those creator IDs
            // Include deleted creators so we can show them
            var query = _context.Users
                .IgnoreQueryFilters()  // 🔥 Include deleted creators
                .Where(u => creatorIds.Contains(u.Id))
                .Select(u => new SelectOptionDto 
                { 
                    Value = u.Id.ToString(), 
                    Label = u.Name + (u.IsDeleted ? " (Deleted)" : "")  // Mark deleted users
                })
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(req.Search))
            {
                query = query.Where(x => x.Label.Contains(req.Search));
            }

            // Apply sorting
            if (req.SortOrder?.ToLower() == "desc")
            {
                query = query.OrderByDescending(x => x.Label);
            }
            else
            {
                query = query.OrderBy(x => x.Label);
            }

            // Apply pagination
            var result = await query
                .Skip(req.Skip)
                .Take(req.Limit)
                .ToListAsync();

            Console.WriteLine($"Returning {result.Count} items");
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

            if (req.Where != null && req.Where.TryGetValue("UpdatedByName", out var updatedByNameNode))
            {
                var updatedByName = updatedByNameNode?.ToString() ?? "";
                if (!string.IsNullOrEmpty(updatedByName))
                    query = query.Where(x => x.user.Name.Contains(updatedByName));
            }

            if (!string.IsNullOrWhiteSpace(req.Search))
                query = query.Where(x => x.user.Name.Contains(req.Search));

            var result = await query
                .Select(x => new SelectOptionDto { Value = x.user.Id.ToString(), Label = x.user.Name })
                .IgnoreQueryFilters()  // 🔥 Include deleted updaters
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
        public async Task<bool> ExistsByUsernameAsync(string username, Guid ignoreId)
        {
            return await _context.Users
                .AnyAsync(u => !u.IsDeleted && u.Username == username && u.Id != ignoreId);
        }

        public async Task<bool> ExistsByEmailAsync(string email, Guid ignoreId)
        {
            return await _context.Users
                .AnyAsync(u => !u.IsDeleted && u.Email == email && u.Id != ignoreId);
        }

        public async Task<bool> ExistsByMobileNoAsync(string mobileNo, Guid ignoreId)
        {
            return await _context.Users
                .AnyAsync(u => !u.IsDeleted && u.MobileNo == mobileNo && u.Id != ignoreId);
        }

        public async Task<bool> ExistsByNIDAsync(string nid, Guid ignoreId)
        {
            return await _context.Users
                .AnyAsync(u => !u.IsDeleted && u.NID == nid && u.Id != ignoreId);
        }

        public async Task<bool> HasRelatedRecordsAsync(Guid userId)
        {
            // Check all tables except mail_verifications, model_roles, model_permissions
            var hasUserLogs = await _context.UserLogs.AnyAsync(ul => ul.CreatedBy == userId);
            var hasRefreshTokens = await _context.RefreshTokens.AnyAsync(rt => rt.UserId == userId);
            var hasPasswordResets = await _context.PasswordResets.AnyAsync(pr => pr.UserId == userId);
            var hasUserTableCombination = await _context.UserTableCombinations.AnyAsync(utc => utc.UserId == userId || utc.UpdatedBy == userId);
            Console.WriteLine($"hasUserLogs: {hasUserLogs}");
            Console.WriteLine($"hasRefreshTokens: {hasRefreshTokens}");
            Console.WriteLine($"hasPasswordResets: {hasPasswordResets}");
            Console.WriteLine($"hasUserTableCombination: {hasUserTableCombination}");
            
            // Add any other tables that should prevent permanent deletion
            // For example: orders, invoices, etc.
            
            return hasUserLogs || hasRefreshTokens || hasPasswordResets || hasUserTableCombination;
        }

        public async Task<bool> HasVerifiedEmailAsync(Guid userId)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.EmailVerifiedAt != null)
                .FirstOrDefaultAsync();
            
            return user;
        }

        private void DeleteUserProfileImage(User user)
        {
            if (string.IsNullOrEmpty(user?.ProfileImage))
                return;

            try
            {
                // Get the filename from the path (handles both /uploads/users/file.jpg and uploads/users/file.jpg)
                var relativePath = user.ProfileImage.TrimStart('/');
                
                // Try different possible paths
                var possiblePaths = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath),
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "users", Path.GetFileName(user.ProfileImage)),
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.Replace('/', Path.DirectorySeparatorChar))
                };

                foreach (var path in possiblePaths)
                {
                    var fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        Console.WriteLine($"Deleted profile image: {fullPath}");
                        return; // Exit after successful deletion
                    }
                }

                Console.WriteLine($"Profile image not found for deletion: {user.ProfileImage}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting profile image for user {user.Id}: {ex.Message}");
                // Don't throw - we still want to delete the user
            }
        }

        public async Task HardDeleteAsync(Guid userId)
        {
            // First, get the user to access their profile image path
            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId);
            
            if (user == null) return;

            // Delete profile image from wwwroot if it exists
            DeleteUserProfileImage(user);

            // Delete related records in other tables
            // Delete model_roles entries
            var modelRoles = await _context.ModelRoles
                .Where(mr => mr.ModelId == userId && mr.ModelName == "User")
                .ToListAsync();
            _context.ModelRoles.RemoveRange(modelRoles);
            
            // Delete model_permissions entries
            var modelPermissions = await _context.ModelPermissions
                .Where(mp => mp.ModelId == userId && mp.ModelName == "User")
                .ToListAsync();
            _context.ModelPermissions.RemoveRange(modelPermissions);
            
            // Delete mail_verifications entries
            var mailVerifications = await _context.MailVerifications
                .Where(mv => mv.UserId == userId)
                .ToListAsync();
            _context.MailVerifications.RemoveRange(mailVerifications);
            
            // Finally delete the user
            _context.Users.Remove(user);
        }

        public async Task SoftDeleteAsync(Guid userId, Guid? deletedBy)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsDeleted = true;
                user.DeletedAt = DateTime.UtcNow;
                user.UpdatedBy = deletedBy;
                user.UpdatedAt = DateTime.UtcNow;
            }
        }

        public async Task RestoreUserAsync(Guid userId, Guid? restoredBy)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsDeleted = false;
                user.DeletedAt = null;
                user.UpdatedBy = restoredBy;
                user.UpdatedAt = DateTime.UtcNow;
            }
        }

    }
}
