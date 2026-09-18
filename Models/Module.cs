using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("modules")]
public partial class Module
{
    [Key]
    [Column("module_id")]
    public int ModuleId { get; set; }

    [Column("module_name")]
    [StringLength(255)]
    public string? ModuleName { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("is_active")]
    public bool? IsActive { get; set; }

    [InverseProperty("Module")]
    public virtual ICollection<CompanyModule> CompanyModules { get; set; } = new List<CompanyModule>();

    [InverseProperty("Module")]
    public virtual ICollection<Packagemodule> Packagemodules { get; set; } = new List<Packagemodule>();

    [InverseProperty("Module")]
    public virtual ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}
