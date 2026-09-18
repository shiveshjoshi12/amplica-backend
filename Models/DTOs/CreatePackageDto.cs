namespace BizfreeApp.Models.DTOs
{
    public class CreatePackageDto
    {
        public string PackageName { get; set; }
        public decimal? PriceMonthly { get; set; }
        public decimal? PriceYearly { get; set; }
        public bool? IsActive { get; set; }
        public string? Description { get; set; }
        public int? TrialDays { get; set; }
    }
}
