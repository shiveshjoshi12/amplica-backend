using System.ComponentModel.DataAnnotations;

public class TaskInputDto
{
    [Required]
    [StringLength(255)]
    public string Title { get; set; } = null!;
    public int StatusId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; } // Replaced DueDate with EndDate
    public int? PriorityId { get; set; }
    public string? Description { get; set; }
    public int? TaskListId { get; set; }

    // Keep for backward compatibility
    public int? AssignedToUserId { get; set; }

    // NEW: Accept multiple user IDs
    public List<int>? AssignedUserIds { get; set; }

    public decimal? EstimatedHours { get; set; }
    public decimal? ActualHours { get; set; }
    public int? TaskOrder { get; set; }
    public int? ParentTaskId { get; set; }
    public int? ProjectId { get; set; }
}
