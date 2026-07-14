// D:\shop\shop_back\src\Shared\Shared.Application\DTOs\Settings\SettingsGroupDto.cs
using System.Collections.Generic;

namespace shop_back.src.Shared.Application.DTOs.Settings
{
    public class SettingsGroupDto
    {
        public string Category { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public List<AppSettingDto> Settings { get; set; } = new();
    }
}