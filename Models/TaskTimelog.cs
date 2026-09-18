using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("task_timelogs")]
public partial class TaskTimelog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("task_id")]
    public int? TaskId { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }


    [Column("logged_at", TypeName = "timestamp without time zone")]
    public DateTime LoggedAt { get; set; }

    [Column("duration", TypeName = "varchar(5)")] 
    public string? Duration { get; set; } 


    [Column("description")]
    public string? Description { get; set; }

    [ForeignKey("TaskId")]
    [InverseProperty("TaskTimelogs")]
    public virtual Task? Task { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("TaskTimelogs")]
    public virtual User? User { get; set; }
}
