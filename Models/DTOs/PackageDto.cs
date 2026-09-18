namespace BizfreeApp.Models.DTOs
{
    public class PackageDto
    {
        public int PackageId { get; set; }
        public string PackageName { get; set; } = null!;
        public decimal? PriceMonthly { get; set; }
        public decimal? PriceYearly { get; set; }
        public bool? IsActive { get; set; }
        public string? Description { get; set; }
        public int? TrialDays { get; set; }
    }
}
