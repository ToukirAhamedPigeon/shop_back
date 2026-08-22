// src/Shared/Shared.Application/Services/IDocumentationService.cs
using shop_back.src.Shared.Application.DTOs.Documentation;

namespace shop_back.src.Shared.Application.Services
{
    public interface IDocumentationService
    {
        Task<List<DocTreeNodeDto>> GetDeveloperTreeAsync();
        Task<DevDocPageDto?> GetDeveloperPageAsync(string? slug);
        Task<UserGuideDto> GetUserGuideAsync(string? roleName);
        Task<PagedChangelogDto> GetChangelogAsync(int page, int pageSize, string? repo);
    }
}
