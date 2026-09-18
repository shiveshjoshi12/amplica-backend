using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("task_todos")] // New table name
public partial class TaskTodo
{
    [Key]
    [Column("task_todo_id")] // Primary key for TaskTodo
    public int TaskTodoId { get; set; }

    // ProjectId and TaskListId will be nullable and effectively ignored for Todos
    [Column("project_id")]
    public int? ProjectId { get; set; } // Will be null or 0 for todos

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

    [Column("end_date")] // Keep EndDate for consistency with Task
    public DateOnly? EndDate { get; set; }

    [Column("daily_log", TypeName = "decimal(10,2)")]
    public decimal? DailyLog { get; set; }

    [Column("company_id")]
    public int? CompanyId { get; set; }

    [Column("task_list_id")]
    public int? TaskListId { get; set; } // Will be null or 0 for todos

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
    public int? ParentTaskId { get; set; } // Todos can also have sub-todos

    // Progression property (copied from Task, adjust if logic differs for todos)
    // Removed _random as it's not used if Progression depends only on subtasks/status
    [NotMapped]
    public int Progression
    {
        get
        {
            if (SubTasks != null && SubTasks.Any())
            {
                var activeSubtasks = SubTasks.Where(st => !st.IsDeleted).ToList();
                if (!activeSubtasks.Any())
                {
                    return 0;
                }
                double totalSubtaskProgression = activeSubtasks.Sum(st => st.Progression);
                return (int)Math.Round(totalSubtaskProgression / activeSubtasks.Count);
            }
            else
            {
                if (StatusNavigation != null && StatusNavigation.Name.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    return 100;
                }
                return 0;
            }
        }
    }

    // Navigation properties - ensure these are also configured correctly in ApplicationDbContext
    [ForeignKey("AssignedTo")]
    [InverseProperty("TaskTodos")] // Links to ICollection<TaskTodo> TaskTodos in User.cs
    public virtual User? AssignedToNavigation { get; set; }

    [ForeignKey("CompanyId")]
    [InverseProperty("TaskTodos")] // Links to ICollection<TaskTodo> TaskTodos in Company.cs
    public virtual Company? Company { get; set; }

    [ForeignKey("PriorityId")]
    [InverseProperty("TaskTodos")] // Links to ICollection<TaskTodo> TaskTodos in Taskpriority.cs
    public virtual Taskpriority? Priority { get; set; }

    // Project and TaskList navigation properties will still exist, but will often be null for todos
    [ForeignKey("ProjectId")]
    [InverseProperty("TaskTodos")] // Links to ICollection<TaskTodo> TaskTodos in Project.cs
    public virtual Project? Project { get; set; }

    [ForeignKey("Status")]
    [InverseProperty("TaskTodos")] // Links to ICollection<TaskTodo> TaskTodos in CompanyTaskStatus.cs
    public virtual CompanyTaskStatus? StatusNavigation { get; set; }

    [ForeignKey("TaskListId")]
    [InverseProperty("TaskTodos")] // Links to ICollection<TaskTodo> TaskTodos in TaskList.cs
    public virtual TaskList? TaskList { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("TaskTodosCreated")] // Links to ICollection<TaskTodo> TaskTodosCreated in User.cs
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("UpdatedBy")]
    [InverseProperty("TaskTodosUpdated")] // Links to ICollection<TaskTodo> TaskTodosUpdated in User.cs
    public virtual User? UpdatedByUser { get; set; }

    // Removed TaskTodoTimelogs, TaskTodoDocuments based on your instruction
    // public virtual ICollection<TaskTodoattachment> TaskTodoattachments { get; set; } = new List<TaskTodoattachment>(); // Removed as per latest discussion if all related tables are gone
    // public virtual ICollection<TaskTodocomment> TaskTodocomments { get; set; } = new List<TaskTodocomment>(); // Removed as per latest discussion if all related tables are gone

    [ForeignKey("ParentTaskId")]
    [InverseProperty("SubTasks")] // Self-referencing: A sub-task todo's ParentTask is another TaskTodo, and ParentTask has a collection of SubTasks
    public virtual TaskTodo? ParentTask { get; set; }

    [InverseProperty("ParentTask")] // Self-referencing: The ParentTask navigation property on TaskTodo is the inverse of this SubTasks collection
    public virtual ICollection<TaskTodo> SubTasks { get; set; } = new List<TaskTodo>();
}