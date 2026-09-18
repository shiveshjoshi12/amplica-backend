using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models
{
    [Table("task_documents")]
    public partial class TaskDocument
    {
        [Key]
        [Column("document_id")]
        public int DocumentId { get; set; }

        [Column("task_id")]
        [Required]
        public int TaskId { get; set; }

        [Column("company_id")]
        [Required]
        public int CompanyId { get; set; }

        [Column("document_name")]
        [Required]
        [StringLength(255)]
        public string DocumentName { get; set; }

        [Column("document_type")]
        [StringLength(100)]
        public string? DocumentType { get; set; }

        [Column("file_path")]
        [Required]
        [StringLength(500)]
        public string FilePath { get; set; }

        [Column("file_size")]
        public long? FileSize { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("uploaded_by")]
        public int? UploadedBy { get; set; }

        [Column("version")]
        [StringLength(50)]
        public string? Version { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Navigation Properties
        [ForeignKey("TaskId")]
        [InverseProperty("TaskDocuments")]
        public virtual Task? Task { get; set; }

        [ForeignKey("CompanyId")]
        [InverseProperty("TaskDocuments")]
        public virtual Company? Company { get; set; }

        [ForeignKey("UploadedBy")]
        [InverseProperty("TaskDocuments")]
        public virtual User? UploadedByUser { get; set; }
    }
}