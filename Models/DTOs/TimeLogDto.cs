// File: BizfreeApp/Models/DTOs/TimelogDto.cs (or suitable DTOs folder)
// Ensure this file has the necessary using statements:
using System;
using System.Collections.Generic; // For List<T> if needed in other DTOs, but not this one directly

namespace BizfreeApp.Models.DTOs // Keep your Timelog related DTOs here
{
    public class TimelogDto
    {
        public string Id { get; set; }
        public string? Duration { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? UserId { get; set; }
        public UserForTimelogDto User { get; set; } // Renamed to be specific to this Timelog response
        public TaskForTimelogDto Task { get; set; } // Renamed to be specific to this Timelog response
        public ProjectForTimelogDto Project { get; set; } // Renamed to be specific to this Timelog response
    }

    public class UserForTimelogDto
    {
        public string UserId { get; set; } // Original User.Id (int) converted to string
        public string Name { get; set; } // Concatenation of FirstName and LastName
        public string? AvatarUrl { get; set; } // Assuming your User entity has this property
    }

    public class TaskForTimelogDto
    {
        public string TaskId { get; set; } // Original Task.Id (int) converted to string
        public string Name { get; set; } // Maps to Task.Title
    }

    public class ProjectForTimelogDto
    {
        public string ProjectId { get; set; } // Original Project.Id (int) converted to string
        public string Name { get; set; } // Maps to Project.Name
    }
    public class TaskTimelogDto
    {
        public int TimelogId { get; set; }
        public int TaskId { get; set; }
        public int UserId { get; set; }
        public DateTime LoggedAt { get; set; }
        public string? Duration { get; set; }            // String duration (HH:MM format)
        public decimal HoursLogged => ParseDurationToDecimal(Duration); // Calculated property
        public string? Description { get; set; }

        // Task-related properties for filtering and display
        public string? TaskTitle { get; set; }
        public string? StatusName { get; set; }
        public string? PriorityName { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public DateOnly? TaskStartDate { get; set; }
        public DateOnly? TaskEndDate { get; set; }
        public int? AssignedToUserId { get; set; }
        public string? AssignedToUserName { get; set; }
        public string? LoggedByUserName { get; set; }

        // Helper method to convert duration string to decimal hours
        internal static decimal ParseDurationToDecimal(string? duration)
        {
            if (string.IsNullOrEmpty(duration)) return 0;

            var parts = duration.Split(':');
            if (parts.Length != 2) return 0;

            if (int.TryParse(parts[0], out int hours) && int.TryParse(parts[1], out int minutes))
            {
                return hours + (minutes / 60m);
            }
            return 0;
        }
    }

    }