using shop_back.src.Shared.Domain.Entities;

namespace shop_back.src.Shared.Application.Services
{
    public interface IMailService
    {
        Task SendEmailAsync(Mail mail);
        Task<Mail?> GetMailByIdAsync(int id);
        Task<IEnumerable<Mail>> GetAllMailsAsync();
        string BuildEmailTemplate(string subject, string body);
    }
}
