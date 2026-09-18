using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Dtos;

public class CreateTaskpriorityDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;
    public string? PriorityColor { get; set; } // Changed from Icon to PriorityColor
    public bool? IsActive { get; set; }
}
