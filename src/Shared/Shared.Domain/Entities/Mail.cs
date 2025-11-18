using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace shop_back.src.Shared.Domain.Entities
{
    [Table("mail")]
    public class Mail
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("from_mail")]
        public string FromMail { get; set; } = string.Empty;

        [Required]
        [Column("to_mail")]
        public string ToMail { get; set; } = string.Empty;

        [Required]
        [Column("subject")]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [Column("body")]
        public string Body { get; set; } = string.Empty;

        [Required]
        [Column("module_name")]
        public string ModuleName { get; set; } = string.Empty;

        [Required]
        [Column("purpose")]
        public string Purpose { get; set; } = string.Empty;

         [Column("attachments", TypeName = "jsonb")]
        public string? AttachmentsJson { get; set; } // store JSON string

        [ForeignKey(nameof(CreatedByUser))]
        [Column("created_by")]
        public Guid? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public List<string> Attachments
        {
            get => string.IsNullOrEmpty(AttachmentsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(AttachmentsJson) ?? new List<string>();
            set => AttachmentsJson = JsonSerializer.Serialize(value);
        }

        // Navigation property
        public User? CreatedByUser { get; set; }
    }
}
