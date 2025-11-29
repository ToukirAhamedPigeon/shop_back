// src/Modules/UserTable/Domain/Repositories/IUserTableCombinationRepository.cs
using shop_back.src.Shared.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IUserTableCombinationRepository
    {
        Task<UserTableCombination?> GetByTableIdAndUserId(string tableId, Guid userId);
        Task AddAsync(UserTableCombination entity);
        Task UpdateAsync(UserTableCombination entity);
    }
}
