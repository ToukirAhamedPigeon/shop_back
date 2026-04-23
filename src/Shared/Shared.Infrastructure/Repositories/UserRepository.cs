using Microsoft.EntityFrameworkCore;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Infrastructure.Data;
using shop_back.src.Shared.Infrastructure.Helpers;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Users;
using shop_back.src.Shared.Application.DTOs.Common;
using System.Reflection;
using Newtonsoft.Json;
using shop_back.src.Shared.Application.Exceptions;

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
                .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
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
            IQueryable<User> baseQuery;
            
            Console.WriteLine($"req: {JsonConvert.SerializeObject(req)}");

            if (req.IsDeleted.HasValue && req.IsDeleted.Value)
            {
                baseQuery = _context.Users
                    .IgnoreQueryFilters()
                    .Where(u => u.IsDeleted == true);
            }
            else
            {
                baseQuery = _context.Users
                    .Where(u => !u.IsDeleted);
            }

            if (req.IsActive.HasValue)
                baseQuery = baseQuery.Where(u => u.IsActive == req.IsActive.Value);

            if (!string.IsNullOrWhiteSpace(req.Q))
            {
                var q = req.Q.Trim();

                baseQuery = baseQuery.Where(u =>
                    u.Name.Contains(q) ||
                    u.Username.Contains(q) ||
                    u.Email.Contains(q) ||
                    (u.MobileNo != null && u.MobileNo.Contains(q)) ||
                    (u.Address != null && u.Address.Contains(q)) ||

                    _context.ModelRoles.Any(mr =>
                        mr.ModelId == u.Id &&
                        mr.ModelName == "User" &&
                        _context.Roles.Any(r =>
                            r.Id == mr.RoleId &&
                            r.Name.Contains(q)
                        )
                    ) ||

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

            if (req.Gender != null && req.Gender.Any())
            {
                baseQuery = baseQuery.Where(u =>
                    u.Gender != null && req.Gender.Contains(u.Gender)
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
                    _context.ModelPermissions.Any(mp =>
                        mp.ModelId == u.Id &&
                        mp.ModelName == "User" &&
                        _context.Permissions.Any(p =>
                            p.Id == mp.PermissionId &&
                            req.Permissions.Contains(p.Name)
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
                            req.Permissions.Contains(p.Name)
                        )
                    )
                );
            }

            if (req.DateType != null && req.DateType.Any() && (req.From.HasValue || req.To.HasValue))
            {
                var from = req.From?.Date ?? DateTime.MinValue;
                var to = req.To?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue;

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

            int totalCount = await baseQuery.CountAsync();
            int grandTotalCount = await _context.Users.IgnoreQueryFilters().CountAsync();

            bool desc = req.SortOrder?.ToLower() == "desc";
            var sortBy = req.SortBy?.ToLower();

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

            var users = await query
                .Skip((req.Page - 1) * req.Limit)
                .Take(req.Limit)
                .ToListAsync();

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
            var creatorIds = await _context.Users
                .IgnoreQueryFilters()  
                .Where(u => u.CreatedBy != null)
                .Select(u => u.CreatedBy.GetValueOrDefault())
                .Distinct()
                .ToListAsync();

            if (!creatorIds.Any())
            {
                return new List<SelectOptionDto>();
            }

            var query = _context.Users
                .IgnoreQueryFilters()
                .Where(u => creatorIds.Contains(u.Id))
                .Select(u => new SelectOptionDto 
                { 
                    Value = u.Id.ToString(), 
                    Label = u.Name + (u.IsDeleted ? " (Deleted)" : "")
                })
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(req.Search))
            {
                query = query.Where(x => x.Label.Contains(req.Search));
            }

            if (req.SortOrder?.ToLower() == "desc")
            {
                query = query.OrderByDescending(x => x.Label);
            }
            else
            {
                query = query.OrderBy(x => x.Label);
            }

            var result = await query
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
                .IgnoreQueryFilters()
                .Distinct()
                .OrderBy(x => x.Label)
                .Skip(req.Skip)
                .Take(req.Limit)
                .ToListAsync();

            return result;
        }
        
        public async Task<IEnumerable<SelectOptionDto>> GetDistinctDateTypesAsync(SelectRequestDto req)
        {
            var dateProperties = typeof(User)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?))
                .Select(p => new SelectOptionDto
                {
                    Value = p.Name,
                    Label = p.Name
                })
                .ToList();

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
            // Only check critical tables that should block permanent delete
            // User Logs, Mails, Model Roles, Mail Verifications are SKIPPED - they will be deleted
            var hasRefreshTokens = await _context.RefreshTokens.AnyAsync(rt => rt.UserId == userId);
            var hasPasswordResets = await _context.PasswordResets.AnyAsync(pr => pr.UserId == userId);
            var hasUserTableCombination = await _context.UserTableCombinations.AnyAsync(utc => utc.UserId == userId || utc.UpdatedBy == userId);
            
            return hasRefreshTokens || hasPasswordResets || hasUserTableCombination;
        }

        public async Task<(bool HasRecords, RelatedRecordsDetails Details)> HasRelatedRecordsWithDetailsAsync(Guid userId)
        {
            var details = new RelatedRecordsDetails();
            
            // Skip UserLogs - will be deleted
            // Skip Mails - will be deleted
            // Skip ModelRoles - will be deleted
            // Skip MailVerifications - will be deleted
            
            // Check RefreshTokens
            details.RefreshTokensCount = await _context.RefreshTokens.CountAsync(rt => rt.UserId == userId);
            details.HasRefreshTokens = details.RefreshTokensCount > 0;
            
            // Check PasswordResets
            details.PasswordResetsCount = await _context.PasswordResets.CountAsync(pr => pr.UserId == userId);
            details.HasPasswordResets = details.PasswordResetsCount > 0;
            
            // Check UserTableCombinations
            details.UserTableCombinationsCount = await _context.UserTableCombinations
                .CountAsync(utc => utc.UserId == userId || utc.UpdatedBy == userId);
            details.HasUserTableCombinations = details.UserTableCombinationsCount > 0;
            
            var hasRecords = details.HasRefreshTokens || details.HasPasswordResets || details.HasUserTableCombinations;
            
            return (hasRecords, details);
        }

        public async Task<bool> HasVerifiedEmailAsync(Guid userId)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => u.EmailVerifiedAt != null)
                .FirstOrDefaultAsync();
            
            return user;
        }

        public async Task DeleteUserProfileImageAsync(User user)
        {
            if (string.IsNullOrEmpty(user?.ProfileImage))
                return;

            try
            {
                // Check if it's a remote URL
                if (user.ProfileImage.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    user.ProfileImage.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Deleting remote profile image: {user.ProfileImage}");
                    await FileHelper.DeleteFileAsync(user.ProfileImage);
                }
                else
                {
                    // Local file path
                    Console.WriteLine($"Deleting local profile image: {user.ProfileImage}");
                    
                    var relativePath = user.ProfileImage.TrimStart('/');
                    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);
                    
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        Console.WriteLine($"Deleted local profile image: {fullPath}");
                    }
                    else
                    {
                        Console.WriteLine($"Local profile image not found: {fullPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting profile image for user {user.Id}: {ex.Message}");
            }
        }

        public async Task HardDeleteAsync(Guid userId)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId);
            
            if (user == null) return;

            // Delete profile image as part of hard delete (after confirmation)
            await DeleteUserProfileImageAsync(user);
            
            // DO NOT create a new transaction here - the calling method already has one
            // Just perform the operations directly
            
            // Delete all related records
            var mails = await _context.Mails.Where(m => m.CreatedBy == userId).ToListAsync();
            _context.Mails.RemoveRange(mails);
            
            var userLogs = await _context.UserLogs
                .Where(ul => ul.CreatedBy == userId || (ul.ModelName == "User" && ul.ModelId == userId.ToString()))
                .ToListAsync();
            _context.UserLogs.RemoveRange(userLogs);
            
            var refreshTokens = await _context.RefreshTokens.Where(rt => rt.UserId == userId).ToListAsync();
            _context.RefreshTokens.RemoveRange(refreshTokens);
            
            var passwordResets = await _context.PasswordResets.Where(pr => pr.UserId == userId).ToListAsync();
            _context.PasswordResets.RemoveRange(passwordResets);
            
            var userTableCombinations = await _context.UserTableCombinations
                .Where(utc => utc.UserId == userId || utc.UpdatedBy == userId)
                .ToListAsync();
            _context.UserTableCombinations.RemoveRange(userTableCombinations);
            
            var modelRoles = await _context.ModelRoles
                .Where(mr => mr.ModelId == userId && mr.ModelName == "User")
                .ToListAsync();
            _context.ModelRoles.RemoveRange(modelRoles);
            
            var modelPermissions = await _context.ModelPermissions
                .Where(mp => mp.ModelId == userId && mp.ModelName == "User")
                .ToListAsync();
            _context.ModelPermissions.RemoveRange(modelPermissions);
            
            var mailVerifications = await _context.MailVerifications
                .Where(mv => mv.UserId == userId)
                .ToListAsync();
            _context.MailVerifications.RemoveRange(mailVerifications);
            
            // Update foreign key references
            var usersCreatedByThis = await _context.Users.Where(u => u.CreatedBy == userId).ToListAsync();
            foreach (var u in usersCreatedByThis)
            {
                u.CreatedBy = null;
            }
            
            var usersUpdatedByThis = await _context.Users.Where(u => u.UpdatedBy == userId).ToListAsync();
            foreach (var u in usersUpdatedByThis)
            {
                u.UpdatedBy = null;
            }
            
            // Finally delete the user
            _context.Users.Remove(user);
            
            // NO SaveChangesAsync here - let the calling method handle it
            Console.WriteLine($"✅ User {userId} marked for permanent deletion");
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

        public async Task<User?> GetByIdIncludingDeletedAsync(Guid id)
        {
            return await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == id);
        }
    }
}