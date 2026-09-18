namespace BizfreeApp.Models.DTOs.Modules
{
    public class ModuleDto
    {
        public int ModuleId { get; set; }
        public string? ModuleName { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
    }

    public class CreateModuleDto
    {
        public string ModuleName { get; set; } = null!;
        public string? Description { get; set; }
        public bool? IsActive { get; set; } = true;
    }

    public class UpdateModuleDto
    {
        public string ModuleName { get; set; } = null!;
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
    }
}
