using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("company_users")]
[Index("EmployeeCode", Name = "company_users_employee_code_key", IsUnique = true)]
public partial class CompanyUser
{
    [Key]
    [Column("employee_id")]
    public int EmployeeId { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    // UPDATED: This should reference company_roles.company_role_id instead of global roles
    [Column("role_id")]
    public int RoleId { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("department_id")]
    public int? DepartmentId { get; set; }

    [Column("managed_department_id")]
    public int? ManagedDepartmentId { get; set; }

    [Column("is_active")]
    public bool? IsActive { get; set; }

    [Column("employment_type")]
    [StringLength(100)]
    public string? EmploymentType { get; set; }

    // Original Address column (if still needed, otherwise this can be replaced)
    [Column("address")]
    public string? Address { get; set; }

    // --- Address Details ---
    [Column("address_line1")]
    [StringLength(255)]
    public string? AddressLine1 { get; set; }

    [Column("city")]
    [StringLength(100)]
    public string? City { get; set; }

    [Column("state")]
    [StringLength(100)]
    public string? State { get; set; }

    [Column("postal_code")]
    [StringLength(20)]
    public string? PostalCode { get; set; }

    [Column("country")]
    [StringLength(100)]
    public string? Country { get; set; }

    // --- Personal & Contact Details ---
    [Column("gender")]
    [StringLength(20)]
    public string? Gender { get; set; }

    [Column("blood_group")]
    [StringLength(10)]
    public string? BloodGroup { get; set; }

    [Column("phone_no")]
    [StringLength(50)]
    public string? PhoneNumber { get; set; }

    [Column("date_of_birth")]
    public DateOnly? DateOfBirth { get; set; }

    [Column("marital_status")]
    [StringLength(50)]
    public string? MaritalStatus { get; set; }

    [Column("joining_date")]
    public DateOnly? JoiningDate { get; set; }

    [Column("employee_code")]
    [StringLength(100)]
    public string? EmployeeCode { get; set; }

    [Column("profile_photo_url")]
    public string? ProfilePhotoUrl { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    // --- Emergency Contact Details ---
    [Column("emergency_contact_name")]
    [StringLength(255)]
    public string? EmergencyContactName { get; set; }

    [Column("emergency_contact_relation")]
    [StringLength(100)]
    public string? EmergencyContactRelation { get; set; }

    [Column("emergency_contact_number")]
    [StringLength(50)]
    public string? EmergencyContactNumber { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public int? UpdatedBy { get; set; }

    [Column("check_admin")]
    public int? CheckAdmin { get; set; }

    [Column("first_name")]
    [StringLength(255)]
    public string? FirstName { get; set; }

    [Column("last_name")]
    [StringLength(255)]
    public string? LastName { get; set; }

    [Column("anniversary_date")]
    public DateOnly? AnniversaryDate { get; set; }

    [Column("report_to_user_id")]
    public int? ReportToUserId { get; set; }

    // REMOVED: CompanyRoleId column as we're now using RoleId to reference CompanyRole
    // [Column("company_role_id")]
    // public int? CompanyRoleId { get; set; }

    // --- Navigation Properties ---

    // UPDATED: RoleId now references CompanyRole instead of global Role
    [ForeignKey("RoleId")]
    [InverseProperty("CompanyUsers")]
    public virtual CompanyRole CompanyRole { get; set; } = null!;

    [ForeignKey("CheckAdmin")]
    [InverseProperty("CompanyUserCheckAdminNavigations")]
    public virtual Role? CheckAdminNavigation { get; set; }

    [ForeignKey("CompanyId")]
    [InverseProperty("CompanyUsers")]
    public virtual Company Company { get; set; } = null!;

    [ForeignKey("CreatedBy")]
    [InverseProperty("CompanyUserCreatedByNavigations")]
    public virtual User? CreatedByNavigation { get; set; }

    [ForeignKey("DepartmentId")]
    [InverseProperty("CompanyUsers")]
    public virtual Department? Department { get; set; }

    [InverseProperty("ReportToUser")]
    public virtual ICollection<CompanyUser> InverseReportToUser { get; set; } = new List<CompanyUser>();

    [ForeignKey("ReportToUserId")]
    [InverseProperty("InverseReportToUser")]
    public virtual CompanyUser? ReportToUser { get; set; }

    // REMOVED: Old Role navigation that referenced global roles table
    // [ForeignKey("RoleId")]
    // [InverseProperty("CompanyUserRoles")]
    // public virtual Role Role { get; set; } = null!;

    [ForeignKey("UpdatedBy")]
    [InverseProperty("CompanyUserUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("CompanyUserUsers")]
    public virtual User User { get; set; } = null!;

    [InverseProperty(nameof(AttendanceRecord.Employee))]
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new HashSet<AttendanceRecord>();
    [InverseProperty("DepartmentHead")]
    public virtual ICollection<Department> ManagedDepartments { get; set; } = new List<Department>();
}
