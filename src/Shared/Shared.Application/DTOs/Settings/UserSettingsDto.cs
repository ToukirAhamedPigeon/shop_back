// D:\shop\shop_back\src\Shared\Shared.Application\DTOs\Settings\UserSettingsDto.cs
using System;

namespace shop_back.src.Shared.Application.DTOs.Settings
{
    public class ThemeSettingsDto
    {
        public string primary_color { get; set; } = "#3B82F6";
        public string secondary_color { get; set; } = "#10B981";
        public string sidebar_bg_image { get; set; } = "";
        public string login_bg_image { get; set; } = "";
        public bool dark_mode { get; set; } = false;
        public string custom_css { get; set; } = "";
    }

    public class GeneralSettingsDto
    {
        public string default_language { get; set; } = "en";
        public string timezone { get; set; } = "Asia/Dhaka";
        public string date_format { get; set; } = "DD/MM/YYYY";
        public string time_format { get; set; } = "12h";
        public string currency { get; set; } = "BDT";
    }

    public class BrandingSettingsDto
    {
        public string app_name { get; set; } = "Shop Management";
        public string logo { get; set; } = "";
        public string favicon { get; set; } = "";
        public string footer_text { get; set; } = "© 2024 Shop Management. All rights reserved.";
    }

    public class UserSettingsDto
    {
        public ThemeSettingsDto Theme { get; set; } = new();
        public GeneralSettingsDto General { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class UpdateThemeSettingsDto
    {
        public string? primary_color { get; set; }
        public string? secondary_color { get; set; }
        public string? sidebar_bg_image { get; set; }
        public string? login_bg_image { get; set; }
        public bool? dark_mode { get; set; }
        public string? custom_css { get; set; }
    }

    public class UpdateGeneralSettingsDto
    {
        public string? default_language { get; set; }
        public string? timezone { get; set; }
        public string? date_format { get; set; }
        public string? time_format { get; set; }
        public string? currency { get; set; }
    }

    public class UpdateBrandingSettingsDto
    {
        public string? app_name { get; set; }
        public string? logo { get; set; }
        public string? favicon { get; set; }
        public string? footer_text { get; set; }
    }

    public class SettingsResponseDto
    {
        public UserSettingsDto User { get; set; } = new();
        public BrandingSettingsDto Branding { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public string? UpdatedBy { get; set; }
    }
}