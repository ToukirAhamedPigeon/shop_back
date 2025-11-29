// src/Shared/Shared.Infrastructure/Services/UserTableCombinationService.cs
using shop_back.src.Shared.Application.DTOs;
using shop_back.src.Shared.Domain.Entities;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using Newtonsoft.Json;
using shop_back.src.Shared.Infrastructure.Helpers; 
using StackExchange.Redis;
using System.Text.Json;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class UserTableCombinationService : IUserTableCombinationService
    {
        private readonly IUserTableCombinationRepository _repository;
        private readonly IDatabase _cache;
        private readonly UserLogHelper _userLogHelper; 
        private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(5);

        public UserTableCombinationService(
            IUserTableCombinationRepository repository,
            IConnectionMultiplexer redis,
            UserLogHelper userLogHelper)
        {
            _repository = repository;
            _cache = redis.GetDatabase();
            _userLogHelper = userLogHelper;
        }

        private string CacheKey(string tableId, Guid userId) => $"user_table:{userId}:{tableId}";

        public async Task<UserTableCombinationDTO> GetByTableAndUserAsync(string tableId, Guid userId)
        {
            var key = CacheKey(tableId, userId);

            // 1️⃣ Try cache first
            var cached = await _cache.StringGetAsync(key);
            UserTableCombination entity;

            if (cached.HasValue)
            {
                entity = System.Text.Json.JsonSerializer.Deserialize<UserTableCombination>(cached!)!;
            }
            else
            {
                // 2️⃣ Fallback to DB
                entity = await _repository.GetByTableIdAndUserId(tableId, userId)
                         ?? new UserTableCombination { TableId = tableId, UserId = userId };

                // 3️⃣ Cache it
                var serialized = System.Text.Json.JsonSerializer.Serialize(entity);
                await _cache.StringSetAsync(key, serialized, _cacheTtl);
            }

            // Convert entity -> DTO
            return new UserTableCombinationDTO
            {
                TableId = entity.TableId,
                ShowColumnCombinations = entity.ShowColumnCombinations?.ToList() ?? new List<string>()
            };
        }

        public async Task SaveOrUpdateAsync(Guid userId, UserTableCombinationDTO dto)
        {
            var entity = await _repository.GetByTableIdAndUserId(dto.TableId, userId);
            var cacheKey = CacheKey(dto.TableId, userId);

            string actionType;
            object? beforeSnapshot = null;
            object afterSnapshot;

            if (entity != null)
            {
                beforeSnapshot = new { entity.ShowColumnCombinations };

                entity.ShowColumnCombinations = dto.ShowColumnCombinations.ToArray();
                entity.UpdatedBy = userId;
                entity.UpdatedAt = DateTime.UtcNow;

                await _repository.UpdateAsync(entity);

                actionType = "Update";

                afterSnapshot = new { entity.ShowColumnCombinations };
            }
            else
            {
                entity = new UserTableCombination
                {
                    TableId = dto.TableId,
                    UserId = userId,
                    ShowColumnCombinations = dto.ShowColumnCombinations.ToArray(),
                    UpdatedBy = userId,
                    UpdatedAt = DateTime.UtcNow
                };

                await _repository.AddAsync(entity);

                actionType = "Create";

                afterSnapshot = new { entity.ShowColumnCombinations };
            }

            // Update Redis
            var serialized = System.Text.Json.JsonSerializer.Serialize(entity);
            await _cache.StringSetAsync(cacheKey, serialized, _cacheTtl);

            try
            {
                string changesJson = JsonConvert.SerializeObject(new
                {
                    before = beforeSnapshot,
                    after = afterSnapshot
                });

                await _userLogHelper.LogAsync(
                    userId: userId,
                    actionType: actionType,
                    detail: actionType == "Update"
                        ? $"Updated column combination for table: {dto.TableId}"
                        : $"Created new column combination for table: {dto.TableId}",
                    changes: changesJson,
                    modelName: "UserTableCombination",
                    modelId: entity.Id
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine("UserLog Error (UserTableCombination): " + ex.Message);
            }
        }
    }
}
