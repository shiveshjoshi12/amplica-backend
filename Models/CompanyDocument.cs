using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BizfreeApp.Models
{
    [Table("company_documents")]
    public class CompanyDocument
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Company")]
        public int CompanyId { get; set; }

        [Required]
        [StringLength(255)]
        public string DocumentName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [StringLength(10)]
        public string DocumentType { get; set; } = string.Empty; // e.g., "PDF", "DOC", "XLS", "IMG"

        [Required]
        public string DocumentUrl { get; set; } = string.Empty;

        [Required]
        public long FileSize { get; set; } // File size in bytes

        [StringLength(50)]
        public string? Version { get; set; }

        [StringLength(100)]
        public string? Category { get; set; } // e.g., "Legal", "Financial", "Compliance", "Certificate"

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;

        // Audit fields
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CreatedByUser")]
        public int CreatedBy { get; set; }

        [ForeignKey("UpdatedByUser")]
        public int UpdatedBy { get; set; }

        // Navigation properties
        public virtual Company Company { get; set; } = null!;

        public virtual User CreatedByUser { get; set; } = null!;

        public virtual User UpdatedByUser { get; set; } = null!;
    }
}