//// BizfreeApp.Models.DTOs/CreateTaskWithDocumentDto.cs
//using System;
//using Microsoft.AspNetCore.Http; // For IFormFile
//using System.ComponentModel.DataAnnotations;

//namespace BizfreeApp.Models.DTOs
//{
//    public class CreateTaskWithDocumentDto
//    {
//        // Properties from TaskInputDto
//        [Required]
//        [StringLength(255)]
//        public string? Title { get; set; }

//        public int? StatusId { get; set; }

//        public DateOnly? DueDate { get; set; }

//        public int? PriorityId { get; set; }

//        public string? Description { get; set; }

//        public int? AssignedToUserId { get; set; }

//        public decimal? EstimatedHours { get; set; }

//        public decimal? ActualHours { get; set; }

//        public int? TaskOrder { get; set; }

//        // Added for subtask functionality - ensure this is handled carefully on the API side
//        public int? ParentTaskId { get; set; }

//        // Document related properties
//        public IFormFile? DocumentFile { get; set; } // The actual file
//        public string? DocumentName { get; set; } // Name for the document (if different from file name)
//        public string? DocumentDescription { get; set; }
//        public string? DocumentVersion { get; set; }
//    }
//}


//namespace BizfreeApp.Models.DTOs
//{
//    public class UpdateTaskWithDocumentDto
//    {
//        // All fields are optional for update, as per your TaskInputDto pattern
//        [StringLength(255)]
//        public string? Title { get; set; }

//        public int? StatusId { get; set; }

//        public DateOnly? DueDate { get; set; }

//        public int? PriorityId { get; set; }

//        public string? Description { get; set; }

//        public int? AssignedToUserId { get; set; }

//        public decimal? EstimatedHours { get; set; }

//        public decimal? ActualHours { get; set; }

//        public int? TaskOrder { get; set; }

//        // Added for subtask functionality - ensure this is handled carefully on the API side
//        public int? ParentTaskId { get; set; }

//        // Document related properties (optional for update)
//        public IFormFile? DocumentFile { get; set; } // The actual file
//        public string? DocumentName { get; set; } // Name for the document
//        public string? DocumentDescription { get; set; }
//        public string? DocumentVersion { get; set; }
//    }
//}