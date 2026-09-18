namespace BizfreeApp.Dtos;

public class TaskpriorityDto
{
    public int PriorityId { get; set; }
    public string? Name { get; set; }
    public string? PriorityColor { get; set; } // Changed from Icon to PriorityColor
    public bool? IsActive { get; set; }
}