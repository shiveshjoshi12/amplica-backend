using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("taskstatus")] // This table remains named "taskstatus"
public partial class Taskstatus
{
    [Key]
    [Column("status_id")]
    public int StatusId { get; set; }

    [Column("name")]
    [StringLength(100)]
    [Required]
    public string Name { get; set; } = null!;

    [Column("status_color")]
    [StringLength(50)]
    public string? StatusColor { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp without time zone")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public int? UpdatedBy { get; set; }

    // Navigation properties for audit users
    [ForeignKey("CreatedBy")]
    [InverseProperty("TaskstatusCreatedByNavigations")] // Assuming User has this collection
    public virtual User? CreatedByNavigation { get; set; }

    [ForeignKey("UpdatedBy")]
    [InverseProperty("TaskstatusUpdatedByNavigations")] // Assuming User has this collection
    public virtual User? UpdatedByNavigation { get; set; }

    // Collection for CompanyTaskStatus entries that link to this global status
    [InverseProperty("DefaultTaskStatusNavigation")]
    public virtual ICollection<CompanyTaskStatus> CompanyTaskStatuses { get; set; } = new List<CompanyTaskStatus>();
}