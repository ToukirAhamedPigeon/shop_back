using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.DTOs.Options;
using shop_back.src.Shared.Application.DTOs.Common;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IOptionRepository
    {
        Task<(IEnumerable<OptionDto> Options, int TotalCount, int GrandTotalCount, int PageIndex, int PageSize)> 
            GetFilteredOptionsAsync(OptionFilterRequest req);
        
        Task<Option?> GetOptionByIdAsync(Guid id);
        Task<Option?> GetOptionByIdIncludingDeletedAsync(Guid id);
        Task<bool> OptionExistsAsync(string name, Guid? parentId, Guid? ignoreId = null);
        Task<Option> CreateOptionAsync(Option option);
        void UpdateOption(Option option);
        Task DeleteOptionAsync(Guid id, bool permanent, Guid? deletedBy);
        Task<bool> OptionHasChildrenAsync(Guid optionId);
        Task<int> GetChildrenCountAsync(Guid optionId);
        Task<IEnumerable<Option>> GetParentOptionsAsync(bool onlyWithChildren = true);
        Task SaveChangesAsync();
        Task<BulkOperationResponse> BulkDeleteOptionsAsync(List<Guid> ids, bool permanent, Guid? deletedBy);
        Task<BulkOperationResponse> BulkRestoreOptionsAsync(List<Guid> ids, Guid? restoredBy);
    }
}