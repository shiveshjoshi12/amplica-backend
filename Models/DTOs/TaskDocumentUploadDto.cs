using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Models.DTOs
{
    public class TaskDocumentUploadDto
    {
        [Required(ErrorMessage = "Document file is required.")]
        public IFormFile File { get; set; } = default!;

        [Required(ErrorMessage = "Document name is required.")]
        [StringLength(255, ErrorMessage = "Document name cannot exceed 255 characters.")]
        public string DocumentName { get; set; } = default!;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [StringLength(50, ErrorMessage = "Version cannot exceed 50 characters.")]
        public string? Version { get; set; }
    }
}