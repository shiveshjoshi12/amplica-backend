using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("roles")]
[Index("CheckAdmin", Name = "uq_roles_check_admin")]
public partial class Role
{
    [Key]
    [Column("role_id")]
    public int RoleId { get; set; }

    [Column("role_name")]
    [StringLength(100)]
    public string RoleName { get; set; } = null!;

    // CompanyId in Role: If roles are truly global/default, this should probably be removed or always null.
    // If it allows for some roles to be global (CompanyId = null) and some predefined for specific companies,
    // then keep it nullable. For now, assuming it can be null for global roles.
    [Column("company_id")]
    public int? CompanyId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("check_admin")]
    public int? CheckAdmin { get; set; }


    [ForeignKey("CompanyId")]
    [InverseProperty("Roles")]
    public virtual Company Company { get; set; } = null!;

    [InverseProperty("CheckAdminNavigation")]
    public virtual ICollection<CompanyUser> CompanyUserCheckAdminNavigations { get; set; } = new List<CompanyUser>();

    [InverseProperty("Role")]
    public virtual ICollection<Rolespermission> Rolespermissions { get; set; } = new List<Rolespermission>();

    [InverseProperty("Role")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();

    // Removed the collection linking to CompanyRole, as Role is now independent of CompanyRole creations:
    // [InverseProperty("Role")]
    // public virtual ICollection<CompanyRole> CompanyRoles { get; set; } = new List<CompanyRole>();
}
