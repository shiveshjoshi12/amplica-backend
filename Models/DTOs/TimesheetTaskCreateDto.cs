namespace BizfreeApp.Models.DTOs
{

    public class TimesheetTaskCreateDto
    {
        public string TaskName { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public decimal? DailyLog { get; set; }
        public int? AssignedTo { get; set; }
        //public string? Message { get; set; }
    }
}
