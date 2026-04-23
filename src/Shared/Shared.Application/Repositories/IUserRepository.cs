// src/Shared/Application/Repositories/IUserRepository.cs

using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Users;
using shop_back.src.Shared.Application.DTOs.Common;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IUserRepository
    {
        Task<bool> ExistsByUsernameAsync(string username);
        Task<bool> ExistsByEmailAsync(string email);
        Task<bool> ExistsByMobileNoAsync(string mobileNo);
        Task<bool> ExistsByNIDAsync(string nid);
        Task<User?> GetByIdentifierAsync(string identifier);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByMobileNoAsync(string mobileNo);
        Task<(
            IEnumerable<UserDto> Users,
            int TotalCount,
            int GrandTotalCount,
            int PageIndex,
            int PageSize
        )> GetFilteredAsync(UserFilterRequest req);
        Task<User?> GetByIdAsync(Guid id);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task SaveChangesAsync();
        Task<IEnumerable<SelectOptionDto>> GetDistinctCreatorsAsync(SelectRequestDto req);
        Task<IEnumerable<SelectOptionDto>> GetDistinctUpdatersAsync(SelectRequestDto req);
        Task<IEnumerable<SelectOptionDto>> GetDistinctDateTypesAsync(SelectRequestDto req);
        Task<bool> ExistsByUsernameAsync(string username, Guid ignoreId);
        Task<bool> ExistsByEmailAsync(string email, Guid ignoreId);
        Task<bool> ExistsByMobileNoAsync(string mobileNo, Guid ignoreId);
        Task<bool> ExistsByNIDAsync(string nid, Guid ignoreId);
        
        // Relation check methods
        Task<bool> HasRelatedRecordsAsync(Guid userId);
        Task<(bool HasRecords, RelatedRecordsDetails Details)> HasRelatedRecordsWithDetailsAsync(Guid userId);
        Task<bool> HasVerifiedEmailAsync(Guid userId);
        
        // Delete methods
        Task HardDeleteAsync(Guid userId);
        Task SoftDeleteAsync(Guid userId, Guid? deletedBy);
        Task RestoreUserAsync(Guid userId, Guid? restoredBy);
        
        // Get user including deleted
        Task<User?> GetByIdIncludingDeletedAsync(Guid id);
        
        // NEW: Profile image deletion method
        Task DeleteUserProfileImageAsync(User user);
    }
}