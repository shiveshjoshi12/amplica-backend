using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("tasks")]
public partial class Task
{
    [Key]
    [Column("task_id")]
    public int TaskId { get; set; }

    [Column("project_id")]
    public int? ProjectId { get; set; }

    [Column("assigned_to")]
    public int? AssignedTo { get; set; }

    [Column("title")]
    [StringLength(255)]
    public string? Title { get; set; }

    [Column("priority_id")]
    public int? PriorityId { get; set; }

    /// <summary>
    /// Foreign Key to the CompanyTaskStatus table. This is the integer-based link
    /// to the company-specific task status. The column name in the database is 'status'.
    /// </summary>
    [Column("status")]
    public int? Status { get; set; }

    [Column("start_time")]
    public TimeOnly? StartTime { get; set; }

    [Column("end_time")]
    public TimeOnly? EndTime { get; set; }

    [Column("start_date")]
    public DateOnly? StartDate { get; set; }

    [Column("due_date")]
    public DateOnly? DueDate { get; set; }

    [Column("end_date")]
    public DateOnly? EndDate { get; set; }

    [Column("daily_log", TypeName = "decimal(10,2)")]
    public decimal? DailyLog { get; set; }

    [Column("company_id")]
    public int? CompanyId { get; set; }

    [Column("task_list_id")]
    public int? TaskListId { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("estimated_hours", TypeName = "decimal(10,2)")]
    public decimal? EstimatedHours { get; set; }

    [Column("actual_hours", TypeName = "decimal(10,2)")]
    public decimal? ActualHours { get; set; }

    [Column("task_order")]
    public int? TaskOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_by")]
    public int? UpdatedBy { get; set; }

    [Column("parent_task_id")]
    public int? ParentTaskId { get; set; }

    [Column("assigned_user_ids")]
    public string? AssignedUserIds { get; set; }

    //private static readonly Random _random = new Random();

    //[NotMapped] // This property will not be mapped to a database column
    //public int Progression
    //{
    //    get
    //    {
    //        lock (_random) // Ensure thread-safety for Random
    //        {
    //            return _random.Next(0, 101); // Generates a random number between 0 and 100 (inclusive)
    //        }
    //    }
    //}

    private static readonly Random _random = new Random(); // Keep if other random uses exist, otherwise remove.

    [NotMapped] // This property will not be mapped to a database column
    public int Progression
    {
        get
        {
            // If the task has subtasks, its progression is an average of its subtasks' progression
            if (SubTasks != null && SubTasks.Any())
            {
                // Ensure subtasks are loaded and not deleted for calculation
                var activeSubtasks = SubTasks.Where(st => !st.IsDeleted).ToList();
                if (!activeSubtasks.Any())
                {
                    return 0; // No active subtasks, so 0% progression
                }

                double totalSubtaskProgression = activeSubtasks.Sum(st => st.Progression);
                return (int)Math.Round(totalSubtaskProgression / activeSubtasks.Count);
            }
            // If no subtasks, progression is based on its own status
            else
            {
                // Assuming "Completed" is the status that signifies 100% progression.
                // Ensure StatusNavigation is loaded in your EF Core queries.
                if (StatusNavigation != null && StatusNavigation.Name.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    return 100;
                }
                return 0;
            }
        }
    }

    [NotMapped]
    public List<int> AssignedUserIdList
    {
        get
        {
            if (string.IsNullOrEmpty(AssignedUserIds))
                return new List<int>();

            return AssignedUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.Parse(id.Trim()))
                .ToList();
        }
        set
        {
            AssignedUserIds = value != null && value.Any()
                ? string.Join(",", value)
                : null;
        }
    }

    [ForeignKey("AssignedTo")]
    [InverseProperty("Tasks")]
    public virtual User? AssignedToNavigation { get; set; }

    [ForeignKey("CompanyId")]
    [InverseProperty("Tasks")]
    public virtual Company? Company { get; set; }

    [ForeignKey("PriorityId")]
    [InverseProperty("Tasks")]
    public virtual Taskpriority? Priority { get; set; }

    [ForeignKey("ProjectId")]
    [InverseProperty("Tasks")]
    public virtual Project? Project { get; set; }

    /// <summary>
    /// Navigation property to the CompanyTaskStatus entity, representing the task's current status.
    /// This links to the 'status' foreign key column.
    /// </summary>
    [ForeignKey("Status")] // Links to the 'Status' property (which is the FK column)
    [InverseProperty("Tasks")] // Links to the 'Tasks' collection in CompanyTaskStatus
    public virtual CompanyTaskStatus? StatusNavigation { get; set; } // Changed type to CompanyTaskStatus

    [ForeignKey("TaskListId")]
    [InverseProperty("Tasks")]
    public virtual TaskList? TaskList { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("TasksCreated")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("UpdatedBy")]
    [InverseProperty("TasksUpdated")]
    public virtual User? UpdatedByUser { get; set; }

    [InverseProperty("Task")]
    public virtual ICollection<TaskTimelog> TaskTimelogs { get; set; } = new List<TaskTimelog>();

    [InverseProperty("Task")]
    public virtual ICollection<TaskDocument> TaskDocuments { get; set; } = new List<TaskDocument>();

    [InverseProperty("Task")]
    public virtual ICollection<Taskattachment> Taskattachments { get; set; } = new List<Taskattachment>();

    [InverseProperty("Task")]
    public virtual ICollection<Taskcomment> Taskcomments { get; set; } = new List<Taskcomment>();

    [ForeignKey("ParentTaskId")]
    [InverseProperty("SubTasks")]
    public virtual Task? ParentTask { get; set; }

    [InverseProperty("ParentTask")]
    public virtual ICollection<Task> SubTasks { get; set; } = new List<Task>();
}
