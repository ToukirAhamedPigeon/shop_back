using System.ComponentModel.DataAnnotations;

namespace shop_back.src.Shared.Application.DTOs.Translations
{
    public class CreateTranslationRequest
    {
        [Required(ErrorMessage = "Key is required")]
        [StringLength(255, MinimumLength = 2, ErrorMessage = "Key must be between 2 and 255 characters")]
        public string Key { get; set; } = string.Empty;

        [Required(ErrorMessage = "Module is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Module must be between 2 and 100 characters")]
        public string Module { get; set; } = "common";

        [Required(ErrorMessage = "English value is required")]
        public string EnglishValue { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bangla value is required")]
        public string BanglaValue { get; set; } = string.Empty;
    }
}