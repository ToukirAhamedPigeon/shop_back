// src/Shared/Domain/Entities/MailAttachment.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace shop_back.src.Shared.Domain.Entities
{
    [Table("mail_attachments")]
    public class MailAttachment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("mail_id")]
        public long MailId { get; set; }

        [Required]
        [Column("file_name")]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [Column("file_path")]
        public string FilePath { get; set; } = string.Empty;

        [Column("file_size")]
        public long? FileSize { get; set; }

        [Column("mime_type")]
        public string? MimeType { get; set; }

        [Column("file_hash")]
        public string? FileHash { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(MailId))]
        public Mail? Mail { get; set; }
    }
}