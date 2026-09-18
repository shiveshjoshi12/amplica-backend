using System.ComponentModel.DataAnnotations;
namespace BizfreeApp.Models.DTOs
{
    public class ProjectInputDto
    {
        [Required(ErrorMessage = "Project Name is required.")]
        public string Name { get; set; } = null!;
        public string? Status { get; set; }
        public string? Description { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int CompanyId { get; set; } 

    }
}
