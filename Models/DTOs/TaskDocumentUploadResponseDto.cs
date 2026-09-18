using System;

namespace BizfreeApp.Models.DTOs
{
    public class TaskDocumentUploadResponseDto
    {
        public int DocumentId { get; set; }
        public int TaskId { get; set; }
        public string? DocumentName { get; set; }
        public string? FilePath { get; set; }
        public string? DocumentType { get; set; }
        public long? FileSize { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}