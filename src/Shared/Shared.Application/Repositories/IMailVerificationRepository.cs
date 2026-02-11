using shop_back.src.Shared.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Application.Repositories
{
    public interface IMailVerificationRepository
    {
        Task<MailVerification?> GetByTokenAsync(string token);
        Task AddAsync(MailVerification entity);
        Task SaveChangesAsync();
    }
}
