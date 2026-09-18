using BizfreeApp.Models.DTOs;

public class TaskDto
{
    public int TaskId { get; set; }
    public string? Title { get; set; }
    public string? StatusName { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; } // Replaced DueDate with EndDate
    public string? PriorityName { get; set; }
    public decimal? EstimatedHours { get; set; }
    public string? Description { get; set; }

    // Keep for backward compatibility
    public int? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public string? AssignedToUserAvatarUrl { get; set; }

    // NEW: Multiple assigned users
    public List<AssignedUserInfo>? AssignedUsers { get; set; }

    public int? TaskListId { get; set; }
    public string? ListName { get; set; }
    public string? TaskListDescription { get; set; }
    public int? ListOrder { get; set; }
        public int? ProjectId { get; set; } // Added for completeness
    public string? Name { get; set; }
    public string? CompanyName { get; set; }
        public int? ParentTaskId { get; set; } // Added this line for Parent Task ID

    public List<TaskDto>? Subtasks { get; set; }
    public List<TaskDocumentDto>? Documents { get; set; }
    public int Progression { get; internal set; }
}

public class AssignedUserInfo
{
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public string? AvatarUrl { get; set; }
}
