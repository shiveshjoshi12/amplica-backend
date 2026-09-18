using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("permissions")]
public partial class Permission
{
    [Key]
    [Column("permission_id")]
    public int PermissionId { get; set; }

    [Column("permission_name")]
    [StringLength(100)]
    public string PermissionName { get; set; } = null!;

    [Column("module_id")]
    public int ModuleId { get; set; }

    [ForeignKey("ModuleId")]
    [InverseProperty("Permissions")]
    public virtual Module Module { get; set; } = null!;

    [InverseProperty("Permission")]
    public virtual ICollection<Rolespermission> Rolespermissions { get; set; } = new List<Rolespermission>();
}
