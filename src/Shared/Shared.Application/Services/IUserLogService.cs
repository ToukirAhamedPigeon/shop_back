using shop_back.src.Shared.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Application.Services
{
    public interface IUserLogService
    {
        Task<UserLog> CreateLogAsync(
            Guid createdBy,
            string action,
            string? detail = null,
            string? changes = null,
            string? modelName = null,
            Guid? modelId = null,
            string? ip = null,
            string? browser = null,
            string? device = null,
            string? os = null,
            string? userAgent = null
        );

        Task<IEnumerable<UserLog>> GetLogsAsync();
        Task<IEnumerable<UserLog>> GetLogsByUserAsync(Guid userId);
        Task<UserLog?> GetLogAsync(Guid id);
    }
}
