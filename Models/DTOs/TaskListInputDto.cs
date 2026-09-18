using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Models.DTOs
{
    public class TaskListInputDto
    {
        [Required(ErrorMessage = "List name is required.")]
        [MaxLength(255)]
        public string ListName { get; set; } = string.Empty; 

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int? ListOrder { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
    }
}