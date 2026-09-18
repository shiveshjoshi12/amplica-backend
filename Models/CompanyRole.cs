using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using global::BizfreeApp.Models; // Ensure this is correct namespace for other models
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("company_roles")]
// The unique index should now be on CompanyId and RoleName to enforce unique roles per company
[Index("CompanyId", "RoleName", Name = "uq_company_roles_company_id_role_name", IsUnique = true)]
public partial class CompanyRole
{
    [Key]
    [Column("company_role_id")]
    public int CompanyRoleId { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    // Removed RoleId as a foreign key to the global Role table.
    // The role_name here is the actual name of the role for this company.
    [Column("role_name")]
    [StringLength(100)]
    public string RoleName { get; set; } = null!;

    [Column("is_active")]
    public bool? IsActive { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public int? UpdatedBy { get; set; }

    // NEW COLUMNS - Changed to string? for storing array-like data (e.g., JSON string)
    [Column("default_role", TypeName = "text")] // Explicitly set column type to text
    public string? DefaultRole { get; set; } // Now stores text, e.g., "[\"permission1\", \"permission2\"]"

    [Column("default_permission", TypeName = "text")] // Explicitly set column type to text
    public string? DefaultPermission { get; set; } // Now stores text, e.g., "[\"read\", \"write\"]"

    // Navigation Properties
    [ForeignKey("CompanyId")]
    [InverseProperty("CompanyRoles")]
    public virtual Company Company { get; set; } = null!;

    // Inverse property to link CompanyUser to this CompanyRole
    [InverseProperty("CompanyRole")]
    public virtual ICollection<CompanyUser> CompanyUsers { get; set; } = new List<CompanyUser>();

    // Audit fields navigation (assuming User model has inverse properties for CreatedBy and UpdatedBy)
    [ForeignKey("CreatedBy")]
    [InverseProperty("CompanyRoleCreatedByNavigations")]
    public virtual User? CreatedByNavigation { get; set; }

    [ForeignKey("UpdatedBy")]
    [InverseProperty("CompanyRoleUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }

    // NEW: Inverse property to link Rolespermission to this CompanyRole
    // This assumes Rolespermission now links to CompanyRole directly, not global Role
    [InverseProperty("CompanyRole")]
    public virtual ICollection<Rolespermission> Rolespermissions { get; set; } = new List<Rolespermission>();
}
