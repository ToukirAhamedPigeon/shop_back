using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.DTOs.Options;

namespace shop_back.src.Shared.Application.Services
{
    public interface IOptionsService
    {
        /// <summary>
        /// Get select options for a given type (like collections, actionTypes, creators)
        /// </summary>
        /// <param name="type">The option type</param>
        /// <param name="req">Filter/pagination/search</param>
        Task<IEnumerable<SelectOptionDto>> GetOptionsAsync(string type, SelectRequestDto req);
        
        // CRUD Operations for Options
        Task<object> GetOptionsAsync(OptionFilterRequest request);
        Task<OptionDto?> GetOptionAsync(Guid id);
        Task<OptionDto?> GetOptionForEditAsync(Guid id);
        Task<(bool Success, string Message)> CreateOptionAsync(CreateOptionRequest request, string? createdBy);
        Task<(bool Success, string Message)> UpdateOptionAsync(Guid id, UpdateOptionRequest request, string? updatedBy);
        Task<(bool Success, string Message, string DeleteType)> DeleteOptionAsync(Guid id, bool permanent, string? currentUserId);
        Task<(bool Success, string Message)> RestoreOptionAsync(Guid id, string? currentUserId);
        Task<DeleteOptionInfoDto> CheckDeleteEligibilityAsync(Guid id);
        
        // Get parent options for dropdown (only those with has_child = true)
        Task<IEnumerable<SelectOptionDto>> GetParentOptionsAsync(SelectRequestDto? req = null);
        Task<BulkOperationResponse> BulkDeleteOptionsAsync(List<Guid> ids, bool permanent, string? currentUserId);
        Task<BulkOperationResponse> BulkRestoreOptionsAsync(List<Guid> ids, string? currentUserId);
    }
}