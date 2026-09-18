using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BizfreeApp.Models;

[Table("department_master")]
public partial class DepartmentMaster
{
    [Key]
    [Column("dept_master_id")]
    public int DeptMasterId { get; set; }

    [Column("department_name")]
    [StringLength(255)]
    public string DepartmentName { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("created_by")]
    [StringLength(100)]
    public string? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    [StringLength(100)]
    public string? UpdatedBy { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;
}
