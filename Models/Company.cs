using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("companies")]
[Index("CompanyEmail", Name = "companies_company_email_key", IsUnique = true)]
public partial class Company
{
    [Key]
    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("company_name")]
    [StringLength(255)]
    public string CompanyName { get; set; } = null!;

    // --- NEW: General description field ---
    [Column("about_company")]
    public string? AboutCompany { get; set; }

    [Column("company_address")] // Maps to "Location"
    public string? CompanyAddress { get; set; }

    [Column("company_email")] // Maps to "Primary Contact Email"
    [StringLength(255)]
    public string? CompanyEmail { get; set; }

    [Column("company_phone")] // Maps to "Primary Contact Phone"
    public long? CompanyPhone { get; set; }

    [Column("company_url")] // Maps to "URL"
    [StringLength(255)]
    public string? CompanyUrl { get; set; }

    [Column("company_logo_url")] // Maps to "Logo"
    public string? CompanyLogoUrl { get; set; }

    // --- NEW: Industry and Size ---
    [Column("industry")]
    [StringLength(100)]
    public string? Industry { get; set; }

    [Column("company_size")]
    public int? CompanySize { get; set; } // e.g., Number of employees

    // Existing "About" fields
    [Column("vision")]
    public string? Vision { get; set; }

    [Column("mission")]
    public string? Mission { get; set; }

    [Column("goal")]
    public string? Goal { get; set; }

    [Column("core_values", TypeName = "jsonb")]  // For PostgreSQL
    public string? CoreValues { get; set; } // Should hold JSON array
    // --- NEW: Approval Status ---
    [Column("is_approved")]
    public bool IsApproved { get; set; } = false; // Maps to "isApproved"

    [Column("is_active")] // Maps to "isActive"
    public bool? IsActive { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    // Foreign Keys and Audit Fields
    [Column("admin_user_id")] // Maps to "Company Admin"
    public int? AdminUserId { get; set; }

    [Column("package_id")] // Maps to "Package Assignment"
    public int? PackageId { get; set; }

    [Column("created_at")] // Maps to "Created"
    public DateTime? CreatedAt { get; set; }

    // --- NEW: Audit User Fields ---
    [Column("created_by")] // Maps to "Created By"
    public int? CreatedBy { get; set; }

    [Column("updated_at")] // Maps to "Updated"
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")] // Maps to "Updated By"
    public int? UpdatedBy { get; set; }

    // Navigation Properties

    [ForeignKey("CreatedBy")]
    [InverseProperty("CompanyCreatedByNavigations")]
    public virtual User? CreatedByNavigation { get; set; }

    [ForeignKey("UpdatedBy")]
    [InverseProperty("CompanyUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }

    [InverseProperty("Company")]
    public virtual ICollection<Client> Clients { get; set; } = new List<Client>();

    [InverseProperty("Company")]
    public virtual ICollection<CompanyModule> CompanyModules { get; set; } = new List<CompanyModule>();

    [InverseProperty("Company")]
    public virtual ICollection<CompanyUser> CompanyUsers { get; set; } = new List<CompanyUser>();

    [InverseProperty("Company")]
    public virtual ICollection<CompanyRole> CompanyRoles { get; set; } = new List<CompanyRole>();

    [InverseProperty("Company")]
    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    [ForeignKey("PackageId")]
    [InverseProperty("Companies")]
    public virtual Package? Package { get; set; }

    [InverseProperty("Company")]
    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

    [InverseProperty("Company")]
    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    [InverseProperty("Company")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();

    [InverseProperty("Company")]
    public virtual ICollection<Rolespermission> Rolespermissions { get; set; } = new List<Rolespermission>();

    [InverseProperty("Company")]
    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();

    [InverseProperty("Company")]
    public virtual ICollection<CompanyTaskStatus> CompanyTaskStatuses { get; set; } = new List<CompanyTaskStatus>();

    [InverseProperty("Company")]
    public virtual ICollection<TaskDocument> TaskDocuments { get; set; } = new List<TaskDocument>();

    [InverseProperty("Company")]
    public virtual ICollection<ProjectDocument> ProjectDocuments { get; set; } = new List<ProjectDocument>();

    [InverseProperty("Company")]
    public virtual ICollection<TaskTodo> TaskTodos { get; set; } = new List<TaskTodo>();
    public virtual ICollection<CompanyDocument> Documents { get; set; } = new List<CompanyDocument>();
}