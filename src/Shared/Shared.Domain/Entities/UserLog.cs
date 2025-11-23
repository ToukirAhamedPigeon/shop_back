using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.src.Shared.Domain.Entities
{
   [Table("user_logs")]
public class UserLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("detail")]
    public string? Detail { get; set; }

    [Column("changes", TypeName = "jsonb")]
    public string? Changes { get; set; }

    [Required]
    [Column("action_type")]
    public string ActionType { get; set; } = string.Empty;

    [Required]
    [Column("model_name")]
    public string ModelName { get; set; } = string.Empty;

    [Column("model_id")]
    public Guid? ModelId { get; set; }

    [Required]
    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("created_at_id")]
    public long CreatedAtId { get; set; }

    // NEW FIELDS
    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("browser")]
    public string? Browser { get; set; }

    [Column("device")]
    public string? Device { get; set; }

    [Column("os")]
    public string? OperatingSystem { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }
}

}
