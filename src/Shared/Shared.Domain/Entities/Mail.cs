// src/Shared/Domain/Entities/Mail.cs
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
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("from_mail")]
        public string FromMail { get; set; } = string.Empty;

        [Required]
        [Column("to_mail")]
        public string ToMail { get; set; } = string.Empty;

        [Column("cc_mail")]
        public string? CcMail { get; set; }

        [Column("bcc_mail")]
        public string? BccMail { get; set; }

        [Required]
        [Column("subject")]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [Column("body")]
        public string Body { get; set; } = string.Empty;

        [Column("module_name")]
        public string? ModuleName { get; set; }

        [Column("purpose")]
        public string? Purpose { get; set; }

        [Column("attachments", TypeName = "jsonb")]
        public string? AttachmentsJson { get; set; }

        [Column("is_sent")]
        public bool IsSent { get; set; }

        [Column("is_received")]
        public bool IsReceived { get; set; }

        [Column("is_read")]
        public bool IsRead { get; set; }

        [Column("is_starred")]
        public bool IsStarred { get; set; }

        [Column("is_trash")]
        public bool IsTrash { get; set; }

        [Column("sent_at")]
        public DateTime? SentAt { get; set; }

        [Column("received_at")]
        public DateTime? ReceivedAt { get; set; }

        [Column("mail_type")]
        public string? MailType { get; set; }

        [Column("parent_mail_id")]
        public long? ParentMailId { get; set; }

        [Column("in_reply_to")]
        public string? InReplyTo { get; set; }

        [Column("message_id")]
        public string? MessageId { get; set; }

        [ForeignKey(nameof(CreatedByUser))]
        [Column("created_by")]
        public Guid? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public List<string> Attachments
        {
            get => string.IsNullOrEmpty(AttachmentsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(AttachmentsJson) ?? new List<string>();
            set => AttachmentsJson = JsonSerializer.Serialize(value);
        }

        // Navigation properties
        public User? CreatedByUser { get; set; }
        public Mail? ParentMail { get; set; }
        public ICollection<Mail> Replies { get; set; } = new List<Mail>();
    }
}