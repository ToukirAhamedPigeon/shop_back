// D:\shop\shop_back\src\Shared\Shared.Application\DTOs\Settings\SettingsResponseDto.cs
using System;
using System.Collections.Generic;

namespace shop_back.src.Shared.Application.DTOs.Settings
{
    public class SettingsResponseDto
    {
        public List<SettingsGroupDto> Groups { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public string? UpdatedBy { get; set; }
    }
}