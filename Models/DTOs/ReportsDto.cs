public class UserTaskReportSimplifiedDto
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public DateRangeDto DateRange { get; set; }
    public TaskSummaryDto Summary { get; set; }
    public PaginationMetaDto Pagination { get; set; }
    public List<TaskDateGroupSimplifiedDto> TasksByDate { get; set; }
}

public class PaginationMetaDto
{
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalTasks { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}

public class TaskDateGroupSimplifiedDto
{
    public DateOnly Date { get; set; }
    public int TaskCount { get; set; }
    public decimal TotalEstimatedHours { get; set; }
    public decimal TotalActualHours { get; set; }
    public int TotalLoggedEntries { get; set; }
    public List<TaskDetailSimplifiedDto> Tasks { get; set; }
}

public class TaskDetailSimplifiedDto
{
    public string TaskId { get; set; }
    public string Title { get; set; }
    public string ProjectId { get; set; }
    public string ProjectName { get; set; }
    public string OriginalStatus { get; set; } // Shows the actual status name for reference
    public decimal EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }
    public int LoggedEntries { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int Progression { get; set; }
    public DateTime? LastLoggedAt { get; set; }
    public List<TimeLogEntryDto> TimeLogs { get; set; }
}

// Keep the existing DTOs
public class DateRangeDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class TaskSummaryDto
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int PendingTasks { get; set; }
    public decimal TotalEstimatedHours { get; set; }
    public decimal TotalActualHours { get; set; }
    public int TotalLoggedEntries { get; set; }
    public double EfficiencyRatio { get; set; }
}

public class TimeLogEntryDto
{
    public DateTime LoggedAt { get; set; }
    public string Duration { get; set; }
    public decimal Hours { get; set; }
    public string Description { get; set; }
}

// DTOs for company-wide (all members) report
public class CompanyTaskReportDto
{
    public string CompanyId { get; set; }
    public DateRangeDto DateRange { get; set; }
    public TaskSummaryDto CompanySummary { get; set; }
    public List<UserTaskSummaryDto> UserSummaries { get; set; }
}

public class UserTaskSummaryDto
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public TaskSummaryDto Summary { get; set; }
    public List<TaskDateGroupSimplifiedDto> TasksByDate { get; set; }
}

