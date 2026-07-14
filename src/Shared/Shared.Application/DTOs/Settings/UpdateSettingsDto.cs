// D:\shop\shop_back\src\Shared\Shared.Application\DTOs\Settings\UpdateSettingsDto.cs
using System.Collections.Generic;

namespace shop_back.src.Shared.Application.DTOs.Settings
{
    public class UpdateSettingsDto
    {
        public string Category { get; set; } = string.Empty;
        public Dictionary<string, object> Settings { get; set; } = new();
    }

    public class BatchUpdateSettingsDto
    {
        public List<UpdateSettingsDto> Categories { get; set; } = new();
    }
}