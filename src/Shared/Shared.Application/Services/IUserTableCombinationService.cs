// src/Shared/Shared.Application/Services/IUserTableCombinationService.cs
using shop_back.src.Shared.Application.DTOs;
using System;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Application.Services
{
    public interface IUserTableCombinationService
    {
        Task<UserTableCombinationDTO> GetByTableAndUserAsync(string tableId, Guid userId);
        Task SaveOrUpdateAsync(Guid userId, UserTableCombinationDTO dto);
    }
}
