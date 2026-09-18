// BizfreeApp.Models/Taskpriority.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("taskpriorities")]
public partial class Taskpriority
{
    [Key]
    [Column("priority_id")]
    public int PriorityId { get; set; }

    [Column("name")]
    [StringLength(100)]
    public string? Name { get; set; }

    // --- COLUMN NAME CHANGE HERE ---
    [Column("priority_color")] // Changed from "icon" to "priority_color"
    public string? PriorityColor { get; set; } // Changed property name from Icon to PriorityColor
    // --- END COLUMN NAME CHANGE ---

    [Column("is_active")]
    public bool? IsActive { get; set; }

    [InverseProperty("Priority")]
    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();

    [InverseProperty("Priority")] //
    public virtual ICollection<TaskTodo> TaskTodos { get; set; } = new List<TaskTodo>();

}