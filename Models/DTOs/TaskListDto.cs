namespace BizfreeApp.Models.DTOs
{
    public class TaskListDto
    {
        public int TaskListId { get; set; }
        public string ListName { get; set; } = string.Empty; // Initialize to prevent null warnings
        public string? Description { get; set; }
        public int? ListOrder { get; set; }
        public string Status { get; set; } = string.Empty; // Initialize to prevent null warnings
        public int ProjectId { get; set; }
        public int CompanyId { get; set; }
        public DateOnly? StartDate { get; set; } // Added
        public DateOnly? EndDate { get; set; }
        public List<TaskDto> Tasks { get; set; } = new List<TaskDto>();
    }
}
