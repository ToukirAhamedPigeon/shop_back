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
            var request = _httpContextAccessor.HttpContext?.Request;

            string? ipAddress = request?.HttpContext.Connection.RemoteIpAddress?.ToString();
            string? userAgent = request?.Headers["User-Agent"].ToString();
            string? os = null;
            string? browser = null;
            string? device = null;

            if (!string.IsNullOrEmpty(userAgent))
                (browser, os, device) = UserAgentParser.Parse(userAgent);

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

            JObject parsed;
            if (changes is string str)
            {
                try
                {
                    parsed = JObject.Parse(str);
                }
                catch
                {
                    return str;
                }
            }
            else
            {
                parsed = JObject.FromObject(changes);
            }

            var before = parsed["before"] as JObject ?? new JObject();
            var after = parsed["after"] as JObject ?? new JObject();
            var filteredAfter = new JObject();

            foreach (var prop in after.Properties()) // ✅ iterate correctly
            {
                var beforeVal = before[prop.Name];
                var afterVal = prop.Value;

                if (!JToken.DeepEquals(beforeVal, afterVal))
                    filteredAfter[prop.Name] = afterVal;
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
            string? browser = null;
            string? os = null;
            string? device = null;

            if (userAgent.Contains("Firefox")) browser = "Firefox";
            else if (userAgent.Contains("Chrome")) browser = "Chrome";
            else if (userAgent.Contains("Safari")) browser = "Safari";

            if (userAgent.Contains("Windows")) os = "Windows";
            else if (userAgent.Contains("Macintosh")) os = "MacOS";
            else if (userAgent.Contains("Linux")) os = "Linux";
            else if (userAgent.Contains("Android")) os = "Android";
            else if (userAgent.Contains("iPhone") || userAgent.Contains("iPad")) os = "iOS";

            if (userAgent.Contains("Mobile") || userAgent.Contains("iPhone") || userAgent.Contains("Android"))
                device = "Mobile";
            else
                device = "Desktop";

            return (browser, os, device);
        }
    }
}