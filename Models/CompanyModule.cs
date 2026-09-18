using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("company_modules")]
public partial class CompanyModule
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("module_id")]
    public int ModuleId { get; set; }

    [Column("enabled")]
    public bool? Enabled { get; set; }

    [Column("enabled_at")]
    public DateTime? EnabledAt { get; set; }

    [Column("updated_by")]
    public int? UpdatedBy { get; set; }

    [ForeignKey("CompanyId")]
    [InverseProperty("CompanyModules")]
    public virtual Company Company { get; set; } = null!;

    [ForeignKey("ModuleId")]
    [InverseProperty("CompanyModules")]
    public virtual Module Module { get; set; } = null!;

    [ForeignKey("UpdatedBy")]
    [InverseProperty("CompanyModules")]
    public virtual User? UpdatedByNavigation { get; set; }
}
