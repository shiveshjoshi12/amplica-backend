using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore; // Added for DbContext, etc. if this file is standalone

namespace BizfreeApp.Models;

[Table("company_task_status")] // This table is named "company_task_status"
public partial class CompanyTaskStatus
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("default_task_status_id")]
    public int? DefaultTaskStatusId { get; set; }

    [Column("name")]
    [StringLength(100)]
    [Required]
    public string Name { get; set; } = null!;

    [Column("slug")]
    [StringLength(100)]
    [Required] // Slug should still be required for internal consistency
    public string Slug { get; set; } = null!;

    [Column("order")]
    public int Order { get; set; }

    [Column("status_color")]
    [StringLength(50)]
    public string? StatusColor { get; set; }

    [Column("is_custom")]
    public bool IsCustom { get; set; } = false;

    [Column("is_editable_name")]
    public bool IsEditableName { get; set; } = true;

    [Column("is_deletable")]
    public bool IsDeletable { get; set; } = true;

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp without time zone")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public int? UpdatedBy { get; set; }

    // Navigation properties for relationships
    [ForeignKey("CompanyId")]
    [InverseProperty("CompanyTaskStatuses")]
    public virtual Company Company { get; set; } = null!; // Assuming a Company model exists

    [ForeignKey("DefaultTaskStatusId")]
    [InverseProperty("CompanyTaskStatuses")] // This refers to the collection in Taskstatus (the global master)
    public virtual BizfreeApp.Models.Taskstatus? DefaultTaskStatusNavigation { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("CompanyTaskStatusCreatedByNavigations")]
    public virtual User? CreatedByNavigation { get; set; }

    [ForeignKey("UpdatedBy")]
    [InverseProperty("CompanyTaskStatusUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }

    // Collections for tasks and projects that will now link to CompanyTaskStatus
    [InverseProperty("StatusNavigation")]
    public virtual ICollection<BizfreeApp.Models.Task> Tasks { get; set; } = new List<BizfreeApp.Models.Task>();

    [InverseProperty("StatusNavigation")]
    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

    [InverseProperty("StatusNavigation")] 
    public virtual ICollection<TaskTodo> TaskTodos { get; set; } = new List<TaskTodo>();
}
