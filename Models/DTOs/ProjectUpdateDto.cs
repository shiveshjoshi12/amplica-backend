using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Models.DTOs
{
    public class ProjectUpdateDto
    {
        // Fields that can be updated
        [Required(ErrorMessage = "Project Name is required.")]
        public string Name { get; set; } = null!;
        public string? Status { get; set; }
        public string? Description { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }

        // Note: CompanyId is usually NOT updated after creation.
        // If you need to allow it, uncomment this and add validation in the controller.
        // public int? CompanyId { get; set; }
    }
}
