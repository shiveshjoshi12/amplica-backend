namespace BizfreeApp.Models.DTOs.Permissions
{
    public class PermissionDto
    {
        public int PermissionId { get; set; }
        public string PermissionName { get; set; } = null!;
        public int ModuleId { get; set; }
        public string? ModuleName { get; set; } // Convenience for list views
    }

    public class CreatePermissionDto
    {
        // e.g., "task:read", "task:create"
        public string PermissionName { get; set; } = null!;
        public int ModuleId { get; set; }
    }

    public class UpdatePermissionDto
    {
        public string PermissionName { get; set; } = null!;
        public int ModuleId { get; set; }
    }
}
