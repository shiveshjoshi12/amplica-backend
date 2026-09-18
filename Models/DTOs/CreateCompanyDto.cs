// Simplified request DTO - only username and email for admin user
using BizfreeApp.Models.DTOs;
using BizfreeApp.Models;
using System.ComponentModel.DataAnnotations;

public class CompanyCreateWithAdminDto : CompanyCreateDto
{
    [Required]
    [StringLength(50)]
    public string AdminUserName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string AdminUserEmail { get; set; } = string.Empty;
}

// Simplified response DTO
public class CompanyWithAdminUserDto
{
    public Company Company { get; set; } = null!;
    public AdminUserDetailsDto AdminUser { get; set; } = null!;
}

public class AdminUserDetailsDto
{
    public int UserId { get; set; }
    public int CompanyEmployeeId { get; set; }
    public int RoleId { get; set; }
    public int CompanyId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string EmployeeCode { get; set; } = string.Empty;
    public DateOnly JoiningDate { get; set; }
    public bool IsActive { get; set; }
    public int CheckAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
}
