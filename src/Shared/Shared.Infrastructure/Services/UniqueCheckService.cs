using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using shop_back.src.Shared.Application.DTOs.Common;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Infrastructure.Data;
using System.Linq.Expressions;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class UniqueCheckService : IUniqueCheckService
    {
        private readonly AppDbContext _context;

        private static readonly Dictionary<string, Type> AllowedModels = new()
        {
            { "User", typeof(Domain.Entities.User) },
            { "Role", typeof(Domain.Entities.Role) },
            { "Permission", typeof(Domain.Entities.Permission) }
        };

        public UniqueCheckService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ExistsAsync(CheckUniqueRequest request)
        {
            if (!AllowedModels.TryGetValue(request.Model, out var entityType))
                throw new Exception("Invalid model");

            var entityMetadata = _context.Model.FindEntityType(entityType)
                ?? throw new Exception("Entity not found in DbContext");

            var propertyMetadata = entityMetadata.FindProperty(request.FieldName)
                ?? throw new Exception("Invalid field name");

            var propertyType = propertyMetadata.ClrType;
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            object? convertedValue = ConvertToType(request.FieldValue, underlyingType);

            // Get DbSet<TEntity>()
            var setMethod = typeof(DbContext)
                .GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(entityType);

            var dbSet = (IQueryable)setMethod.Invoke(_context, null)!;

            var parameter = Expression.Parameter(entityType, "x");

            // EF.Property<T>(x, "FieldName")
            var propertyMethod = typeof(EF)
                .GetMethod(nameof(EF.Property))!
                .MakeGenericMethod(propertyType);

            var propertyAccess = Expression.Call(
                propertyMethod,
                parameter,
                Expression.Constant(request.FieldName)
            );

            var constant = Expression.Constant(convertedValue, propertyType);

            var equals = Expression.Equal(propertyAccess, constant);

            Expression finalExpression = equals;

            // 🔥 EXCEPT logic (type-safe)
            if (!string.IsNullOrEmpty(request.ExceptFieldName) &&
                !string.IsNullOrEmpty(request.ExceptFieldValue))
            {
                var exceptMetadata = entityMetadata.FindProperty(request.ExceptFieldName)
                    ?? throw new Exception("Invalid except field name");

                var exceptType = exceptMetadata.ClrType;
                var exceptUnderlying = Nullable.GetUnderlyingType(exceptType) ?? exceptType;

                object? exceptConverted = ConvertToType(request.ExceptFieldValue, exceptUnderlying);

                var exceptPropertyMethod = typeof(EF)
                    .GetMethod(nameof(EF.Property))!
                    .MakeGenericMethod(exceptType);

                var exceptPropertyAccess = Expression.Call(
                    exceptPropertyMethod,
                    parameter,
                    Expression.Constant(request.ExceptFieldName)
                );

                var exceptConstant = Expression.Constant(exceptConverted, exceptType);

                var notEquals = Expression.NotEqual(exceptPropertyAccess, exceptConstant);

                finalExpression = Expression.AndAlso(equals, notEquals);
            }

            var lambda = Expression.Lambda(finalExpression, parameter);

            var whereMethod = typeof(Queryable)
                .GetMethods()
                .First(m => m.Name == nameof(Queryable.Where)
                            && m.GetParameters().Length == 2)
                .MakeGenericMethod(entityType);

            var whereCall = Expression.Call(
                whereMethod,
                dbSet.Expression,
                lambda
            );

            var filteredQuery = dbSet.Provider.CreateQuery(whereCall);

            var anyAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods()
                .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.AnyAsync)
                            && m.GetParameters().Length == 2)
                .MakeGenericMethod(entityType);

            var task = (Task<bool>)anyAsyncMethod.Invoke(
                null,
                new object[] { filteredQuery, CancellationToken.None }
            )!;

            return await task;
        }

        // 🔥 Safe type conversion helper
        private static object? ConvertToType(string value, Type targetType)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (targetType == typeof(Guid))
                return Guid.Parse(value);

            if (targetType.IsEnum)
                return Enum.Parse(targetType, value);

            return Convert.ChangeType(value, targetType);
        }
    }
}
