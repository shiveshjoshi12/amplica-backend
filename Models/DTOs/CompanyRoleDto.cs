using System;

namespace BizfreeApp.Models.DTOs
{
    /// <summary>
    /// Data Transfer Object for CompanyRole, providing a simplified view for API responses.
    /// </summary>
    public class CompanyRoleDto
    {
        public int CompanyRoleId { get; set; }
        public int CompanyId { get; set; }
        public string RoleName { get; set; } = null!;
        public bool? IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
        public string? DefaultRole { get; set; }
        public string? DefaultPermission { get; set; }

        // Optional: Include basic company info if needed
        public string? CompanyName { get; set; }

        // Optional: Include creator/updater names if needed
        public string? CreatedByName { get; set; }
        public string? UpdatedByName { get; set; }
    }
}
