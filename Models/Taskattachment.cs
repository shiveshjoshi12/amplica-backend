using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("taskattachment")]
public partial class Taskattachment
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("file_url")]
    public string? FileUrl { get; set; }

    [Column("task_id")]
    public int? TaskId { get; set; }

    [Column("file_name")]
    [StringLength(255)]
    public string? FileName { get; set; }

    [Column("uploaded_by")]
    public int? UploadedBy { get; set; }

    [Column("uploaded_at", TypeName = "timestamp without time zone")]
    public DateTime? UploadedAt { get; set; }

    [ForeignKey("TaskId")]
    [InverseProperty("Taskattachments")]
    public virtual Task? Task { get; set; }

    [ForeignKey("UploadedBy")]
    [InverseProperty("Taskattachments")]
    public virtual User? UploadedByNavigation { get; set; }
}
