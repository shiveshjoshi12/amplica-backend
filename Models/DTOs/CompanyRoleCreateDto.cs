using System;
using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Models.DTOs
{
    public class CompanyRoleCreateDto
    {
        [Required]
        public int CompanyId { get; set; }

        [Required]
        [StringLength(100)]
        public string RoleName { get; set; } = null!; // This is the actual name of the company-specific role

        public bool? IsActive { get; set; } = true;
        public string? DefaultRole { get; set; } // Changed to string?
        public string? DefaultPermission { get; set; }
        public List<int> PermissionIds { get; set; } = new();
    }

    public class CompanyRoleUpdateDto
    {
        [Required]
        [StringLength(100)]
        public string RoleName { get; set; } = null!; // This is the actual name of the company-specific role

        public bool? IsActive { get; set; }
        public string? DefaultRole { get; set; } // Changed to string?
        public string? DefaultPermission { get; set; }
        public List<int> PermissionIds { get; set; } = new();
    }
}
