using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using shop_back.src.Shared.Domain.Enums;

namespace shop_back.src.Shared.Infrastructure.Services.Authorization
{
    public class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        private const string PREFIX = "PERMISSION";
        private readonly DefaultAuthorizationPolicyProvider _fallback;

        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        {
            _fallback = new DefaultAuthorizationPolicyProvider(options);
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
            => _fallback.GetDefaultPolicyAsync();

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
            => _fallback.GetFallbackPolicyAsync();

        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (!policyName.StartsWith(PREFIX))
                return _fallback.GetPolicyAsync(policyName);

            // Format: PERMISSION:Or:read,write
            var parts = policyName.Split(':', 3);
            var relation = Enum.Parse<PermissionRelation>(parts[1]);
            var permissions = parts[2].Split(',');

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permissions, relation))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
    }
}
