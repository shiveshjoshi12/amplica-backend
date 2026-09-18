using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("packages")]
public partial class Package
{
    [Key]
    [Column("package_id")]
    public int PackageId { get; set; }

    [Column("package_name")]
    [StringLength(100)]
    public string PackageName { get; set; } = null!;

    [Column("price_monthly")]
    [Precision(10, 2)]
    public decimal? PriceMonthly { get; set; }

    [Column("price_yearly")]
    [Precision(10, 2)]
    public decimal? PriceYearly { get; set; }

    [Column("is_active")]
    public bool? IsActive { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("trial_days")]
    public int? TrialDays { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("updated_by")]
    public int? UpdatedBy { get; set; }

    [InverseProperty("Package")]
    public virtual ICollection<Company> Companies { get; set; } = new List<Company>();

    [ForeignKey("CreatedBy")]
    [InverseProperty("PackageCreatedByNavigations")]
    public virtual User? CreatedByNavigation { get; set; }

    [InverseProperty("Package")]
    public virtual ICollection<Packagemodule> Packagemodules { get; set; } = new List<Packagemodule>();

    [ForeignKey("UpdatedBy")]
    [InverseProperty("PackageUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }
}
