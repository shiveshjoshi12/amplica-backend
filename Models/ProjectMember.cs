using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("project_members")]
public partial class ProjectMember
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("project_id")]
    public int ProjectId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("added_by")]
    public int? AddedBy { get; set; }

    [Column("joined_at")]
    public DateOnly? JoinedAt { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [ForeignKey("AddedBy")]
    [InverseProperty("ProjectMemberAddedByNavigations")]
    public virtual User? AddedByNavigation { get; set; }

    [ForeignKey("ProjectId")]
    [InverseProperty("ProjectMembers")]
    public virtual Project Project { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("ProjectMemberUsers")]
    public virtual User User { get; set; } = null!;
}
