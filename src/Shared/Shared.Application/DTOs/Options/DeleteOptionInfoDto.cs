namespace shop_back.src.Shared.Application.DTOs.Options
{
    public class DeleteOptionInfoDto
    {
        public bool CanBePermanent { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool HasChildren { get; set; }
        public int ChildrenCount { get; set; }
    }
}