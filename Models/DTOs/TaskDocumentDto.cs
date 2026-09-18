using System;

namespace BizfreeApp.Models.DTOs
{
    public class TaskDocumentDto
    {
        public int DocumentId { get; set; }
        public int TaskId { get; set; }
        public string DocumentName { get; set; } = default!;
        public string? FilePath { get; set; } // The URL or path to access the document
        public string? DocumentType { get; set; } // e.g., "pdf", "image/jpeg"
        public string? Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}