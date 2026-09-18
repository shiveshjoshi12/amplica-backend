using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
namespace BizfreeApp.Models;
[Table("projects")]
public partial class Project
{
    [Key]
    [Column("project_id")]
    public int ProjectId { get; set; }
    [Column("company_id")]
    public int CompanyId { get; set; }
    [Column("code")]
    [StringLength(50)]
    public string? Code { get; set; }
    [Column("name")]
    [StringLength(255)]
    [Required(ErrorMessage = "Project Name is required.")]
    public string Name { get; set; } = null!;
    [Column("description")]
    public string? Description { get; set; }
    [Column("status")]
    [StringLength(100)]
    public string? Status { get; set; }
    [Column("start_date")]
    public DateOnly? StartDate { get; set; }
    [Column("end_date")]
    public DateOnly? EndDate { get; set; }
    [Column("is_active")]
    public bool? IsActive { get; set; }
    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }
    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
    [Column("created_by")]
    public int? CreatedBy { get; set; }
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
    [Column("updated_by")]
    public int? UpdatedBy { get; set; }
    /// <summary>
    /// Foreign Key to the CompanyTaskStatus table. This is the new, integer-based link
    /// to the company-specific status.
    /// </summary>
    [Column("status_id")]
    public int? StatusId { get; set; }
    /// <summary>
    /// Navigation property to the CompanyTaskStatus entity, representing the project's current status.
    /// This is the primary way to access the detailed status object.
    /// </summary>
    [ForeignKey("StatusId")]
    [InverseProperty("Projects")] // Links to the 'Projects' collection in CompanyTaskStatus
    public virtual CompanyTaskStatus? StatusNavigation { get; set; } // Changed type to CompanyTaskStatus
    [ForeignKey("CompanyId")]
    [InverseProperty("Projects")]
    public virtual Company Company { get; set; } = null!;
    [ForeignKey("CreatedBy")]
    [InverseProperty("ProjectCreatedByNavigations")]
    public virtual User? CreatedByNavigation { get; set; }
    [InverseProperty("Project")]
    public virtual ICollection<ProjectMember> ProjectMembers { get; set; } = new List<ProjectMember>();
    [InverseProperty("Project")]
    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
    [ForeignKey("UpdatedBy")]
    [InverseProperty("ProjectUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }
    [InverseProperty("Project")]
    public virtual ICollection<ProjectDocument> ProjectDocuments { get; set; } = new List<ProjectDocument>();
    [InverseProperty("Project")]
    public virtual ICollection<TaskList> TaskLists { get; set; } = new List<TaskList>();
    [InverseProperty("Project")] //
    public virtual ICollection<TaskTodo> TaskTodos { get; set; } = new List<TaskTodo>();
   
    // Non-mapped field for Progression
    //[NotMapped]
    //public int Progression
    //{
    //    get
    //    {
    //        // Use a static Random instance to avoid getting the same sequence of numbers
    //        // if this property is accessed rapidly.
    //        // For a production application, consider a more robust way to manage Random instances.
    //        return _random.Next(0, 101); // 0 to 100 inclusive
    //    }
    //}
    private static readonly Random _random = new Random(); // Keep if other random uses exist, otherwise remove.

    // Non-mapped field for Progression
    [NotMapped]
    public int Progression
    {
        get
        {
            // Calculate progression based on top-level tasks
            if (Tasks != null && Tasks.Any())
            {
                // Filter for top-level tasks (those with no ParentTaskId) and are not deleted
                var topLevelTasks = Tasks.Where(t => !t.ParentTaskId.HasValue && !t.IsDeleted).ToList();

                if (!topLevelTasks.Any())
                {
                    return 0; // No active top-level tasks, so 0% progression
                }

                // Sum the progression of all top-level tasks
                double totalProgression = topLevelTasks.Sum(t => t.Progression);

                // Calculate average progression
                return (int)Math.Round(totalProgression / topLevelTasks.Count);
            }
            return 0; // No tasks in the project
        }
    }
}