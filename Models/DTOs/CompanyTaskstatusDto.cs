namespace BizfreeApp.Models.DTOs;

public class CompanyTaskstatusDto
{
    // Renamed from StatusId to Id to align with CompanyTaskStatus model,
    // but kept as StatusId for potential frontend compatibility.
    // If frontend strictly expects 'Id', rename this to 'Id'.
    public int Id { get; set; } // Changed from StatusId to Id
    public int CompanyId { get; set; }
    public int? DefaultTaskStatusId { get; set; } // If it originated from a global status
    public string Name { get; set; } = null!;
    public string? StatusColor { get; set; }
    public string Slug { get; set; } = null!;
    public int Order { get; set; }
    public bool IsCustom { get; set; }
    public bool IsEditableName { get; set; }
    public bool IsDeletable { get; set; }
}