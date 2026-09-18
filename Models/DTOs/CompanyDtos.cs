// In your BizfreeApp.Models.DTOs namespace
using System;
using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Models.DTOs
{
    public class CompanyDto
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = null!;
        public string? CompanyAddress { get; set; }
        public string? CompanyEmail { get; set; }
        public long? CompanyPhone { get; set; }
        public string? CompanyUrl { get; set; }
        public string? CompanyLogoUrl { get; set; }

        public DateTime? SubscriptionStartDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? SubscriptionStatus { get; set; }
        public int? DaysRemaining { get; set; }

        // --- NEW FIELDS ADDED ---
        public string? AboutCompany { get; set; }
        public string? Industry { get; set; }
        public int? CompanySize { get; set; }
        public bool IsApproved { get; set; }
        public int? CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }
        // --- END OF NEW FIELDS ---

        public string? Vision { get; set; }
        public string? Mission { get; set; }
        public string? Goal { get; set; }
        public bool? IsActive { get; set; }
        public int? AdminUserId { get; set; }
        public string? AdminEmail { get; set; }
        public int? PackageId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<CompanyRoleDto>? CompanyRoles { get; set; }
        public object? Documents { get; set; }
        public int ActiveUsersCount { get; set; }
        public List<string>? CoreValues { get; internal set; }
    }

    public class CompanyCreateDto
    {
        [Required]
        [StringLength(255)]
        public string CompanyName { get; set; } = null!;

        [StringLength(255)]
        [EmailAddress]
        public string? CompanyEmail { get; set; }
        public string? CompanyAddress { get; set; }
        public long? CompanyPhone { get; set; }
        [StringLength(255)]
        public string? CompanyUrl { get; set; }
        public string? CompanyLogoUrl { get; set; }

        // --- NEW FIELDS ADDED ---
        public string? AboutCompany { get; set; }
        [StringLength(100)]
        public string? Industry { get; set; }
        public int? CompanySize { get; set; }
        public bool IsApproved { get; set; } = false; // Default to not approved
        // --- END OF NEW FIELDS ---

        public string? Vision { get; set; }
        public string? Mission { get; set; }
        public string? Goal { get; set; }
        public List<string>? CoreValues { get; set; }
        public bool? IsActive { get; set; } = true;
        public int? AdminUserId { get; set; }
        public int? PackageId { get; set; }

    }

    public class CompanyUpdateDto
    {
        [Required]
        [StringLength(255)]
        public string CompanyName { get; set; } = null!;

        [StringLength(255)]
        [EmailAddress]
        public string? CompanyEmail { get; set; }
        public string? CompanyAddress { get; set; }
        public long? CompanyPhone { get; set; }
        [StringLength(255)]
        public string? CompanyUrl { get; set; }
        public string? CompanyLogoUrl { get; set; }

        // --- NEW FIELDS ADDED ---
        public string? AboutCompany { get; set; }
        [StringLength(100)]
        public string? Industry { get; set; }
        public int? CompanySize { get; set; }
        public bool IsApproved { get; set; }
        // --- END OF NEW FIELDS ---

        public string? Vision { get; set; }
        public string? Mission { get; set; }
        public string? Goal { get; set; }
        public List<string>? CoreValues { get; set; }
        public bool? IsActive { get; set; }
        public int? AdminUserId { get; set; }
        public int? PackageId { get; set; }
    }
    public class CompanyCreateResponseDto
    {
        public Company Company { get; set; } = null!;
        public List<CompanyRoleDto> CompanyRoles { get; set; } = new List<CompanyRoleDto>();
    }
    public class DashboardStatsDto
    {
        public int TotalActiveUsers { get; set; }
        public int TotalCompanies { get; set; }
        public int ActiveCompanies { get; set; }
        public int ApprovedCompanies { get; set; }
    }
}
