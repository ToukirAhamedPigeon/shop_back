using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using shop_back.src.Shared.Application.Services;
using shop_back.src.Shared.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Infrastructure.Helpers
{
    public class UserLogHelper
    {
        private readonly IUserLogService _userLogService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserLogHelper(IUserLogService userLogService, IHttpContextAccessor httpContextAccessor)
        {
            _userLogService = userLogService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<UserLog> LogAsync(
            Guid userId,
            string actionType,
            string? detail = null,
            object? changes = null,
            string? modelName = null,
            Guid? modelId = null
        )
        {
            var http = _httpContextAccessor.HttpContext;
            var request = http?.Request;

            string? ipAddress = http?.Connection?.RemoteIpAddress?.ToString();
            string? userAgent = request?.Headers["User-Agent"].ToString();

            string? os = "Unknown";
            string? browser = "Unknown";
            string? device = "Unknown";

            if (!string.IsNullOrEmpty(userAgent))
            {
                try
                {
                    (browser, os, device) = UserAgentParser.Parse(userAgent);
                }
                catch { }
            }

            var normalizedChanges = NormalizeChanges(changes);

            return await _userLogService.CreateLogAsync(
                createdBy: userId,
                action: actionType,
                detail: detail,
                changes: normalizedChanges,
                modelName: modelName,
                modelId: modelId,
                ip: ipAddress,
                browser: browser,
                device: device,
                os: os,
                userAgent: userAgent
            );
        }

        private string? NormalizeChanges(object? changes)
        {
            if (changes == null) return null;

            JObject before = new JObject();
            JObject after = new JObject();

            if (changes is string str)
            {
                try
                {
                    var parsed = JObject.Parse(str);
                    before = parsed["before"] as JObject ?? new JObject();
                    after = parsed["after"] as JObject ?? new JObject();
                }
                catch { return str; }
            }
            else
            {
                // Automatically map properties to "after" if no before/after provided
                var obj = JObject.FromObject(changes);

                if (obj["before"] != null || obj["after"] != null)
                {
                    before = obj["before"] as JObject ?? new JObject();
                    after = obj["after"] as JObject ?? new JObject();
                }
                else
                {
                    // Convert flat object into after-values
                    after = obj;
                }
            }

            // Filter changed values
            var filteredAfter = new JObject();
            foreach (var prop in after.Properties())
            {
                var beforeVal = before[prop.Name];
                if (!JToken.DeepEquals(beforeVal, prop.Value))
                    filteredAfter[prop.Name] = prop.Value;
            }

            return JsonConvert.SerializeObject(new
            {
                before,
                after = filteredAfter
            });
        }
    }

    public static class UserAgentParser
    {
        public static (string? browser, string? os, string? device) Parse(string userAgent)
        {
            string browser = "Unknown";
            string os = "Unknown";
            string device = "Desktop";

            if (userAgent.Contains("Firefox")) browser = "Firefox";
            else if (userAgent.Contains("Chrome") && !userAgent.Contains("Edge")) browser = "Chrome";
            else if (userAgent.Contains("Safari") && !userAgent.Contains("Chrome")) browser = "Safari";
            else if (userAgent.Contains("Edge")) browser = "Edge";

            if (userAgent.Contains("Windows")) os = "Windows";
            else if (userAgent.Contains("Macintosh")) os = "MacOS";
            else if (userAgent.Contains("Linux")) os = "Linux";
            else if (userAgent.Contains("Android")) os = "Android";
            else if (userAgent.Contains("iPhone") || userAgent.Contains("iPad")) os = "iOS";

            if (userAgent.Contains("Mobile") || userAgent.Contains("Android") || userAgent.Contains("iPhone"))
                device = "Mobile";

            return (browser, os, device);
        }
    }
}
