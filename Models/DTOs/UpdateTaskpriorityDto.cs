using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Dtos;

public class UpdateTaskpriorityDto
{
    [Required]
    public int PriorityId { get; set; }
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;
    public string? PriorityColor { get; set; } // Changed from Icon to PriorityColor
    public bool? IsActive { get; set; }

}