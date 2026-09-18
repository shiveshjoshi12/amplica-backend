namespace BizfreeApp.Models.DTOs
{
    public class ClockRequest
    {
        //public int EmployeeId { get; set; }
        public decimal? LocationLat { get; set; }
        public decimal? LocationLng { get; set; }
        public string DeviceId { get; set; }
        public string Source { get; set; }
        public string IpAddress { get; set; }
        public string Notes { get; set; }
    }
    public class AttendanceQueryParams
    {
        public DateTime? FromDate { get; set; }  
        public DateTime? ToDate { get; set; } 
        public string SortBy { get; set; } = "Date"; 
        public string SortOrder { get; set; } = "desc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 5;
    }
    public class EmployeeAttendanceReportDto
    {
        public int EmployeeId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int TotalDaysInRange { get; set; }
        public int DaysWorked { get; set; }
        public int DaysAbsent { get; set; }
        public int DaysWithIncompletePunches { get; set; }
        public TimeSpan TotalTimeSpent { get; set; }
        public string TotalTimeFormatted { get; set; } = string.Empty;
    }
    public class ClockResponseDto
    {
        public string Type { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int EmployeeId { get; set; }
    }

    public class AttendancePunchDto
    {
        public int Id { get; set; }
        public string PunchType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public decimal? LocationLat { get; set; }
        public decimal? LocationLng { get; set; }
        public string? DeviceId { get; set; }
        public string? Source { get; set; }
        public string? IpAddress { get; set; }
        public string? Notes { get; set; }
    }

    public class AttendanceByDateDto
    {
        public DateTime Date { get; set; }
        public int TotalTimeInSeconds { get; set; }
        public string TotalTimeFormatted { get; set; } = string.Empty;
        public List<AttendancePunchDto> Punches { get; set; } = new();
    }

    public class AttendanceLogsDto
    {
        public int EmployeeId { get; set; }
        public List<AttendanceByDateDto> AttendanceByDate { get; set; } = new();
        public AttendancePunchDto? FinalEntry { get; set; }
    }

}
