using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Models; // For your EF Core entities like TaskTimelog, Task, User, Project, etc.
using BizfreeApp.Models.DTOs; // For TaskDto, TimelogDto, ProjectDetailInTimelogDto
using BizfreeApp.DTOs; // For CompanyUserDetailsDto
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization; // Add this for [Authorize] attribute
using System.Security.Claims; // Needed if you add claim-based helper methods
using BizfreeApp.Constants; // Add this to access your Permissions constants
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize] // Apply this at the controller level to ensure all actions require authentication by default
public class TimelogsController : ControllerBase
{
    private readonly Data.ApplicationDbContext _context;
    private readonly ILogger<TimelogsController> _logger; // Inject ILogger


    public TimelogsController(Data.ApplicationDbContext context, ILogger<TimelogsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    protected ActionResult<ApiResponse<T>> Success<T>(string message, T? data = default, int statusCode = StatusCodes.Status200OK)
    {
        return Ok(new ApiResponse<T>(message, "Success", statusCode, data));
    }

    protected ActionResult<ApiResponse<T>> Error<T>(string message, int statusCode = StatusCodes.Status400BadRequest, object? errorDetails = null)
    {
        var apiResponse = new ApiResponse<T>(message, "Error", statusCode, default, errorDetails);

        if (statusCode == StatusCodes.Status400BadRequest)
        {
            return BadRequest(apiResponse);
        }
        else if (statusCode == StatusCodes.Status404NotFound)
        {
            return NotFound(apiResponse);
        }
        else
        {
            return StatusCode(statusCode, apiResponse);
        }
    }

    // Helper method for sorting expressions
    private Expression<Func<TaskTimelogDto, object>> GetTimelogSortExpression(string sortBy)
    {
        return sortBy.ToLower() switch
        {
            "loggedat" => tl => tl.LoggedAt,
            "duration" => tl => tl.Duration ?? "",
            "hourslogged" => tl => tl.HoursLogged,
            "tasktitle" => tl => tl.TaskTitle ?? "",
            "projectname" => tl => tl.ProjectName ?? "",
            "statusname" => tl => tl.StatusName ?? "",
            "priorityname" => tl => tl.PriorityName ?? "",
            "assignedtousername" => tl => tl.AssignedToUserName ?? "",
            "loggedbyusername" => tl => tl.LoggedByUserName ?? "",
            "description" => tl => tl.Description ?? "",
            _ => tl => tl.LoggedAt
        };
    }

    protected IActionResult Error(string message, int statusCode = StatusCodes.Status400BadRequest, object? errorDetails = null)
    {
        var apiResponse = new ApiResponse<object>(message, "Error", statusCode, default, errorDetails);

        if (statusCode == StatusCodes.Status400BadRequest)
        {
            return BadRequest(apiResponse);
        }
        else
        {
            return StatusCode(statusCode, apiResponse);
        }
    }


    // Helper methods to get claims
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedUserId))
        {
            return parsedUserId;
        }
        return null;
    }

    private int? GetCurrentUserCompanyId()
    {
        var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
        if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out int parsedCompanyId))
        {
            return parsedCompanyId;
        }
        return null;
    }


    // Helper method to convert HH:MM string to TimeSpan
    private TimeSpan ParseDuration(string duration)
    {
        if (string.IsNullOrEmpty(duration)) return TimeSpan.Zero;

        var parts = duration.Split(':');
        if (parts.Length != 2) return TimeSpan.Zero;

        if (int.TryParse(parts[0], out int hours) && int.TryParse(parts[1], out int minutes))
        {
            return new TimeSpan(hours, minutes, 0);
        }
        return TimeSpan.Zero;
    }

    // Helper method to format TimeSpan to HH:MM string
    private string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalHours:D2}:{duration.Minutes:D2}";
    }

    // This GET API returns all TaskTimelogs, consider if you still need it or if GetFilteredTimelogs suffices
    // Usually, this would be highly restricted or removed in a production environment.
    [HttpGet]
    [Authorize(Policy = "CanTimelogRead")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TaskTimelogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<PagedResult<TaskTimelogDto>>>> GetTaskTimelogs(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string sortBy = "loggedAt",
    [FromQuery] string sortOrder = "desc",
    [FromQuery] string? search = null,
    [FromQuery] string? statusNames = null,
    [FromQuery] int? priorityId = null,
    [FromQuery] DateOnly? startDateFrom = null,
    [FromQuery] DateOnly? startDateTo = null,
    [FromQuery] DateOnly? endDateFrom = null,
    [FromQuery] DateOnly? endDateTo = null,
    [FromQuery] int? memberUserId = null,
    [FromQuery] DateTime? loggedAtFrom = null,
    [FromQuery] DateTime? loggedAtTo = null,
    [FromQuery] int? projectId = null,
    [FromQuery] int? taskId = null,
    [FromQuery] string? durationFrom = null,
    [FromQuery] string? durationTo = null)
    {
        try
        {
            var companyId = GetCurrentUserCompanyId();
            if (!companyId.HasValue)
                return Error<PagedResult<TaskTimelogDto>>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);

            var currentUserId = GetCurrentUserId();
            bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);
            bool canReadAllTimelogs = User.HasClaim("permission", Permissions.TimelogReadAll);

            // Build base timelog query with necessary includes
            IQueryable<TaskTimelog> timelogQuery = _context.TaskTimelogs
                .Include(tl => tl.Task)
                    .ThenInclude(t => t.StatusNavigation)
                .Include(tl => tl.Task)
                    .ThenInclude(t => t.Priority)
                .Include(tl => tl.Task)
                    .ThenInclude(t => t.Project)
                .Include(tl => tl.Task)
                    .ThenInclude(t => t.Company)
                .Include(tl => tl.Task)
                    .ThenInclude(t => t.AssignedToNavigation)
                        .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == (isGlobalAdmin ? cu.CompanyId : companyId.Value)))
                .Include(tl => tl.User)
                    .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == (isGlobalAdmin ? cu.CompanyId : companyId.Value)));

            // Apply company isolation unless global admin
            if (!isGlobalAdmin)
            {
                timelogQuery = timelogQuery.Where(tl => tl.Task != null && tl.Task.CompanyId == companyId.Value);
            }

            // Data filtering logic based on permissions
            if (isGlobalAdmin || canReadAllTimelogs)
            {
                _logger.LogInformation($"Admin (User ID: {currentUserId}) is fetching all accessible timelogs.");

                if (memberUserId.HasValue)
                {
                    timelogQuery = timelogQuery.Where(tl => tl.UserId == memberUserId.Value);

                    if (!isGlobalAdmin)
                    {
                        var memberExists = await _context.Users
                            .Include(u => u.CompanyUserUsers)
                            .AnyAsync(u => u.UserId == memberUserId.Value &&
                                          u.CompanyUserUsers.Any(cu => cu.CompanyId == companyId.Value));

                        if (!memberExists)
                        {
                            return Error<PagedResult<TaskTimelogDto>>("Invalid member user ID or user not in your company.", StatusCodes.Status400BadRequest);
                        }
                    }
                }
            }
            else 
            {
                // Check if user manages any department
                var managedDepartmentIds = await _context.CompanyUsers
                    .Where(cu => cu.UserId == currentUserId && cu.ManagedDepartmentId.HasValue && !cu.IsDeleted.GetValueOrDefault())
                    .Select(cu => cu.ManagedDepartmentId)
                    .ToListAsync();

                if (managedDepartmentIds.Any())
                {
                    _logger.LogInformation($"Manager (User ID: {currentUserId}) is fetching timelogs for their department(s).");

                    var departmentUserIds = await _context.CompanyUsers
                        .Where(cu => cu.CompanyId == companyId.Value &&
                                    cu.DepartmentId.HasValue &&
                                    managedDepartmentIds.Contains(cu.DepartmentId) &&
                                    !cu.IsDeleted.GetValueOrDefault())
                        .Select(cu => cu.UserId)
                        .ToListAsync();

                    if (memberUserId.HasValue)
                    {
                        if (!departmentUserIds.Contains(memberUserId.Value) && memberUserId.Value != currentUserId)
                        {
                            return Error<PagedResult<TaskTimelogDto>>("You can only view timelogs of users in your department or your own.", StatusCodes.Status403Forbidden);
                        }
                        timelogQuery = timelogQuery.Where(tl => tl.UserId == memberUserId.Value);
                    }
                    else
                    {
                        // See department users + self
                        timelogQuery = timelogQuery.Where(tl => tl.UserId.HasValue && (departmentUserIds.Contains(tl.UserId.Value) || tl.UserId == currentUserId));
                    }
                }
                else
                {
                    _logger.LogInformation($"User (User ID: {currentUserId}) is fetching their own timelogs.");

                    timelogQuery = timelogQuery.Where(tl => tl.UserId == currentUserId.Value);

                    if (memberUserId.HasValue && memberUserId.Value != currentUserId.Value)
                    {
                        _logger.LogWarning($"User (ID: {currentUserId}) attempted to access timelogs of user {memberUserId.Value}. Access denied.");
                        return Error<PagedResult<TaskTimelogDto>>("You can only view your own timelogs.", StatusCodes.Status403Forbidden);
                    }
                }
            }

            // --- REFACTOR: Database-side filtering, sorting, and pagination ---

            // Apply filters to IQueryable
            if (!string.IsNullOrWhiteSpace(search))
            {
                string lowerSearch = search.ToLower();
                timelogQuery = timelogQuery.Where(tl =>
                    (tl.Task.Title != null && tl.Task.Title.ToLower().Contains(lowerSearch)) ||
                    (tl.Description != null && tl.Description.ToLower().Contains(lowerSearch)) ||
                    (tl.Task.Project != null && tl.Task.Project.Name != null && tl.Task.Project.Name.ToLower().Contains(lowerSearch))
                );
            }

            if (!string.IsNullOrWhiteSpace(statusNames))
            {
                var statusList = statusNames.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim().ToLower()).ToList();
                timelogQuery = timelogQuery.Where(tl => tl.Task.StatusNavigation != null && statusList.Contains(tl.Task.StatusNavigation.Name.ToLower()));
            }
            else
            {
                // Default filter: hide completed tasks unless specified
                timelogQuery = timelogQuery.Where(tl => tl.Task.StatusNavigation == null || tl.Task.StatusNavigation.Name != "Completed");
            }

            if (priorityId.HasValue)
            {
                timelogQuery = timelogQuery.Where(tl => tl.Task.PriorityId == priorityId.Value);
            }

            if (startDateFrom.HasValue)
                timelogQuery = timelogQuery.Where(tl => tl.Task.StartDate.HasValue && tl.Task.StartDate.Value >= startDateFrom.Value);

            if (startDateTo.HasValue)
                timelogQuery = timelogQuery.Where(tl => tl.Task.StartDate.HasValue && tl.Task.StartDate.Value <= startDateTo.Value);

            if (endDateFrom.HasValue)
                timelogQuery = timelogQuery.Where(tl => tl.Task.EndDate.HasValue && tl.Task.EndDate.Value >= endDateFrom.Value);

            if (endDateTo.HasValue)
                timelogQuery = timelogQuery.Where(tl => tl.Task.EndDate.HasValue && tl.Task.EndDate.Value <= endDateTo.Value);

            if (loggedAtFrom.HasValue)
                timelogQuery = timelogQuery.Where(tl => tl.LoggedAt >= loggedAtFrom.Value);

            if (loggedAtTo.HasValue)
                timelogQuery = timelogQuery.Where(tl => tl.LoggedAt <= loggedAtTo.Value);

            if (projectId.HasValue)
                timelogQuery = timelogQuery.Where(tl => tl.Task.ProjectId == projectId.Value);

            if (taskId.HasValue)
                timelogQuery = timelogQuery.Where(tl => tl.TaskId == taskId.Value);

            // Note: Duration filtering (HH:MM) is kept simple (lexicographical) or handled if needed.
            // For now, we prioritize the core performance fix of pagination.

            // Sorting (Database side)
            timelogQuery = sortBy.ToLower() switch
            {
                "loggedat" => sortOrder.ToLower() == "desc" ? timelogQuery.OrderByDescending(tl => tl.LoggedAt) : timelogQuery.OrderBy(tl => tl.LoggedAt),
                "tasktitle" => sortOrder.ToLower() == "desc" ? timelogQuery.OrderByDescending(tl => tl.Task.Title) : timelogQuery.OrderBy(tl => tl.Task.Title),
                "projectname" => sortOrder.ToLower() == "desc" ? timelogQuery.OrderByDescending(tl => tl.Task.Project.Name) : timelogQuery.OrderBy(tl => tl.Task.Project.Name),
                "duration" => sortOrder.ToLower() == "desc" ? timelogQuery.OrderByDescending(tl => tl.Duration) : timelogQuery.OrderBy(tl => tl.Duration),
                "statusname" => sortOrder.ToLower() == "desc" ? timelogQuery.OrderByDescending(tl => tl.Task.StatusNavigation.Name) : timelogQuery.OrderBy(tl => tl.Task.StatusNavigation.Name),
                _ => sortOrder.ToLower() == "desc" ? timelogQuery.OrderByDescending(tl => tl.LoggedAt) : timelogQuery.OrderBy(tl => tl.LoggedAt)
            };

            var totalCount = await timelogQuery.CountAsync();

            var items = await timelogQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(tl => new TaskTimelogDto
                {
                    TimelogId = tl.Id,
                    TaskId = tl.TaskId ?? 0,
                    UserId = tl.UserId ?? 0,
                    LoggedAt = tl.LoggedAt,
                    Duration = tl.Duration,
                    Description = tl.Description,
                    TaskTitle = tl.Task.Title,
                    StatusName = tl.Task.StatusNavigation.Name,
                    PriorityName = tl.Task.Priority.Name,
                    ProjectId = tl.Task.ProjectId,
                    ProjectName = tl.Task.Project.Name,
                    TaskStartDate = tl.Task.StartDate,
                    TaskEndDate = tl.Task.EndDate,
                    AssignedToUserId = tl.Task.AssignedTo,
                    AssignedToUserName = tl.Task.AssignedToNavigation != null && tl.Task.AssignedToNavigation.CompanyUserUsers.Any(cu => cu.CompanyId == companyId.Value)
                        ? tl.Task.AssignedToNavigation.CompanyUserUsers.Where(cu => cu.CompanyId == companyId.Value).Select(cu => cu.FirstName + " " + cu.LastName).FirstOrDefault()
                        : null,
                    LoggedByUserName = tl.User != null && tl.User.CompanyUserUsers.Any(cu => cu.CompanyId == companyId.Value)
                        ? tl.User.CompanyUserUsers.Where(cu => cu.CompanyId == companyId.Value).Select(cu => cu.FirstName + " " + cu.LastName).FirstOrDefault()
                        : null
                })
                .ToListAsync();

            var pagedResult = new PagedResult<TaskTimelogDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                TotalTasks = totalCount
            };

            return Success("Timelogs retrieved successfully.", pagedResult);
        }
        catch (Exception ex)
        {
            return Error<PagedResult<TaskTimelogDto>>("An error occurred while retrieving timelogs.", StatusCodes.Status500InternalServerError);
        }
    }


    // This GET API returns a single TaskTimelog by ID, adjust if it also needs to return the DTO
    [HttpGet("{id}")]
    [Authorize(Policy = "CanTimelogRead")] // Requires 'timelog:read' permission
    [ProducesResponseType(typeof(ApiResponse<TimelogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)] // Added for consistency
    public async Task<ActionResult<ApiResponse<TimelogDto>>> GetTaskTimelog(int id)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserCompanyId = GetCurrentUserCompanyId();
        if (!currentUserId.HasValue || !currentUserCompanyId.HasValue)
        {
            return Error<TimelogDto>("Authentication information is missing.", StatusCodes.Status401Unauthorized);
        }

        var timelogDto = await _context.TaskTimelogs
            .Include(tt => tt.Task)
                .ThenInclude(t => t.Project)
            .Include(tt => tt.User)
                .ThenInclude(u => u.CompanyUserUsers)
            .Where(tt => tt.Id == id)
            .Select(tt => new TimelogDto
            {
                Id = tt.Id.ToString(),
                Duration = tt.Duration,
                Description = tt.Description,
                CreatedAt = tt.LoggedAt,
                UserId = tt.UserId, // Capture original UserId from timelog for comparison
                User = new UserForTimelogDto
                {
                    UserId = tt.User.UserId.ToString(),
                    Name = tt.User.CompanyUserUsers.FirstOrDefault() != null ?
                               $"{tt.User.CompanyUserUsers.FirstOrDefault()!.FirstName} {tt.User.CompanyUserUsers.FirstOrDefault()!.LastName}" : null,
                    AvatarUrl = tt.User.CompanyUserUsers.FirstOrDefault() != null ?
                                 tt.User.CompanyUserUsers.FirstOrDefault()!.ProfilePhotoUrl : null
                },
                Task = new TaskForTimelogDto
                {
                    TaskId = tt.Task.TaskId.ToString(),
                    Name = tt.Task.Title
                },
                Project = new ProjectForTimelogDto
                {
                    ProjectId = tt.Task.Project != null ? tt.Task.Project.ProjectId.ToString() : null,
                    Name = tt.Task.Project != null ? tt.Task.Project.Name : null
                }
            })
            .FirstOrDefaultAsync();

        if (timelogDto == null)
        {
            return Error<TimelogDto>("Timelog not found.", StatusCodes.Status404NotFound);
        }

        // Data-level authorization for single timelog
        bool canAccess = false;
        bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);
        bool canReadAllTimelogs = User.HasClaim("permission", Permissions.TimelogReadAll);

        if (isGlobalAdmin)
        {
            canAccess = true; // Super Admin can see any timelog
        }
        else if (canReadAllTimelogs)
        {
            // Admin can see timelogs only within their company (we need the companyId from the log's user/task)
            // The mapping here would require checking the task's company or the user's company records.
            // Since we know the user's company is currentUserCompanyId, we check if the log belongs to a task in that company.
            
            var logDetails = await _context.TaskTimelogs
                .Include(tt => tt.Task)
                .Where(tt => tt.Id == id)
                .Select(tt => new { tt.Task.CompanyId })
                .FirstOrDefaultAsync();

            canAccess = logDetails != null && logDetails.CompanyId == currentUserCompanyId.Value;
        }
        else // Regular user/employee
        {
            // Employee can only see their own timelogs
            canAccess = timelogDto.UserId == currentUserId.Value;
        }

        if (!canAccess)
        {
            return Error<TimelogDto>("You do not have permission to view this timelog.", StatusCodes.Status403Forbidden);
        }

        return Success("Timelog retrieved successfully.", timelogDto);
    }

    // *** THE PRIMARY GET API FOR FILTERED TIMELOGS WITH DTOs AND AGGREGATION ***
    [HttpGet("filtered")]
    [Authorize(Policy = "CanTimelogRead")] // Requires 'timelog:read' permission
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TimelogDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<PagedResult<TimelogDto>>>> GetFilteredTimelogs(
        [FromQuery] int? userId, // Can be used by admins to filter by specific user
        [FromQuery] int? projectId,
        [FromQuery] int? taskId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? sortBy = "loggedat",
        [FromQuery] string? sortOrder = "desc",
        [FromQuery] string? search = null,
        [FromQuery] int? statusId = null,
        [FromQuery] int? priorityId = null,
        [FromQuery] int? assignedToUserId = null,
        [FromQuery] DateOnly? dueDateFrom = null,
        [FromQuery] DateOnly? dueDateTo = null,
        [FromQuery] DateOnly? loggedAtFrom = null, // Filter for logged date range
        [FromQuery] DateOnly? loggedAtTo = null) // Filter for logged date range
    {
        var currentUserId = GetCurrentUserId();
        var currentUserCompanyId = GetCurrentUserCompanyId();
        bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);
        bool canReadAllTimelogs = User.HasClaim("permission", Permissions.TimelogReadAll);
        
        // Re-calculate manager status for department head logic
        var managedDepartmentIds = await _context.CompanyUsers
            .Where(cu => cu.UserId == currentUserId && cu.ManagedDepartmentId.HasValue && cu.IsDeleted != true)
            .Select(cu => cu.ManagedDepartmentId)
            .ToListAsync();
        bool isDepartmentManager = managedDepartmentIds.Any();

        IQueryable<TaskTimelog> query = _context.TaskTimelogs
            .Include(tt => tt.Task)
                .ThenInclude(t => t.Project)
            .Include(tt => tt.Task)
                .ThenInclude(t => t.StatusNavigation) // For statusId filter
            .Include(tt => tt.Task)
                .ThenInclude(t => t.Priority) // For priorityId filter
            .Include(tt => tt.User)
                .ThenInclude(u => u.CompanyUserUsers);

        // Apply company-level filtering based on permissions
        if (isGlobalAdmin)
        {
            // Super Admin can view timelogs across all companies
            // No company restriction applied
        }
        else // Company Admin, Department Head, Employee
        {
            query = query.Where(tt => tt.Task != null && tt.Task.CompanyId == currentUserCompanyId.Value);

            // Restrict `userId` filter for non-admins
            if (userId.HasValue && userId.Value != currentUserId.Value && !canReadAllTimelogs && !isDepartmentManager)
            {
                // Employees cannot query other users' timelogs
                return Error<PagedResult<TimelogDto>>("You do not have permission to query other users' timelogs.", StatusCodes.Status403Forbidden);
            }

            if (!canReadAllTimelogs && !isDepartmentManager)
            {
                // Employees only see their own timelogs, override any userId filter
                query = query.Where(tt => tt.UserId == currentUserId.Value);
            }
            else if (isDepartmentManager && !canReadAllTimelogs)
            {
                // Department heads can see themselves + their department
                var departmentUserIds = await _context.CompanyUsers
                    .Where(cu => cu.CompanyId == currentUserCompanyId.Value &&
                                cu.DepartmentId.HasValue &&
                                managedDepartmentIds.Contains(cu.DepartmentId) &&
                                !cu.IsDeleted.GetValueOrDefault())
                    .Select(cu => cu.UserId)
                    .ToListAsync();
                
                query = query.Where(tt => tt.UserId == currentUserId.Value || (tt.UserId.HasValue && departmentUserIds.Contains(tt.UserId.Value)));
            }
        }

        if (projectId.HasValue)
        {
            query = query.Where(tt => tt.Task != null && tt.Task.ProjectId == projectId.Value);
        }

        if (taskId.HasValue)
        {
            query = query.Where(tt => tt.TaskId == taskId.Value);
        }

        // Apply logged date range filters (FIXED - uncommented and working)
        if (loggedAtFrom.HasValue)
        {
            query = query.Where(tt => tt.LoggedAt.Date >= loggedAtFrom.Value.ToDateTime(TimeOnly.MinValue));
        }

        if (loggedAtTo.HasValue)
        {
            query = query.Where(tt => tt.LoggedAt.Date <= loggedAtTo.Value.ToDateTime(TimeOnly.MaxValue));
        }

        // Default date range for last 30 days if no date filters are provided
        if (!loggedAtFrom.HasValue && !loggedAtTo.HasValue)
        {
            DateTime currentLocalTime = DateTime.Now;
            DateTime defaultEndDate = currentLocalTime.Date.AddDays(1).AddTicks(-1);
            DateTime defaultStartDate = currentLocalTime.Date.AddDays(-30);
            query = query.Where(tt => tt.LoggedAt.Date >= defaultStartDate && tt.LoggedAt.Date <= defaultEndDate);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string lowerCaseSearchKeyword = search.ToLower();
            query = query.Where(tt =>
                (tt.Description != null && tt.Description.ToLower().Contains(lowerCaseSearchKeyword)) ||
                (tt.Task != null && tt.Task.Title != null && tt.Task.Title.ToLower().Contains(lowerCaseSearchKeyword)) ||
                (tt.Task != null && tt.Task.Description != null && tt.Task.Description.ToLower().Contains(lowerCaseSearchKeyword))
            );
        }

        if (statusId.HasValue)
        {
            query = query.Where(tt => tt.Task != null && tt.Task.Status == statusId.Value);
        }

        if (priorityId.HasValue)
        {
            query = query.Where(tt => tt.Task != null && tt.Task.PriorityId == priorityId.Value);
        }

        if (assignedToUserId.HasValue)
        {
            query = query.Where(tt => tt.Task != null && tt.Task.AssignedTo == assignedToUserId.Value);
        }

        if (dueDateFrom.HasValue)
        {
            query = query.Where(tt => tt.Task != null && tt.Task.DueDate.HasValue && tt.Task.DueDate.Value >= dueDateFrom.Value);
        }

        if (dueDateTo.HasValue)
        {
            query = query.Where(tt => tt.Task != null && tt.Task.DueDate.HasValue && tt.Task.DueDate.Value <= dueDateTo.Value);
        }

        // --- REFACTOR: Database-side grouping and pagination ---
        
        // 1. Group by TaskId and project necessary fields for sorting at the database level
        var groupedQuery = query
            .GroupBy(tt => tt.TaskId)
            .Select(g => new
            {
                TaskId = g.Key,
                LatestLoggedAt = g.Max(tt => tt.LoggedAt),
                TaskTitle = g.Select(tt => tt.Task.Title).FirstOrDefault(),
                // Duration sum is difficult in SQL, so we handle sorting by duration if possible
                // otherwise we fall back to LatestLoggedAt for consistency.
            });

        // 2. Sorting (Database side)
        IQueryable<dynamic> sortedQuery = groupedQuery;
        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            switch (sortBy.ToLower())
            {
                case "loggedat":
                    sortedQuery = (sortOrder?.ToLower() == "desc")
                        ? groupedQuery.OrderByDescending(x => x.LatestLoggedAt)
                        : groupedQuery.OrderBy(x => x.LatestLoggedAt);
                    break;
                case "tasktitle":
                    sortedQuery = (sortOrder?.ToLower() == "desc")
                        ? groupedQuery.OrderByDescending(x => x.TaskTitle)
                        : groupedQuery.OrderBy(x => x.TaskTitle);
                    break;
                default:
                    sortedQuery = groupedQuery.OrderByDescending(x => x.LatestLoggedAt);
                    break;
            }
        }
        else
        {
            sortedQuery = groupedQuery.OrderByDescending(x => x.LatestLoggedAt);
        }

        var totalCount = await groupedQuery.CountAsync();
        var pagedInfo = await sortedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var pagedIds = pagedInfo.Select(x => (int?)x.TaskId).ToList();

        // 3. Hydrate the paged results with full details and calculate aggregated durations
        // We only fetch timelogs for the tasks that are actually on the current page.
        var pagedTimelogs = await query
            .Where(tt => pagedIds.Contains(tt.TaskId))
            .ToListAsync();

        var aggregatedItems = pagedTimelogs
            .GroupBy(tt => tt.TaskId)
            .Select(group => {
                var latest = group.OrderByDescending(tt => tt.LoggedAt).First();
                var totalDuration = group
                    .Select(tt => ParseDuration(tt.Duration))
                    .Aggregate(TimeSpan.Zero, (acc, duration) => acc.Add(duration));
                
                return new TimelogDto
                {
                    Id = latest.Id.ToString(),
                    Duration = FormatDuration(totalDuration),
                    Description = string.Join("; ", group.Where(tt => !string.IsNullOrEmpty(tt.Description))
                                    .Select(tt => tt.Description).Distinct()),
                    CreatedAt = latest.LoggedAt,
                    User = latest.User != null ? new UserForTimelogDto
                    {
                        UserId = latest.User.UserId.ToString(),
                        Name = latest.User.CompanyUserUsers.FirstOrDefault() != null ?
                                   $"{latest.User.CompanyUserUsers.FirstOrDefault()!.FirstName} {latest.User.CompanyUserUsers.FirstOrDefault()!.LastName}" : null,
                        AvatarUrl = latest.User.CompanyUserUsers.FirstOrDefault()?.ProfilePhotoUrl
                    } : null,
                    Task = latest.Task != null ? new TaskForTimelogDto
                    {
                        TaskId = latest.Task.TaskId.ToString(),
                        Name = latest.Task.Title
                    } : null,
                    Project = latest.Task?.Project != null ? new ProjectForTimelogDto
                    {
                        ProjectId = latest.Task.Project.ProjectId.ToString(),
                        Name = latest.Task.Project.Name
                    } : null
                };
            })
            .ToList();

        // Re-order aggregatedItems to match the database sort order
        var items = pagedIds
            .Select(id => aggregatedItems.FirstOrDefault(i => i.Task.TaskId == id.ToString()))
            .Where(i => i != null)
            .ToList();

        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var pagedResult = new PagedResult<TimelogDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalTasks = totalCount,
            SortBy = sortBy,
            SortOrder = sortOrder,
            SearchKeyword = search
        };

        return Success("Aggregated timelogs retrieved successfully.", pagedResult);
    }

    [HttpPost]
    [Authorize(Policy = "CanTimelogCreate")]
    [ProducesResponseType(typeof(ApiResponse<TaskTimelog>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TaskTimelog>>> PostTaskTimelog(TaskTimelog taskTimelog)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserCompanyId = GetCurrentUserCompanyId();
        bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);
        bool canCreateAnyTimelog = User.HasClaim("permission", Permissions.TimelogReadAll); // Reuse ReadAll or create a WriteAll

        var task = await _context.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.TaskId == taskTimelog.TaskId && !t.IsDeleted);
        if (task == null)
        {
            return Error<TaskTimelog>("Associated task not found.", StatusCodes.Status404NotFound);
        }

        bool canCreateTimelog = false;
        if (isGlobalAdmin)
        {
            canCreateTimelog = true;
        }
        else if (task.CompanyId == currentUserCompanyId.Value)
        {
            if (canCreateAnyTimelog)
            {
                canCreateTimelog = true;
            }
            else
            {
                // Regular employees can log time for tasks assigned to them
                if (task.AssignedTo == currentUserId.Value)
                {
                    canCreateTimelog = true;
                }
            }
        }

        if (!canCreateTimelog)
        {
            return Error<TaskTimelog>("You do not have permission to add timelogs for this task.", StatusCodes.Status403Forbidden);
        }

        if (!isGlobalAdmin && !canCreateAnyTimelog && taskTimelog.UserId != currentUserId.Value)
        {
            return Error<TaskTimelog>("You can only log time for yourself.", StatusCodes.Status403Forbidden);
        }


        if (!System.Text.RegularExpressions.Regex.IsMatch(taskTimelog.Duration ?? "", @"^(?:2[0-3]|[01]?[0-9]):[0-5][0-9]$"))
        {
            return Error<TaskTimelog>("Duration must be in HH:MM format (e.g., '08:30').", StatusCodes.Status400BadRequest);
        }

        taskTimelog.LoggedAt = DateTime.Now;

        var existingEntries = await _context.TaskTimelogs
            .Where(t => t.TaskId == taskTimelog.TaskId)
            .ToListAsync();

        if (existingEntries.Any())
        {
            var totalDuration = existingEntries
                .Select(e => ParseDuration(e.Duration))
                .Aggregate(TimeSpan.Zero, (acc, duration) => acc.Add(duration));

            totalDuration = totalDuration.Add(ParseDuration(taskTimelog.Duration));

            _context.TaskTimelogs.RemoveRange(existingEntries);

            var allDescriptions = existingEntries
                .Where(e => !string.IsNullOrEmpty(e.Description))
                .Select(e => e.Description)
                .Concat(new[] { taskTimelog.Description })
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct()
                .ToList();

            var consolidatedEntry = new TaskTimelog
            {
                TaskId = taskTimelog.TaskId,
                UserId = taskTimelog.UserId,
                Duration = FormatDuration(totalDuration),
                Description = string.Join("; ", allDescriptions),
                LoggedAt = DateTime.Now
            };

            _context.TaskTimelogs.Add(consolidatedEntry);
            await _context.SaveChangesAsync();

            return Success("Timelog aggregated successfully.", consolidatedEntry, StatusCodes.Status201Created);
        }
        else
        {
            _context.TaskTimelogs.Add(taskTimelog);
            await _context.SaveChangesAsync();

            return Success("Timelog created successfully.", taskTimelog, StatusCodes.Status201Created);
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "CanTimelogUpdate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PutTaskTimelog(int id, TaskTimelog taskTimelog)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserCompanyId = GetCurrentUserCompanyId();
        bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);
        bool canUpdateAnyTimelog = User.HasClaim("permission", Permissions.TimelogUpdate); // Already checked by policy, but we check scope here

        if (id != taskTimelog.Id)
        {
            return Error("ID mismatch.", StatusCodes.Status400BadRequest);
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(taskTimelog.Duration ?? "", @"^(?:2[0-3]|[01]?[0-9]):[0-5][0-9]$"))
        {
            return Error("Duration must be in HH:MM format (e.g., '08:30').", StatusCodes.Status400BadRequest);
        }

        var existingTimelog = await _context.TaskTimelogs
            .Include(tt => tt.Task)
            .FirstOrDefaultAsync(tt => tt.Id == id);

        if (existingTimelog == null)
        {
            return Error("Timelog not found.", StatusCodes.Status404NotFound);
        }

        bool canUpdateTimelog = false;
        if (isGlobalAdmin)
        {
            canUpdateTimelog = true;
        }
        else if (existingTimelog.Task?.CompanyId == currentUserCompanyId.Value)
        {
            // We assume if they have CanTimelogUpdate and belong to company, they might be admins
            // However, we should be more specific. Let's use TimelogReadAll as a proxy for 'Admin' level
            bool isCompanyAdmin = User.HasClaim("permission", Permissions.TimelogReadAll);
            
            if (isCompanyAdmin)
            {
                canUpdateTimelog = true;
            }
            else
            {
                // Employees can only update their own logs
                canUpdateTimelog = existingTimelog.UserId == currentUserId.Value;
            }
        }

        if (!canUpdateTimelog)
        {
            return Error("You do not have permission to update this timelog.", StatusCodes.Status403Forbidden);
        }

        if (!isGlobalAdmin && !User.HasClaim("permission", Permissions.TimelogReadAll) &&
            (existingTimelog.UserId != taskTimelog.UserId || existingTimelog.TaskId != taskTimelog.TaskId))
        {
            return Error("You cannot change the user or task associated with a timelog.", StatusCodes.Status403Forbidden);
        }

        existingTimelog.TaskId = taskTimelog.TaskId;
        existingTimelog.UserId = taskTimelog.UserId;
        existingTimelog.Duration = taskTimelog.Duration;
        existingTimelog.Description = taskTimelog.Description;
        existingTimelog.LoggedAt = DateTime.Now;

        _context.Entry(existingTimelog).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!TaskTimelogExists(id))
            {
                return Error("Timelog not found after concurrent update.", StatusCodes.Status404NotFound);
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "CanTimelogDelete")] // Requires 'timelog:delete' permission
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteTaskTimelog(int id)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserCompanyId = GetCurrentUserCompanyId();
        bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);
        bool isCompanyAdmin = User.HasClaim("permission", Permissions.TimelogReadAll);

        var taskTimelog = await _context.TaskTimelogs
            .Include(tt => tt.Task)
            .FirstOrDefaultAsync(tt => tt.Id == id);

        if (taskTimelog == null)
        {
            return Error("Timelog not found.", StatusCodes.Status404NotFound);
        }

        bool canDeleteTimelog = false;
        if (isGlobalAdmin)
        {
            canDeleteTimelog = true; // Super Admin can delete any timelog
        }
        else if (taskTimelog.Task?.CompanyId == currentUserCompanyId.Value) // Timelog's task must be in user's company
        {
            if (isCompanyAdmin)
            {
                canDeleteTimelog = true; // Company Admin can delete any timelog in their company
            }
            else // Regular user/employee
            {
                // Employee can only delete their own timelogs
                canDeleteTimelog = taskTimelog.UserId == currentUserId.Value;
            }
        }

        if (!canDeleteTimelog)
        {
            return Error("You do not have permission to delete this timelog.", StatusCodes.Status403Forbidden);
        }

        _context.TaskTimelogs.Remove(taskTimelog);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool TaskTimelogExists(int id)
    {
        return _context.TaskTimelogs.Any(e => e.Id == id);
    }
}
