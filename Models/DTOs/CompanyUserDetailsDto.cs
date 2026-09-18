using System;

namespace BizfreeApp.DTOs;

public class CompanyUserDetailsDto
{
    // CompanyUser specific properties
    public int UserId { get; set; } // Main identifier for the DTO
    public int CompanyEmployeeId { get; set; } // Represents employee_id from company_users table
    public int RoleId { get; set; }
    public int CompanyId { get; set; }
    public int? DepartmentId { get; set; }
    public bool? IsActive { get; set; }
    public string? EmploymentType { get; set; }
    public string? Address { get; set; }
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; } // Added Country
    public string? Gender { get; set; }
    public string? BloodGroup { get; set; }
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? MaritalStatus { get; set; }
    public DateOnly? JoiningDate { get; set; } // Re-added for returning in GET (was removed from Create/Update DTO)
    public string? EmployeeCode { get; set; }  // Re-added for returning in GET (was removed from Create/Update DTO)
    public string? ProfilePhotoUrl { get; set; }
    public string? Description { get; set; }
    public bool? IsDeleted { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public int? CheckAdmin { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? AnniversaryDate { get; set; }
    public int? ReportToUserId { get; set; }
    public string? ReportToUserName { get; set; }

    // Emergency Contact Details
    public string? EmergencyContactName { get; set; } // Added
    public string? EmergencyContactRelation { get; set; } // Added
    public string? EmergencyContactNumber { get; set; } // Added

    // User specific properties (mapped from the related User model)
    public string UserEmail { get; set; } = string.Empty;
    // Removed UserPasswordHash, UserRefreshToken, UserRefreshTokenExpiryTime for security
    public bool UserIsActive { get; set; }
    public bool UserIsDeleted { get; set; }
    public DateTime? UserCreatedAt { get; set; }
    public DateTime? UserUpdatedAt { get; set; }
    public int? UserUpdatedBy { get; set; }
    public int? UserRoleId { get; set; }
    public int? UserCompanyId { get; set; }

    // Department property
    public string? DepartmentName { get; set; }
    public string? RoleName { get; set; }
}