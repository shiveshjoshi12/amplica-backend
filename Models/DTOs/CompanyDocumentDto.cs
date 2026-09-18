using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Models.DTOs
{
    public class CompanyDocumentCreateDto
    {
        [Required]
        [StringLength(255)]
        public string DocumentName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? Version { get; set; }

        [StringLength(100)]
        public string? Category { get; set; }
    }

    public class CompanyDocumentResponseDto
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string DocumentUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? Version { get; set; }
        public string? Category { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string UpdatedByName { get; set; } = string.Empty;
    }
}
