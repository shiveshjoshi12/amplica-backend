using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Models.DTOs;

public class UpdateCompanyTaskstatusDto
{
    [Required]
    public int Id { get; set; } // The ID of the CompanyTaskStatus to update
    [Required]
    public int CompanyId { get; set; } // New: Company ID is required for update context
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;
    [StringLength(50)]
    public string? StatusColor { get; set; }
    // Order, IsEditableName, IsDeletable might also be updated by admin,
    // but for now, keep it simple.
    public int Order { get; set; }
    public string? Slug { get; set; }

}