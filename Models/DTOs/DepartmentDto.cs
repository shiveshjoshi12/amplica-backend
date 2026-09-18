using System.ComponentModel.DataAnnotations;

public class DepartmentDto
{
    public int DeptId { get; set; }
    public int CompanyId { get; set; }
    public string DepartmentName { get; set; }
    public string? Description { get; set; } // NEW
    public int? DepartmentHeadUserId { get; set; } // NEW
    public string? DepartmentHeadName { get; set; } // NEW
    public DateTime? CreatedAt { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public bool? IsDeleted { get; set; }
    public string? CompanyName { get; set; }
    public string? CreatedByName { get; set; }
    public string? UpdatedByName { get; set; }
}

public class DepartmentCreateDto
{
    [Required]
    public string DepartmentName { get; set; }
    public string? Description { get; set; } // NEW
    public int? DepartmentHeadUserId { get; set; } // NEW
    //public int? CompanyId { get; set; }
}

public class DepartmentUpdateDto
{
    [Required]
    public string DepartmentName { get; set; }
    public string? Description { get; set; } // NEW
    public int? DepartmentHeadUserId { get; set; } // NEW
}
