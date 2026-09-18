using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("taskcomment")]
public partial class Taskcomment
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("task_id")]
    public int? TaskId { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("comment")]
    [StringLength(1000)]
    public string? Comment { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("parent_comment_id")]
    public int? ParentCommentId { get; set; }

    [InverseProperty("ParentComment")]
    public virtual ICollection<Taskcomment> InverseParentComment { get; set; } = new List<Taskcomment>();

    [ForeignKey("ParentCommentId")]
    [InverseProperty("InverseParentComment")]
    public virtual Taskcomment? ParentComment { get; set; }

    [ForeignKey("TaskId")]
    [InverseProperty("Taskcomments")]
    public virtual Task? Task { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Taskcomments")]
    public virtual User? User { get; set; }
}
