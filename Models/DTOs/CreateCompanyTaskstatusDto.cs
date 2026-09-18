using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Models.DTOs;

public class CreateCompanyTaskstatusDto
{
    [Required]
    public int CompanyId { get; set; } // New: Company ID is required for creation
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;
    [StringLength(50)]
    public string? StatusColor { get; set; }

    public string? Slug { get; set; }
    // Slug and Order could be automatically generated or provided if needed.
    // For simplicity, we'll generate Slug and set default Order.
}