// BizfreeApp.Models.DTOs/CreateProjectWithDocumentDto.cs
using System;
using Microsoft.AspNetCore.Http; // For IFormFile
using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Models.DTOs
{
    public class CreateProjectWithDocumentDto
    {
        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Status { get; set; } // e.g., "Planned", "InProgress", "Completed"

        public DateOnly? StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        [Required]
        public int CompanyId { get; set; } // Company the project belongs to

        // Document related properties
        public IFormFile? DocumentFile { get; set; } // The actual file
        public string? DocumentName { get; set; } // Name for the document (if different from file name)
        public string? DocumentDescription { get; set; }
        public string? DocumentVersion { get; set; }
    }
}



namespace BizfreeApp.Models.DTOs
{
    public class UpdateProjectWithDocumentDto
    {
        // All fields are optional for update, but we keep validation for clarity
        // Add [Required] if a field is mandatory for updates.
        [StringLength(255)]
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Status { get; set; }

        public DateOnly? StartDate { get; set; }

        public DateOnly? EndDate { get; set; }

        // Document related properties (optional for update)
        public IFormFile? DocumentFile { get; set; } // The actual file
        public string? DocumentName { get; set; } // Name for the document
        public string? DocumentDescription { get; set; }
        public string? DocumentVersion { get; set; }
    }
}