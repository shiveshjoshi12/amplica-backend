using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[PrimaryKey("PackageId", "ModuleId")]
[Table("packagemodule")]
public partial class Packagemodule
{
    [Key]
    [Column("package_id")]
    public int PackageId { get; set; }

    [Key]
    [Column("module_id")]
    public int ModuleId { get; set; }

    [Column("package_name")]
    [StringLength(100)]
    public string? PackageName { get; set; }

    [ForeignKey("ModuleId")]
    [InverseProperty("Packagemodules")]
    public virtual Module Module { get; set; } = null!;

    [ForeignKey("PackageId")]
    [InverseProperty("Packagemodules")]
    public virtual Package Package { get; set; } = null!;
}
