using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BizfreeApp.Models
{
    public class AttendanceRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Employee))]
        public int EmployeeId { get; set; }

        [Required]
        [MaxLength(3)]
        public string PunchType { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        [Column(TypeName = "decimal(10, 7)")]
        public decimal? LocationLat { get; set; }

        [Column(TypeName = "decimal(10, 7)")]
        public decimal? LocationLng { get; set; }

        [MaxLength(100)]
        public string DeviceId { get; set; }

        [MaxLength(50)]
        public string Source { get; set; }

        [MaxLength(45)] // IPv6 compatible
        public string IpAddress { get; set; }

        public string Notes { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [InverseProperty(nameof(CompanyUser.AttendanceRecords))]
        public virtual CompanyUser Employee { get; set; }
    }
}
