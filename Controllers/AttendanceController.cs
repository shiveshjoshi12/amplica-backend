using BizfreeApp.Data;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BizfreeApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceController : ControllerBase
    {
        private readonly BizfreeApp.Data.ApplicationDbContext _context;

        public AttendanceController(BizfreeApp.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        private ActionResult<ApiResponse<T>> Success<T>(string message, T data, int statusCode = StatusCodes.Status200OK) =>
            Ok(new ApiResponse<T>(message, "Success", statusCode, data));

        private ActionResult<ApiResponse<T>> Error<T>(string message, int statusCode, object? errorDetails = null) =>
            StatusCode(statusCode, new ApiResponse<T>(message, "Error", statusCode, default, errorDetails));

        private async Task<int?> GetEmployeeIdFromClaimsAsync()
        {
            try
            {
                var userIdClaim = User.FindFirst("UserId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return null;
                }

                var companyUser = await _context.CompanyUsers
                    .Where(cu => cu.UserId == userId)
                    .FirstOrDefaultAsync();

                return companyUser?.EmployeeId;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        [HttpPost("clock")]
        [Authorize] 
        [ProducesResponseType(typeof(ApiResponse<ClockResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ClockResponseDto>>> Clock([FromBody] ClockRequest request)
        {
            try
            {
                var employeeId = await GetEmployeeIdFromClaimsAsync();
                if (!employeeId.HasValue)
                {
                    return Error<ClockResponseDto>("Employee record not found. Please ensure you are properly registered as an employee.", StatusCodes.Status401Unauthorized);
                }

                var now = DateTime.UtcNow;
                var today = now.Date;

                var punches = await _context.AttendanceRecords
                    .Where(r => r.EmployeeId == employeeId.Value && r.Timestamp.Date == today)
                    .OrderByDescending(r => r.Timestamp)
                    .ToListAsync();

                string punchType = (punches.Count == 0) ? "IN" : (punches.First().PunchType == "IN" ? "OUT" : "IN");

                var attendance = new AttendanceRecord
                {
                    EmployeeId = employeeId.Value,
                    PunchType = punchType,
                    Timestamp = now,
                    LocationLat = request.LocationLat,
                    LocationLng = request.LocationLng,
                    DeviceId = request.DeviceId,
                    Source = request.Source,
                    IpAddress = request.IpAddress,
                    Notes = request.Notes,
                    CreatedAt = now
                };

                _context.AttendanceRecords.Add(attendance);
                await _context.SaveChangesAsync();

                var response = new ClockResponseDto
                {
                    Type = punchType,
                    Timestamp = now,
                    EmployeeId = employeeId.Value 
                };

                return Success("Successfully clocked " + punchType, response);
            }
            catch (Exception ex)
            {
                return Error<ClockResponseDto>("An error occurred while clocking attendance.", StatusCodes.Status500InternalServerError, ex.Message);
            }
        }


        [HttpGet("{employeeId}")]
        [ProducesResponseType(typeof(ApiResponse<AttendanceLogsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AttendanceLogsDto>>> GetAttendanceLogs(int employeeId)
        {
            try
            {
                var punches = await _context.AttendanceRecords
                    .Where(a => a.EmployeeId == employeeId)
                    .OrderBy(a => a.Timestamp)
                    .ToListAsync();

                if (!punches.Any())
                {
                    return Error<AttendanceLogsDto>($"No attendance records found for EmployeeId = {employeeId}", StatusCodes.Status404NotFound);
                }

                // Group punches by date (in UTC)
                var grouped = punches
                    .GroupBy(p => p.Timestamp.Date)
                    .Select(g =>
                    {
                        var dailyPunches = g.ToList();
                        TimeSpan totalTime = TimeSpan.Zero;

                        var punchDtos = dailyPunches.Select(p => new AttendancePunchDto
                        {
                            Id = p.Id,
                            PunchType = p.PunchType,
                            Timestamp = p.Timestamp,
                            LocationLat = p.LocationLat,
                            LocationLng = p.LocationLng,
                            DeviceId = p.DeviceId,
                            Source = p.Source,
                            IpAddress = p.IpAddress,
                            Notes = p.Notes
                        }).ToList();

                        for (int i = 0; i < dailyPunches.Count - 1; i++)
                        {
                            var current = dailyPunches[i];
                            var next = dailyPunches[i + 1];
                            if (current.PunchType == "IN" && next.PunchType == "OUT")
                            {
                                totalTime += (next.Timestamp - current.Timestamp);
                                i++;
                            }
                        }

                        return new AttendanceByDateDto
                        {
                            Date = g.Key,
                            TotalTimeInSeconds = (int)totalTime.TotalSeconds,
                            TotalTimeFormatted = FormatTimeSpan(totalTime),
                            Punches = punchDtos
                        };
                    })
                    .OrderByDescending(x => x.Date)
                    .ToList();

                var last = punches.Last();
                var finalEntry = new AttendancePunchDto
                {
                    Id = last.Id,
                    PunchType = last.PunchType,
                    Timestamp = last.Timestamp,
                    LocationLat = last.LocationLat,
                    LocationLng = last.LocationLng,
                    DeviceId = last.DeviceId,
                    Source = last.Source,
                    IpAddress = last.IpAddress,
                    Notes = last.Notes
                };

                var result = new AttendanceLogsDto
                {
                    EmployeeId = employeeId,
                    AttendanceByDate = grouped,
                    FinalEntry = finalEntry
                };

                return Success("Attendance records retrieved successfully.", result);
            }
            catch (Exception ex)
            {
                return Error<AttendanceLogsDto>("An error occurred while fetching attendance logs.", StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // --- GET /api/attendance/{employeeId}/report ---
        [HttpGet("{employeeId}/report")]
        [ProducesResponseType(typeof(ApiResponse<EmployeeAttendanceReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<EmployeeAttendanceReportDto>>> GetAttendanceReport(
            int employeeId,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                DateTime startDate = fromDate?.Date ?? DateTime.UtcNow.Date.AddMonths(-1);
                DateTime endDate = toDate?.Date ?? DateTime.UtcNow.Date;

                if (startDate > endDate)
                {
                    return Error<EmployeeAttendanceReportDto>("FromDate cannot be later than ToDate", StatusCodes.Status400BadRequest);
                }

                var punches = await _context.AttendanceRecords
                    .Where(p => p.EmployeeId == employeeId && p.Timestamp.Date >= startDate && p.Timestamp.Date <= endDate)
                    .OrderBy(p => p.Timestamp)
                    .ToListAsync();

                if (!punches.Any())
                {
                    return Error<EmployeeAttendanceReportDto>($"No attendance records found for employee {employeeId} in given date range.", StatusCodes.Status404NotFound);
                }

                int totalDays = (endDate - startDate).Days + 1;
                var allDates = Enumerable.Range(0, totalDays).Select(offset => startDate.AddDays(offset)).ToList();

                var groupedByDate = punches.GroupBy(p => p.Timestamp.Date)
                                           .ToDictionary(g => g.Key, g => g.ToList());

                int daysWorked = 0, incompleteDays = 0;
                TimeSpan totalTimeSpent = TimeSpan.Zero;

                foreach (var day in allDates)
                {
                    if (!groupedByDate.ContainsKey(day))
                        continue;

                    var dailyPunches = groupedByDate[day];
                    TimeSpan dailyTotalTime = TimeSpan.Zero;
                    bool incompletePunchFound = false;

                    for (int i = 0; i < dailyPunches.Count; i++)
                    {
                        var current = dailyPunches[i];
                        if (current.PunchType == "IN")
                        {
                            if (i + 1 < dailyPunches.Count && dailyPunches[i + 1].PunchType == "OUT")
                            {
                                dailyTotalTime += (dailyPunches[i + 1].Timestamp - current.Timestamp);
                                i++; // skip paired
                            }
                            else
                            {
                                incompletePunchFound = true;
                            }
                        }
                    }
                    if (dailyTotalTime > TimeSpan.Zero)
                    {
                        daysWorked++;
                        totalTimeSpent += dailyTotalTime;
                    }
                    if (incompletePunchFound)
                        incompleteDays++;
                }

                int absentDays = totalDays - daysWorked;

                var reportDto = new EmployeeAttendanceReportDto
                {
                    EmployeeId = employeeId,
                    FromDate = startDate,
                    ToDate = endDate,
                    TotalDaysInRange = totalDays,
                    DaysWorked = daysWorked,
                    DaysAbsent = absentDays,
                    DaysWithIncompletePunches = incompleteDays,
                    TotalTimeSpent = totalTimeSpent,
                    TotalTimeFormatted = FormatTimeSpan(totalTimeSpent)
                };

                return Success("Attendance report generated successfully.", reportDto);
            }
            catch (Exception ex)
            {
                return Error<EmployeeAttendanceReportDto>("An error occurred while generating attendance report.", StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        private string FormatTimeSpan(TimeSpan ts) =>
            $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
