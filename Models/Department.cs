using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("department")]
public partial class Department
{
    [Key]
    [Column("dept_id")]
    public int DeptId { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("department_name")]
    [StringLength(150)]
    public string DepartmentName { get; set; } = null!;

    [Column("department_head_user_id")]
    public int? DepartmentHeadUserId { get; set; }

    // NEW: Description field
    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public int? UpdatedBy { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [ForeignKey("CompanyId")]
    [InverseProperty("Departments")]
    public virtual Company Company { get; set; } = null!;

    [InverseProperty("Department")]
    public virtual ICollection<CompanyUser> CompanyUsers { get; set; } = new List<CompanyUser>();

    [ForeignKey("CreatedBy")]
    [InverseProperty("DepartmentCreatedByNavigations")]
    public virtual User? CreatedByNavigation { get; set; }

    [ForeignKey("UpdatedBy")]
    [InverseProperty("DepartmentUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }
    [ForeignKey("DepartmentHeadUserId")]
    [InverseProperty("ManagedDepartments")]
    public virtual CompanyUser? DepartmentHead { get; set; }
}
