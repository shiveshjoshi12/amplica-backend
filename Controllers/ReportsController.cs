using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BizfreeApp.Constants;
using Task = BizfreeApp.Models.Task;
using System.Linq.Expressions;

namespace BizfreeApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TaskReportsController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly Data.ApplicationDbContext _context;

        public TaskReportsController(ILogger<TaskReportsController> logger, Data.ApplicationDbContext context)
        { 
            _logger = logger;
            _context = context;
        }

        // Helper methods (same as your existing controller)
        private Expression<Func<Models.Task, bool>> BuildMemberFilterExpression(List<string> memberIds)
        {
            var parameter = Expression.Parameter(typeof(Models.Task), "t");
            Expression? orExpression = null;

            var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) })!;
            var endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) })!;
            var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) })!;

            foreach (var id in memberIds)
            {
                if (int.TryParse(id, out int parsedId))
                {
                    var assignedToProp = Expression.Property(parameter, "AssignedTo");
                    var parsedIdConst = Expression.Constant((int?)parsedId, typeof(int?));
                    var eqAssignedTo = Expression.Equal(assignedToProp, parsedIdConst);

                    var assignedUserIdsProp = Expression.Property(parameter, "AssignedUserIds");
                    var idConst = Expression.Constant(id, typeof(string));
                    var eqAssignedUserIds = Expression.Equal(assignedUserIdsProp, idConst);

                    var startsWithConst = Expression.Constant(id + ",", typeof(string));
                    var startsWithExp = Expression.Call(assignedUserIdsProp, startsWithMethod, startsWithConst);

                    var endsWithConst = Expression.Constant("," + id, typeof(string));
                    var endsWithExp = Expression.Call(assignedUserIdsProp, endsWithMethod, endsWithConst);

                    var containsConst = Expression.Constant("," + id + ",", typeof(string));
                    var containsExp = Expression.Call(assignedUserIdsProp, containsMethod, containsConst);

                    var notNullExp = Expression.NotEqual(assignedUserIdsProp, Expression.Constant(null, typeof(string)));
                    
                    var stringChecks = Expression.OrElse(eqAssignedUserIds, startsWithExp);
                    stringChecks = Expression.OrElse(stringChecks, endsWithExp);
                    stringChecks = Expression.OrElse(stringChecks, containsExp);
                    
                    var safeStringChecks = Expression.AndAlso(notNullExp, stringChecks);

                    var allChecksForId = Expression.OrElse(eqAssignedTo, safeStringChecks);

                    if (orExpression == null)
                    {
                        orExpression = allChecksForId;
                    }
                    else
                    {
                        orExpression = Expression.OrElse(orExpression, allChecksForId);
                    }
                }
            }

            if (orExpression == null)
            {
                return t => true;
            }

            return Expression.Lambda<Func<Models.Task, bool>>(orExpression, parameter);
        }

        private bool HasPermission(string permission)
        {
            return User.HasClaim("permission", permission);
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedUserId))
                return parsedUserId;
            return null;
        }

        private int? GetCurrentUserCompanyId()
        {
            var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
            if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out int parsedCompanyId))
                return parsedCompanyId;
            return null;
        }

        private int? GetCurrentUserRoleId()
        {
            var roleIdClaim = User.Claims.FirstOrDefault(c => c.Type == "RoleId");
            if (roleIdClaim != null && int.TryParse(roleIdClaim.Value, out int parsedRoleId))
                return parsedRoleId;
            return null;
        }

        private TimeSpan ParseDuration(string duration)
        {
            if (string.IsNullOrEmpty(duration)) return TimeSpan.Zero;
            var parts = duration.Split(':');
            if (parts.Length != 2) return TimeSpan.Zero;
            if (int.TryParse(parts[0], out int hours) && int.TryParse(parts[1], out int minutes))
                return new TimeSpan(hours, minutes, 0);
            return TimeSpan.Zero;
        }

        private decimal ConvertDurationToHours(string duration)
        {
            var timeSpan = ParseDuration(duration);
            return (decimal)timeSpan.TotalHours;
        }

        private bool IsCompletedStatus(string statusName)
        {
            // Check if status name indicates completion
            return !string.IsNullOrEmpty(statusName) &&
                   statusName.Equals("Completed", StringComparison.OrdinalIgnoreCase);
        }

        protected ActionResult<T> Success<T>(string message, T? data = default, int statusCode = StatusCodes.Status200OK)
        {
            return Ok(new ApiResponse<T>(message, "Success", statusCode, data));
        }

        protected ActionResult<T> Error<T>(string message, int statusCode = StatusCodes.Status400BadRequest, object? errorDetails = null)
        {
            var apiResponse = new ApiResponse<T>(message, "Error", statusCode, default, errorDetails);
            return StatusCode(statusCode, apiResponse);
        }

        [HttpGet("user-tasks-by-status/{userId}")]
        [Authorize(Policy = "CanTimelogRead")]
        [ProducesResponseType(typeof(ApiResponse<UserTaskReportSimplifiedDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<UserTaskReportSimplifiedDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<UserTaskReportSimplifiedDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<UserTaskReportSimplifiedDto>), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<UserTaskReportSimplifiedDto>> GetUserTasksByStatus(
     [FromRoute] int userId,
     [FromQuery] DateTime? startDateTo = null,
     [FromQuery] DateTime? endDateTo = null,
     [FromQuery] string? statuses = null,
     [FromQuery] int pageNumber = 1,
     [FromQuery] int pageSize = 10)
        {
            var currentUserId = GetCurrentUserId();
            var currentUserCompanyId = GetCurrentUserCompanyId();

            if (!currentUserId.HasValue || !currentUserCompanyId.HasValue)
            {
                return Error<UserTaskReportSimplifiedDto>("Authentication information is missing.", StatusCodes.Status401Unauthorized);
            }

            // Authorization logic: Scoped Permission System
            bool isAuthorized = false;

            // 1. ReportReadAll (Company Scope): If user has this permission, they can view any report within their company.
            if (HasPermission(Permissions.ReportReadAll))
            {
                isAuthorized = true;
                _logger.LogInformation($"User {currentUserId} authorized by {Permissions.ReportReadAll} to view User {userId}'s report.");
            }
            // 2. ReportReadDepartment (Department Scope): Authorize if target user is in the same department as the requester.
            else if (HasPermission(Permissions.ReportReadDepartment))
            {
                var currentUserDeptId = await _context.CompanyUsers
                    .Where(cu => cu.UserId == currentUserId.Value &&
                                cu.CompanyId == currentUserCompanyId.Value &&
                                !(cu.IsDeleted ?? false))
                    .Select(cu => cu.DepartmentId)
                    .FirstOrDefaultAsync();

                if (currentUserDeptId.HasValue)
                {
                    var isTargetInSameDept = await _context.CompanyUsers
                        .AnyAsync(cu => cu.UserId == userId &&
                                      cu.CompanyId == currentUserCompanyId.Value &&
                                      cu.DepartmentId == currentUserDeptId.Value &&
                                      !(cu.IsDeleted ?? false));

                    if (isTargetInSameDept)
                    {
                        isAuthorized = true;
                        _logger.LogInformation($"User {currentUserId} authorized by {Permissions.ReportReadDepartment} to view User {userId}'s report (Same Department: {currentUserDeptId}).");
                    }
                }
            }
            // 3. ReportRead (Self Scope): Authorize if viewing own report.
            else if (HasPermission(Permissions.ReportRead))
            {
                if (userId == currentUserId.Value)
                {
                    isAuthorized = true;
                    _logger.LogInformation($"User {currentUserId} authorized by {Permissions.ReportRead} to view their own report.");
                }
            }

            if (!isAuthorized)
            {
                _logger.LogWarning($"User {currentUserId} (Company: {currentUserCompanyId}) denied access to User {userId}'s report.");
                return Error<UserTaskReportSimplifiedDto>("You don't have permission to view this report.", StatusCodes.Status403Forbidden);
            }

            // Validate target user exists and belongs to the same company
            var targetUser = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive && !u.IsDeleted);

            if (targetUser == null)
            {
                return Error<UserTaskReportSimplifiedDto>("User not found.", StatusCodes.Status404NotFound);
            }

            if (targetUser.CompanyId != currentUserCompanyId.Value)
            {
                return Error<UserTaskReportSimplifiedDto>("Multi-tenant security violation: Target user belongs to a different company.", StatusCodes.Status403Forbidden);
            }

            // Fetch CompanyUser details for name
            var companyUserRecord = await _context.CompanyUsers
                .Where(cu => cu.UserId == userId && cu.CompanyId == currentUserCompanyId.Value && !(cu.IsDeleted ?? false))
                .FirstOrDefaultAsync();

            var userName = companyUserRecord != null ?
                $"{companyUserRecord.FirstName} {companyUserRecord.LastName}".Trim() :
                "Unknown User";

            // Calculate date ranges based on provided parameters
            DateTime? filterStartDate = null;
            DateTime? filterEndDate = null;

            if (startDateTo.HasValue || endDateTo.HasValue)
            {
                filterStartDate = startDateTo?.Date ?? DateTime.MinValue;
                filterEndDate = endDateTo?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue;
            }

            // Base query for user's tasks
            IQueryable<Task> baseQuery = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.StatusNavigation)
                .Include(t => t.AssignedToNavigation)
                    .ThenInclude(u => u.CompanyUserUsers)
                .Include(t => t.TaskTimelogs)
                .Where(t => t.AssignedTo == userId && !t.IsDeleted);

            // Apply company-level filtering to maintain multi-tenant security
            baseQuery = baseQuery.Where(t => t.CompanyId == currentUserCompanyId.Value);

            // Apply date filtering based on StartDate (consistent with StartDate-based grouping).
            // When start == end (single day), returns only tasks whose StartDate matches that day.
            if (filterStartDate.HasValue && filterEndDate.HasValue)
            {
                var startDateOnly = DateOnly.FromDateTime(filterStartDate.Value);
                var endDateOnly = DateOnly.FromDateTime(filterEndDate.Value);

                baseQuery = baseQuery.Where(t =>
                    t.StartDate.HasValue &&
                    t.StartDate.Value >= startDateOnly &&
                    t.StartDate.Value <= endDateOnly);
            }

            // Apply status filtering
            if (!string.IsNullOrEmpty(statuses))
            {
                var statusList = statuses.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLower())
                    .ToList();
                baseQuery = baseQuery.Where(t => t.StatusNavigation != null && statusList.Contains(t.StatusNavigation.Name.ToLower()));
            }

            // --- REFACTOR: Database-side aggregation and pagination ---
            
            // 1. Summary Statistics (Database side where possible)
            var totalTasks = await baseQuery.CountAsync();
            var completedTasks = await baseQuery.CountAsync(t => t.StatusNavigation != null && t.StatusNavigation.Name.ToLower() == "completed");
            var pendingTasks = totalTasks - completedTasks;
            var totalEstimatedHours = await baseQuery.SumAsync(t => t.EstimatedHours ?? 0);

            // Fetch only durations for summary calculation to avoid pulling full Task objects
            var allDurations = await baseQuery
                .SelectMany(t => t.TaskTimelogs
                    .Where(tl => (!filterStartDate.HasValue || tl.LoggedAt >= filterStartDate) && 
                                 (!filterEndDate.HasValue || tl.LoggedAt <= filterEndDate))
                    .Select(tl => tl.Duration))
                .ToListAsync();

            var totalActualHours = allDurations.Sum(d => ConvertDurationToHours(d ?? "00:00"));
            var totalLoggedEntries = allDurations.Count;

            // 2. Pagination metadata
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;
            var totalPages = (int)Math.Ceiling(totalTasks / (double)pageSize);

            // 3. Paginate and Project (Database side)
            var pagedTasksData = await baseQuery
                .OrderByDescending(t => t.StartDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.TaskId,
                    t.Title,
                    t.ProjectId,
                    ProjectName = t.Project != null ? t.Project.Name : "No Project",
                    StatusName = t.StatusNavigation != null ? t.StatusNavigation.Name : "Unknown",
                    EstimatedHours = t.EstimatedHours ?? 0,
                    t.StartDate,
                    t.DueDate,
                    t.EndDate,
                    t.Progression,
                    Timelogs = t.TaskTimelogs
                        .Where(tl => (!filterStartDate.HasValue || tl.LoggedAt >= filterStartDate) && 
                                     (!filterEndDate.HasValue || tl.LoggedAt <= filterEndDate))
                        .OrderByDescending(tl => tl.LoggedAt)
                        .Select(tl => new
                        {
                            tl.LoggedAt,
                            tl.Duration,
                            tl.Description
                        })
                        .ToList()
                })
                .ToListAsync();

            // 4. Map to DTOs and group by date (In-memory for the current page only)
            var pagedTasks = pagedTasksData.Select(t => new TaskDetailSimplifiedDto
            {
                TaskId = t.TaskId.ToString(),
                Title = t.Title ?? "Untitled",
                ProjectId = t.ProjectId?.ToString(),
                ProjectName = t.ProjectName,
                OriginalStatus = t.StatusName,
                EstimatedHours = t.EstimatedHours,
                ActualHours = t.Timelogs.Sum(tl => ConvertDurationToHours(tl.Duration ?? "00:00")),
                LoggedEntries = t.Timelogs.Count,
                StartDate = t.StartDate,
                DueDate = t.DueDate,
                EndDate = t.EndDate,
                Progression = t.Progression,
                LastLoggedAt = t.Timelogs.Any() ? t.Timelogs.Max(tl => tl.LoggedAt) : (DateTime?)null,
                TimeLogs = t.Timelogs.Select(tl => new TimeLogEntryDto
                {
                    LoggedAt = tl.LoggedAt,
                    Duration = tl.Duration ?? "00:00",
                    Hours = ConvertDurationToHours(tl.Duration ?? "00:00"),
                    Description = tl.Description
                }).ToList()
            }).ToList();

            var pagedDateGroups = pagedTasks
                .GroupBy(t => t.StartDate ?? DateOnly.MinValue)
                .OrderByDescending(g => g.Key)
                .Select(g => new TaskDateGroupSimplifiedDto
                {
                    Date = g.Key,
                    TaskCount = g.Count(),
                    TotalEstimatedHours = g.Sum(t => t.EstimatedHours),
                    TotalActualHours = g.Sum(t => t.ActualHours),
                    TotalLoggedEntries = g.Sum(t => t.LoggedEntries),
                    Tasks = g.OrderByDescending(t => t.ActualHours).ToList()
                })
                .ToList();

            var report = new UserTaskReportSimplifiedDto
            {
                UserId = userId.ToString(),
                UserName = userName,
                DateRange = new DateRangeDto
                {
                    StartDate = filterStartDate == DateTime.MinValue ? null : filterStartDate,
                    EndDate = filterEndDate == DateTime.MaxValue ? null : filterEndDate
                },
                Summary = new TaskSummaryDto
                {
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks,
                    PendingTasks = pendingTasks,
                    TotalEstimatedHours = totalEstimatedHours,
                    TotalActualHours = totalActualHours,
                    TotalLoggedEntries = totalLoggedEntries,
                    EfficiencyRatio = totalEstimatedHours > 0 ?
                        Math.Round((double)(totalActualHours / totalEstimatedHours), 2) : 0
                },
                Pagination = new PaginationMetaDto
                {
                    CurrentPage = pageNumber,
                    PageSize = pageSize,
                    TotalTasks = totalTasks,
                    TotalPages = totalPages,
                    HasNextPage = pageNumber < totalPages,
                    HasPreviousPage = pageNumber > 1
                },
                TasksByDate = pagedDateGroups
            };

            _logger.LogInformation($"Generated task report for User {userId} with {totalTasks} tasks (Page {pageNumber}/{totalPages})");

            return Success("User task report by status generated successfully.", report);
        }

        [HttpGet("company-users-tasks-by-status")]
        [Authorize(Policy = "CanTimelogRead")]
        [ProducesResponseType(typeof(ApiResponse<CompanyTaskReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CompanyTaskReportDto>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<CompanyTaskReportDto>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<CompanyTaskReportDto>), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<CompanyTaskReportDto>> GetCompanyUserTasksByStatus(
            [FromQuery] DateTime? startDateTo = null,
            [FromQuery] DateTime? endDateTo = null,
            [FromQuery] string? statuses = null,
            [FromQuery] string? memberUserIds = null)
        {
            var currentUserId = GetCurrentUserId();
            var currentUserCompanyId = GetCurrentUserCompanyId();

            if (!currentUserId.HasValue || !currentUserCompanyId.HasValue)
            {
                return Error<CompanyTaskReportDto>("Authentication information is missing.", StatusCodes.Status401Unauthorized);
            }

            // --- TIERED PERMISSION CHECK ---
            bool hasReadAll = HasPermission(Permissions.ReportReadAll);
            bool hasReadDepartment = HasPermission(Permissions.ReportReadDepartment);

            if (!hasReadAll && !hasReadDepartment)
            {
                _logger.LogWarning($"User {currentUserId} (Company: {currentUserCompanyId}) denied access to company-wide report.");
                return Error<CompanyTaskReportDto>("You don't have permission to view company-wide reports.", StatusCodes.Status403Forbidden);
            }

            // Determine which department to scope to (null = no dept filter = all company)
            int? scopedDepartmentId = null;

            if (!hasReadAll && hasReadDepartment)
            {
                // Department Head: fetch their own DepartmentId from CompanyUsers
                scopedDepartmentId = await _context.CompanyUsers
                    .Where(cu => cu.UserId == currentUserId.Value &&
                                 cu.CompanyId == currentUserCompanyId.Value &&
                                 !(cu.IsDeleted ?? false))
                    .Select(cu => cu.DepartmentId)
                    .FirstOrDefaultAsync();

                if (!scopedDepartmentId.HasValue)
                {
                    return Error<CompanyTaskReportDto>("Your account is not associated with a department.", StatusCodes.Status403Forbidden);
                }

                _logger.LogInformation($"User {currentUserId} scoped to Department {scopedDepartmentId} for company report.");
            }

            // Fetch users — scoped to department if applicable, otherwise whole company
            var companyUsersQuery = _context.CompanyUsers
                .Where(cu => cu.CompanyId == currentUserCompanyId.Value && !(cu.IsDeleted ?? false));

            if (scopedDepartmentId.HasValue)
            {
                companyUsersQuery = companyUsersQuery.Where(cu => cu.DepartmentId == scopedDepartmentId.Value);
            }

            List<int>? requestedUserIds = null;
            if (!string.IsNullOrWhiteSpace(memberUserIds))
            {
                requestedUserIds = memberUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                .Select(s => s.Trim())
                                                .Where(s => int.TryParse(s, out _))
                                                .Select(int.Parse)
                                                .ToList();
            }

            if (requestedUserIds != null && requestedUserIds.Any())
            {
                companyUsersQuery = companyUsersQuery.Where(cu => requestedUserIds.Contains(cu.UserId));
            }

            var companyUsers = await companyUsersQuery
                .Select(cu => new
                {
                    cu.UserId,
                    FullName = $"{cu.FirstName} {cu.LastName}".Trim()
                })
                .ToListAsync();

            // Calculate date ranges
            DateTime? filterStartDate = null;
            DateTime? filterEndDate = null;

            if (startDateTo.HasValue || endDateTo.HasValue)
            {
                filterStartDate = startDateTo?.Date ?? DateTime.MinValue;
                filterEndDate = endDateTo?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue;
            }

            // Parse status filter
            List<string>? statusList = null;
            if (!string.IsNullOrEmpty(statuses))
            {
                statusList = statuses.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLower())
                    .ToList();
            }

            // Fetch all tasks for this company (with optional filters) — full projection same as single-user endpoint
            IQueryable<Task> baseQuery = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.StatusNavigation)
                .Include(t => t.TaskTimelogs)
                .Where(t => t.CompanyId == currentUserCompanyId.Value && !t.IsDeleted);

            // Scope tasks to the department users only (if department-scoped) or if specific users requested
            if (scopedDepartmentId.HasValue || (requestedUserIds != null && requestedUserIds.Any()))
            {
                var allowedUserIds = companyUsers.Select(cu => cu.UserId.ToString()).ToList();
                if (allowedUserIds.Any())
                {
                    baseQuery = baseQuery.Where(BuildMemberFilterExpression(allowedUserIds));
                }
                else
                {
                    baseQuery = baseQuery.Where(t => false);
                }
            }

            if (filterStartDate.HasValue && filterEndDate.HasValue)
            {
                var startDateOnly = DateOnly.FromDateTime(filterStartDate.Value);
                var endDateOnly = DateOnly.FromDateTime(filterEndDate.Value);

                baseQuery = baseQuery.Where(t =>
                    t.StartDate.HasValue &&
                    t.StartDate.Value >= startDateOnly &&
                    t.StartDate.Value <= endDateOnly);
            }

            if (statusList != null && statusList.Count > 0)
            {
                baseQuery = baseQuery.Where(t => t.StatusNavigation != null && statusList.Contains(t.StatusNavigation.Name.ToLower()));
            }

            // Fetch all matching tasks with full detail — same projection as user-tasks-by-status
            var allTasksData = await baseQuery
                .OrderByDescending(t => t.StartDate)
                .Select(t => new
                {
                    t.TaskId,
                    t.Title,
                    t.ProjectId,
                    t.AssignedTo,
                    t.AssignedUserIds,
                    ProjectName = t.Project != null ? t.Project.Name : "No Project",
                    StatusName = t.StatusNavigation != null ? t.StatusNavigation.Name : "Unknown",
                    IsCompleted = t.StatusNavigation != null && t.StatusNavigation.Name.ToLower() == "completed",
                    EstimatedHours = t.EstimatedHours ?? 0,
                    t.StartDate,
                    t.DueDate,
                    t.EndDate,
                    t.Progression,
                    Timelogs = t.TaskTimelogs
                        .Where(tl => (!filterStartDate.HasValue || tl.LoggedAt >= filterStartDate) &&
                                     (!filterEndDate.HasValue || tl.LoggedAt <= filterEndDate))
                        .OrderByDescending(tl => tl.LoggedAt)
                        .Select(tl => new
                        {
                            tl.LoggedAt,
                            tl.Duration,
                            tl.Description
                        })
                        .ToList()
                })
                .ToListAsync();

            // Build per-user summaries with full TasksByDate breakdown
            var userSummaries = companyUsers.Select(cu =>
            {
                var userIdStr = cu.UserId.ToString();
                var userTasksRaw = allTasksData.Where(t => 
                    t.AssignedTo == cu.UserId || 
                    (t.AssignedUserIds != null && 
                     (t.AssignedUserIds == userIdStr || 
                      t.AssignedUserIds.StartsWith(userIdStr + ",") || 
                      t.AssignedUserIds.EndsWith("," + userIdStr) || 
                      t.AssignedUserIds.Contains("," + userIdStr + ",")))
                ).ToList();

                // Map to TaskDetailSimplifiedDto (same as single-user endpoint)
                var userTasks = userTasksRaw.Select(t => new TaskDetailSimplifiedDto
                {
                    TaskId = t.TaskId.ToString(),
                    Title = t.Title ?? "Untitled",
                    ProjectId = t.ProjectId?.ToString(),
                    ProjectName = t.ProjectName,
                    OriginalStatus = t.StatusName,
                    EstimatedHours = t.EstimatedHours,
                    ActualHours = t.Timelogs.Sum(tl => ConvertDurationToHours(tl.Duration ?? "00:00")),
                    LoggedEntries = t.Timelogs.Count,
                    StartDate = t.StartDate,
                    DueDate = t.DueDate,
                    EndDate = t.EndDate,
                    Progression = t.Progression,
                    LastLoggedAt = t.Timelogs.Any() ? t.Timelogs.Max(tl => tl.LoggedAt) : (DateTime?)null,
                    TimeLogs = t.Timelogs.Select(tl => new TimeLogEntryDto
                    {
                        LoggedAt = tl.LoggedAt,
                        Duration = tl.Duration ?? "00:00",
                        Hours = ConvertDurationToHours(tl.Duration ?? "00:00"),
                        Description = tl.Description
                    }).ToList()
                }).ToList();

                // Group by StartDate (same as single-user endpoint)
                var tasksByDate = userTasks
                    .GroupBy(t => t.StartDate ?? DateOnly.MinValue)
                    .OrderByDescending(g => g.Key)
                    .Select(g => new TaskDateGroupSimplifiedDto
                    {
                        Date = g.Key,
                        TaskCount = g.Count(),
                        TotalEstimatedHours = g.Sum(t => t.EstimatedHours),
                        TotalActualHours = g.Sum(t => t.ActualHours),
                        TotalLoggedEntries = g.Sum(t => t.LoggedEntries),
                        Tasks = g.OrderByDescending(t => t.ActualHours).ToList()
                    })
                    .ToList();

                // Compute summary from mapped tasks
                var totalTasks = userTasks.Count;
                var completedTasks = userTasksRaw.Count(t => t.IsCompleted);
                var pendingTasks = totalTasks - completedTasks;
                var totalEstimatedHours = userTasks.Sum(t => t.EstimatedHours);
                var totalActualHours = userTasks.Sum(t => t.ActualHours);
                var totalLoggedEntries = userTasks.Sum(t => t.LoggedEntries);

                return new UserTaskSummaryDto
                {
                    UserId = cu.UserId.ToString(),
                    UserName = string.IsNullOrWhiteSpace(cu.FullName) ? "Unknown User" : cu.FullName,
                    Summary = new TaskSummaryDto
                    {
                        TotalTasks = totalTasks,
                        CompletedTasks = completedTasks,
                        PendingTasks = pendingTasks,
                        TotalEstimatedHours = totalEstimatedHours,
                        TotalActualHours = totalActualHours,
                        TotalLoggedEntries = totalLoggedEntries,
                        EfficiencyRatio = totalEstimatedHours > 0
                            ? Math.Round((double)(totalActualHours / totalEstimatedHours), 2)
                            : 0
                    },
                    TasksByDate = tasksByDate
                };
            })
            .OrderByDescending(u => u.Summary.TotalTasks)
            .ToList();

            // Company-level totals
            var companySummary = new TaskSummaryDto
            {
                TotalTasks = userSummaries.Sum(u => u.Summary.TotalTasks),
                CompletedTasks = userSummaries.Sum(u => u.Summary.CompletedTasks),
                PendingTasks = userSummaries.Sum(u => u.Summary.PendingTasks),
                TotalEstimatedHours = userSummaries.Sum(u => u.Summary.TotalEstimatedHours),
                TotalActualHours = userSummaries.Sum(u => u.Summary.TotalActualHours),
                TotalLoggedEntries = userSummaries.Sum(u => u.Summary.TotalLoggedEntries),
                EfficiencyRatio = userSummaries.Sum(u => u.Summary.TotalEstimatedHours) > 0
                    ? Math.Round((double)(userSummaries.Sum(u => u.Summary.TotalActualHours) /
                                         userSummaries.Sum(u => u.Summary.TotalEstimatedHours)), 2)
                    : 0
            };

            var result = new CompanyTaskReportDto
            {
                CompanyId = currentUserCompanyId.Value.ToString(),
                DateRange = new DateRangeDto
                {
                    StartDate = filterStartDate == DateTime.MinValue ? null : filterStartDate,
                    EndDate = filterEndDate == DateTime.MaxValue ? null : filterEndDate
                },
                CompanySummary = companySummary,
                UserSummaries = userSummaries
            };

            _logger.LogInformation($"Generated company-wide task report for Company {currentUserCompanyId} with {userSummaries.Count} users.");

            return Success("Company task report generated successfully.", result);
        }



    }
}
