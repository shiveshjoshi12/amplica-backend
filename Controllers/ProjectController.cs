using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Models.DTOs;
using BizfreeApp.Models;
using Microsoft.AspNetCore.Authorization; // Make sure this is present
using System.Linq.Expressions;
using System;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using BizfreeApp.Services;
using System.Collections.Generic;
using BizfreeApp.Constants; // Add this using directive to access your Permissions constants

namespace BizfreeApp.Controllers
{
    [Route("api/[controller]")]
    [Authorize] // Apply this at the controller level to ensure all actions require authentication by default
    [ApiController]
    public class ProjectController : ControllerBase
    {
        private readonly Data.ApplicationDbContext _context;
        private readonly ILogger<ProjectController> _logger;
        private readonly IUploadHandler _uploadHandler;
        private readonly ITaskNotificationService _taskNotificationService;
        private readonly IProjectEmailService _projectEmailService;

        public ProjectController(Data.ApplicationDbContext context, ILogger<ProjectController> logger, IUploadHandler uploadHandler, ITaskNotificationService taskNotificationService, IProjectEmailService projectEmailService)
        {
            _context = context;
            _logger = logger;
            _uploadHandler = uploadHandler;
            _taskNotificationService = taskNotificationService;
            _projectEmailService = projectEmailService;
        }

        // Helper method for consistent success responses
        private ActionResult<ApiResponse<T>> Success<T>(string message, T? data, int statusCode = StatusCodes.Status200OK)
        {
            return Ok(new ApiResponse<T>(message, "Success", statusCode, data));
        }

        // Helper method for consistent error responses
        private ActionResult<ApiResponse<T>> Error<T>(string message, int statusCode, object? errorDetails = null)
        {
            _logger.LogError("API Error: {Message} - Status Code: {StatusCode} - Details: {Details}", message, statusCode, errorDetails);
            return StatusCode(statusCode, new ApiResponse<T>(message, "Error", statusCode, data: default(T)));
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedUserId))
            {
                return parsedUserId;
            }
            _logger.LogWarning("UserId claim not found or could not be parsed.");
            return null;
        }

        private int? GetCurrentUserCompanyId()
        {
            var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
            if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out int parsedCompanyId))
            {
                return parsedCompanyId;
            }
            _logger.LogWarning("CompanyId claim not found or could not be parsed.");
            return null;
        }


        // GET: api/Project
        [HttpGet]
        [Authorize(Policy = "CanProjectRead")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PagedResult<object>>>> GetProjects(
     [FromQuery] int page = 1,
     [FromQuery] int pageSize = 5,
     [FromQuery] string sortBy = "endDate",
     [FromQuery] string sortOrder = "desc",
     [FromQuery] string? search = null,
     [FromQuery] string? statusNames = "Active,Delayed,InProgress,InReview,Open,NotStarted,Cancelled,Backlog",
     [FromQuery] DateOnly? startDateFrom = null,
     [FromQuery] DateOnly? startDateTo = null,
     [FromQuery] DateOnly? endDateFrom = null,
     [FromQuery] DateOnly? endDateTo = null,
     [FromQuery] int? companyIdFilter = null,
     [FromQuery] int? memberUserId = null
 )
        {
            try
            {
                _logger.LogInformation("Fetching projects based on user role with pagination and filtering.");

                var currentUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();

                if (!currentUserId.HasValue || !currentCompanyId.HasValue)
                {
                    return Error<PagedResult<object>>("Authentication information (UserId, CompanyId) is missing.", StatusCodes.Status401Unauthorized);
                }

                // Check if user has global or company-wide read permissions
                bool canReadAllCompanies = User.HasClaim("permission", Permissions.CompanyReadAll);
                bool canReadAllInCompany = User.HasClaim("permission", Permissions.ProjectReadAll);

                // Check if user is a department head by checking if they manage a department
                var managedDepartmentId = await _context.CompanyUsers
                    .Where(cu => cu.UserId == currentUserId.Value && cu.ManagedDepartmentId.HasValue)
                    .Select(cu => cu.ManagedDepartmentId)
                    .FirstOrDefaultAsync();

                bool isDepartmentHead = managedDepartmentId.HasValue;

                _logger.LogInformation($"User {currentUserId} - CanReadAllCompanies: {canReadAllCompanies}, CanReadAllInCompany: {canReadAllInCompany}, IsDepartmentHead: {isDepartmentHead}, ManagedDept: {managedDepartmentId}");

                var baseQuery = _context.Projects
                    .Where(p => p.IsDeleted != true);

                // Apply role-based filtering
                if (canReadAllCompanies)
                {
                    _logger.LogInformation($"User with Global Read (UserId: {currentUserId}) fetching projects.");

                    if (companyIdFilter.HasValue)
                        baseQuery = baseQuery.Where(p => p.CompanyId == companyIdFilter.Value);

                    if (memberUserId.HasValue)
                        baseQuery = baseQuery.Where(p => p.ProjectMembers.Any(pm => pm.UserId == memberUserId.Value && !(pm.IsDeleted ?? false)));
                }
                else if (canReadAllInCompany)
                {
                    _logger.LogInformation($"User with Company Read (UserId: {currentUserId}) fetching projects for CompanyId: {currentCompanyId}.");

                    baseQuery = baseQuery.Where(p => p.CompanyId == currentCompanyId.Value);

                    if (companyIdFilter.HasValue && companyIdFilter.Value != currentCompanyId.Value)
                        return Error<PagedResult<object>>("Cannot filter by other company.", StatusCodes.Status403Forbidden);

                    if (memberUserId.HasValue)
                    {
                        var userExists = await _context.Users.AnyAsync(u => u.UserId == memberUserId.Value && u.CompanyId == currentCompanyId.Value);
                        if (!userExists)
                            return Error<PagedResult<object>>($"Member {memberUserId.Value} not in your company.", StatusCodes.Status400BadRequest);

                        baseQuery = baseQuery.Where(p => p.ProjectMembers.Any(pm => pm.UserId == memberUserId.Value && !(pm.IsDeleted ?? false)));
                    }
                }
                else if (isDepartmentHead)
                {
                    // Department Head logic - regardless of global role name
                    _logger.LogInformation($"Department Head (UserId: {currentUserId}) managing department {managedDepartmentId}");

                    // Get all departments managed by this user
                    var managedDepartmentIds = await _context.Departments
                        .Where(d => d.DepartmentHead != null &&
                                   d.DepartmentHead.UserId == currentUserId.Value &&
                                   !(d.IsDeleted ?? false))
                        .Select(d => d.DeptId)
                        .ToListAsync();

                    if (!managedDepartmentIds.Any())
                    {
                        _logger.LogWarning($"No active departments found for department head.");

                        // Fallback to employee behavior
                        _logger.LogInformation($"Falling back to employee behavior for UserId: {currentUserId}");
                        baseQuery = baseQuery.Where(p => p.ProjectMembers.Any(pm => pm.UserId == currentUserId.Value && !(pm.IsDeleted ?? false)));
                    }
                    else
                    {
                        // Get all user IDs in the managed departments
                        var departmentUserIds = await _context.CompanyUsers
                            .Where(cu => cu.CompanyId == currentCompanyId.Value &&
                                        cu.DepartmentId.HasValue &&
                                        managedDepartmentIds.Contains(cu.DepartmentId.Value) &&
                                        !(cu.IsDeleted ?? false))
                            .Select(cu => cu.UserId)
                            .ToListAsync();

                        if (!departmentUserIds.Any())
                        {
                            // No users in department, show empty result
                            return Success("No projects found.", new PagedResult<object>
                            {
                                Items = new List<object>(),
                                TotalCount = 0,
                                PageNumber = page,
                                PageSize = pageSize,
                                TotalPages = 0,
                                TotalTasks = 0
                            });
                        }

                        // Get projects with department members
                        var departmentProjectIds = await _context.ProjectMembers
                            .Where(pm => departmentUserIds.Contains(pm.UserId) && !(pm.IsDeleted ?? false))
                            .Select(pm => pm.ProjectId)
                            .Distinct()
                            .ToListAsync();

                        if (departmentProjectIds.Any())
                        {
                            baseQuery = baseQuery.Where(p => p.CompanyId == currentCompanyId.Value &&
                                                            departmentProjectIds.Contains(p.ProjectId));
                        }
                        else
                        {
                            // No projects found for department, return empty
                            return Success("No projects found.", new PagedResult<object>
                            {
                                Items = new List<object>(),
                                TotalCount = 0,
                                PageNumber = page,
                                PageSize = pageSize,
                                TotalPages = 0,
                                TotalTasks = 0
                            });
                        }

                        if (companyIdFilter.HasValue && companyIdFilter.Value != currentCompanyId.Value)
                            return Error<PagedResult<object>>("Cannot filter by other company.", StatusCodes.Status403Forbidden);

                        if (memberUserId.HasValue)
                        {
                            if (!departmentUserIds.Contains(memberUserId.Value))
                            {
                                return Error<PagedResult<object>>($"User {memberUserId.Value} is not in your managed department.", StatusCodes.Status403Forbidden);
                            }

                            var memberProjectIds = await _context.ProjectMembers
                                .Where(pm => pm.UserId == memberUserId.Value && !(pm.IsDeleted ?? false))
                                .Select(pm => pm.ProjectId)
                                .ToListAsync();

                            if (memberProjectIds.Any())
                            {
                                baseQuery = baseQuery.Where(p => memberProjectIds.Contains(p.ProjectId));
                            }
                        }
                    }
                }
                else
                {
                    // Regular employee - can only see projects they're assigned to
                    _logger.LogInformation($"Employee (UserId: {currentUserId}) fetching their assigned projects.");

                    baseQuery = baseQuery.Where(p => p.ProjectMembers.Any(pm => pm.UserId == currentUserId.Value && !(pm.IsDeleted ?? false)));
                }

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    string lowerSearch = search.ToLower();
                    baseQuery = baseQuery.Where(p =>
                        (p.Name != null && p.Name.ToLower().Contains(lowerSearch)) ||
                        (!string.IsNullOrEmpty(p.Description) && p.Description.ToLower().Contains(lowerSearch)));
                }

                // Apply status filter
                if (!string.IsNullOrWhiteSpace(statusNames))
                {
                    var parsedStatus = statusNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim().ToLower())
                        .ToList();

                    if (parsedStatus.Any())
                        baseQuery = baseQuery.Where(p => p.StatusNavigation != null && parsedStatus.Contains(p.StatusNavigation.Name.ToLower()));
                }

                // Apply date filters
                bool isCombinedDateFilter = startDateFrom.HasValue && endDateTo.HasValue;
                if (isCombinedDateFilter)
                {
                    baseQuery = baseQuery.Where(p =>
                        p.StartDate.HasValue && p.EndDate.HasValue &&
                        p.StartDate.Value >= startDateFrom.Value &&
                        p.EndDate.Value <= endDateTo.Value);
                }
                else
                {
                    if (startDateFrom.HasValue)
                        baseQuery = baseQuery.Where(p => p.StartDate.HasValue && p.StartDate.Value >= startDateFrom.Value);
                    if (startDateTo.HasValue)
                        baseQuery = baseQuery.Where(p => p.StartDate.HasValue && p.StartDate.Value <= startDateTo.Value);
                    if (endDateFrom.HasValue)
                        baseQuery = baseQuery.Where(p => p.EndDate.HasValue && p.EndDate.Value >= endDateFrom.Value);
                    if (endDateTo.HasValue)
                        baseQuery = baseQuery.Where(p => p.EndDate.HasValue && p.EndDate.Value <= endDateTo.Value);
                }

                // Get total count before pagination
                var totalProjects = await baseQuery.CountAsync();

                // Apply sorting
                var sortedQuery = ApplySorting(baseQuery, sortBy, sortOrder);

                // Apply pagination
                page = Math.Max(1, page);
                var projectIds = await sortedQuery
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => p.ProjectId)
                    .ToListAsync();

                // Get detailed project data
                var detailedProjects = await GetDetailedProjectDataWithOrder(projectIds, sortBy, sortOrder);

                // Ensure detailedProjects is never null
                if (detailedProjects == null)
                {
                    detailedProjects = new List<object>();
                }

                var totalTasks = 0;
                if (projectIds.Any())
                {
                    totalTasks = await _context.Tasks
                        .Where(t => t.ProjectId.HasValue &&
                                   projectIds.Contains(t.ProjectId.Value) &&
                                   !t.IsDeleted &&
                                   t.ParentTaskId == null)
                        .CountAsync();
                }

                var pagedResult = new PagedResult<object>
                {
                    Items = detailedProjects,
                    TotalCount = totalProjects,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalPages = totalProjects > 0 ? (int)Math.Ceiling((double)totalProjects / pageSize) : 0,
                    TotalTasks = totalTasks
                };

                _logger.LogInformation($"Retrieved {detailedProjects.Count} projects (Total: {totalProjects}) for User ID: {currentUserId}.");
                return Success("Projects retrieved successfully.", pagedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching projects.");
                return Error<PagedResult<object>>("An error occurred while retrieving projects.", StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        private IQueryable<Project> ApplySorting(IQueryable<Project> query, string sortBy, string sortOrder)
        {
            bool descending = sortOrder.ToLower() == "desc";

            // Add necessary includes for sorting based on the sort field
            switch (sortBy.ToLower())
            {
                case "statusname":
                    query = query.Include(p => p.StatusNavigation);
                    break;
                case "companyname":
                    query = query.Include(p => p.Company);
                    break;
            }

            switch (sortBy.ToLower())
            {
                case "name":
                    return descending ? query.OrderByDescending(p => p.Name)
                                      : query.OrderBy(p => p.Name);

                case "startdate":
                    return descending ? query.OrderByDescending(p => p.StartDate)
                                      : query.OrderBy(p => p.StartDate);

                case "enddate":
                    return descending ? query.OrderByDescending(p => p.EndDate)
                                      : query.OrderBy(p => p.EndDate);

                case "statusname":
                    return descending ? query.OrderByDescending(p => p.StatusNavigation.Name)
                                      : query.OrderBy(p => p.StatusNavigation.Name);

                case "createdat":
                    return descending ? query.OrderByDescending(p => p.CreatedAt)
                                      : query.OrderBy(p => p.CreatedAt);

                case "companyname":
                    return descending ? query.OrderByDescending(p => p.Company.CompanyName)
                                      : query.OrderBy(p => p.Company.CompanyName);

                case "progression":
                    return descending ? query.OrderByDescending(p => p.Progression)
                                      : query.OrderBy(p => p.Progression);

                default:
                    // Default fallback sorting
                    return query.OrderByDescending(p => p.CreatedAt);
            }
        }

        private async Task<List<object>> GetDetailedProjectDataWithOrder(List<int> projectIds, string sortBy, string sortOrder)
        {
            // First, get the projects with proper ordering to maintain sort sequence
            var projectsQuery = _context.Projects
                .Where(p => projectIds.Contains(p.ProjectId))
                .Include(p => p.StatusNavigation)
                .Include(p => p.Company)
                .Include(p => p.CreatedByNavigation)
                .Include(p => p.UpdatedByNavigation);

            // Apply the same sorting logic to maintain order
            var sortedProjects = ApplySorting(projectsQuery, sortBy, sortOrder);
            var projects = await sortedProjects.ToListAsync();

            // Get all related data for the projects
            var projectMembers = await _context.ProjectMembers
                .Where(pm => projectIds.Contains(pm.ProjectId) && pm.IsDeleted != true)
                .Include(pm => pm.User)
                    .ThenInclude(u => u.CompanyUserUsers)
                .ToListAsync();

            var taskLists = await _context.TaskLists
                .Where(tl => projectIds.Contains(tl.ProjectId) && tl.IsDeleted != true)
                .OrderBy(tl => tl.ListOrder)
                .ToListAsync();

            // Fetch only top-level tasks (tasks without a parent) using direct foreign key access
            var tasks = await _context.Tasks
    .Where(t => t.ProjectId.HasValue
                && projectIds.Contains(t.ProjectId.Value)
                && !t.IsDeleted
                && t.ParentTaskId == null).Include(t => t.StatusNavigation)
                .Include(t => t.AssignedToNavigation)
                    .ThenInclude(u => u.CompanyUserUsers)
                .OrderBy(t => t.TaskOrder)
                .ToListAsync();

            var projectDocuments = await _context.ProjectDocuments
                .Where(pd => projectIds.Contains(pd.ProjectId) && pd.IsDeleted != true)
                .Include(pd => pd.UploadedByUser)
                    .ThenInclude(u => u.CompanyUserUsers)
                .ToListAsync();

            // Build the result maintaining the sorted order
            var result = projects.Select(p => new
            {
                p.ProjectId,
                p.Name,
                p.Description,
                Status = p.StatusNavigation?.Name ?? p.Status?.ToString(),
                StatusName = p.StatusNavigation?.Name ?? p.Status?.ToString(),
                p.StartDate,
                p.EndDate,
                p.IsActive,
                p.CreatedAt,
                p.UpdatedAt,
                p.Progression,
                Progress = p.Progression,
                Company = p.Company != null ? new
                {
                    p.Company.CompanyId,
                    CompanyName = p.Company.CompanyName
                } : null,
                CreatedBy = p.CreatedByNavigation != null ? new
                {
                    p.CreatedByNavigation.UserId,
                    p.CreatedByNavigation.Email
                } : null,
                UpdatedBy = p.UpdatedByNavigation != null ? new
                {
                    p.UpdatedByNavigation.UserId,
                    p.UpdatedByNavigation.Email
                } : null,
                ProjectMembers = projectMembers
                    .Where(pm => pm.ProjectId == p.ProjectId)
                    .Select(pm => new {
                        pm.Id,
                        pm.UserId,
                        pm.JoinedAt,
                        User = pm.User != null ? new
                        {
                            pm.User.UserId,
                            pm.User.Email,
                            ProfilePhotoUrl = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId)?.ProfilePhotoUrl,
                            FirstName = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId)?.FirstName,
                            LastName = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId)?.LastName
                        } : null
                    }).ToList(),
                TaskLists = taskLists
                    .Where(tl => tl.ProjectId == p.ProjectId)
                    .Select(tl => new
                    {
                        tl.TaskListId,
                        tl.ListName,
                        tl.Description,
                        tl.ListOrder,
                        tl.Status,
                        tl.IsActive,
                        tl.CreatedAt,
                        tl.UpdatedAt,
                        tl.EndDate,
                        tl.StartDate,
                        Tasks = tasks
                        .Where(t => t.TaskListId == tl.TaskListId && t.ParentTaskId == null) // This list *only* contains top-level tasks now
                            .Select(t => new
                            {
                                t.TaskId,
                                t.Title,
                                t.Description,
                                t.DueDate,
                                t.CompanyId,
                                t.TaskListId,
                                Status = t.StatusNavigation?.Name ?? t.Status?.ToString(),
                                t.EstimatedHours,
                                t.ActualHours,
                                t.TaskOrder,
                                t.IsActive,
                                t.CreatedAt,
                                AssignedTo = t.AssignedToNavigation != null ? new
                                {
                                    t.AssignedToNavigation.UserId,
                                    t.AssignedToNavigation.Email,
                                    ProfilePhotoUrl = t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId)?.ProfilePhotoUrl,
                                    FirstName = t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId)?.FirstName,
                                    LastName = t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId)?.LastName
                                } : null,
                                Progression = t.Progression
                            })
                            .ToList()
                    })
                    .ToList(),
                ProjectDocuments = projectDocuments
                    .Where(pd => pd.ProjectId == p.ProjectId)
                    .Select(pd => new
                    {
                        pd.DocumentId,
                        pd.DocumentName,
                        pd.DocumentType,
                        pd.FilePath,
                        pd.FileSize,
                        pd.Description,
                        pd.Version,
                        pd.IsActive,
                        pd.CreatedAt,
                        UploadedBy = pd.UploadedByUser != null ? new
                        {
                            pd.UploadedByUser.UserId,
                            pd.UploadedByUser.Email,
                            ProfilePhotoUrl = pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId)?.ProfilePhotoUrl,
                            FirstName = pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId)?.FirstName,
                            LastName = pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId)?.LastName
                        } : null
                    })
                    .ToList(),
                // This count will now correctly reflect only top-level tasks.
                TaskCount = tasks.Count(t => t.ProjectId == p.ProjectId && t.ParentTaskId == null)
            }).Cast<object>().ToList();

            return result;
        }


        // GET: api/Project/5
        [HttpGet("{id}")]
        [Authorize(Policy = "CanProjectRead")] // Policy for reading a specific project
        public async Task<ActionResult<ApiResponse<object>>> GetProject(int id)
        {
            try
            {
                _logger.LogInformation($"Fetching project with ID: {id} based on user role.");

                var currentUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();
                if (!currentUserId.HasValue || !currentCompanyId.HasValue)
                {
                    return Unauthorized(new ApiResponse<object>("Authentication information (UserId, CompanyId) is missing.", "Error", 401));
                }

                bool canReadAllCompanies = User.HasClaim("permission", Permissions.CompanyReadAll);
                bool canReadAllInCompany = User.HasClaim("permission", Permissions.ProjectReadAll);

                IQueryable<Project> query = _context.Projects
           .Include(p => p.StatusNavigation)
           .Include(p => p.TaskLists)
               .ThenInclude(tl => tl.Tasks.Where(t => !t.IsDeleted && (t.ParentTaskId == null || t.ParentTaskId == 0)))
                   .ThenInclude(t => t.StatusNavigation)
           .Include(p => p.TaskLists)
               .ThenInclude(tl => tl.Tasks.Where(t => !t.IsDeleted && (t.ParentTaskId == null || t.ParentTaskId == 0)))
                   .ThenInclude(t => t.AssignedToNavigation)
                       .ThenInclude(u => u.CompanyUserUsers)
           .Include(p => p.TaskLists)
               .ThenInclude(tl => tl.Tasks.Where(t => !t.IsDeleted && (t.ParentTaskId == null || t.ParentTaskId == 0)))
                   .ThenInclude(t => t.TaskDocuments.Where(td => !td.IsDeleted))
           .Include(p => p.TaskLists)
               .ThenInclude(tl => tl.Tasks.Where(t => !t.IsDeleted && (t.ParentTaskId == null || t.ParentTaskId == 0)))
                   .ThenInclude(t => t.SubTasks.Where(st => !st.IsDeleted)) // Include Sub-tasks
                       .ThenInclude(st => st.StatusNavigation) // Include Sub-task status
           .Include(p => p.TaskLists)
               .ThenInclude(tl => tl.Tasks.Where(t => !t.IsDeleted && (t.ParentTaskId == null || t.ParentTaskId == 0)))
                   .ThenInclude(t => t.SubTasks.Where(st => !st.IsDeleted))
                       .ThenInclude(st => st.AssignedToNavigation) // Include Sub-task assigned user
                           .ThenInclude(u => u.CompanyUserUsers)
           .Include(p => p.TaskLists)
               .ThenInclude(tl => tl.Tasks.Where(t => !t.IsDeleted && (t.ParentTaskId == null || t.ParentTaskId == 0)))
                   .ThenInclude(t => t.SubTasks.Where(st => !st.IsDeleted))
                       .ThenInclude(st => st.TaskDocuments.Where(td => !td.IsDeleted)) // Include Sub-task documents
           .Where(p => p.ProjectId == id && p.IsDeleted != true);

                if (canReadAllCompanies)
                {
                    _logger.LogInformation($"User with Global Read (User ID: {currentUserId}, Company ID: {currentCompanyId}) is fetching project ID: {id}.");
                }
                else if (canReadAllInCompany)
                {
                    _logger.LogInformation($"User with Company Read (User ID: {currentUserId}, Company ID: {currentCompanyId}) is fetching project ID: {id} within their company.");
                    query = query.Where(p => p.CompanyId == currentCompanyId.Value);
                }
                else if (User.HasClaim("permission", Permissions.ProjectRead))
                {
                    _logger.LogInformation($"User with Restricted Read (User ID: {currentUserId}, Company ID: {currentCompanyId}) is fetching project ID: {id} they are assigned to.");
                    query = query.Where(p => p.ProjectMembers.Any(pm => pm.UserId == currentUserId.Value));
                }
                else
                {
                    _logger.LogWarning($"User ID: {currentUserId} attempted to access project ID: {id} without sufficient permissions.");
                    return StatusCode(403, new ApiResponse<object>("Access denied due to missing project read permissions.", "Error", 403));
                }

                var project = await query
                    .Select(p => new
                    {
                        p.ProjectId,
                        p.CompanyId,
                        p.Name,
                        p.Description,
                        p.Status,
                        p.StartDate,
                        p.EndDate,
                        p.IsActive,
                        p.CreatedAt,
                        p.UpdatedAt,
                        p.Progression,
                        Company = p.Company != null ? new
                        {
                            p.Company.CompanyId,
                            CompanyName = p.Company.CompanyName
                        } : null,
                        CreatedBy = p.CreatedByNavigation != null ? new
                        {
                            p.CreatedByNavigation.UserId,
                            p.CreatedByNavigation.Email
                        } : null,
                        UpdatedBy = p.UpdatedByNavigation != null ? new
                        {
                            p.UpdatedByNavigation.UserId,
                            p.UpdatedByNavigation.Email
                        } : null,
                        ProjectMembers = p.ProjectMembers
                            .Where(pm => pm.IsDeleted != true)
                            .Select(pm => new
                            {
                                pm.Id,
                                pm.UserId,
                                pm.JoinedAt,
                                User = pm.User != null ? new
                                {
                                    pm.User.UserId,
                                    pm.User.Email,
                                    ProfilePhotoUrl = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                        pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).ProfilePhotoUrl : null,
                                    FirstName = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                        pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).FirstName : null,
                                    LastName = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                        pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).LastName : null
                                } : null
                            }).ToList(),
                        ProjectDocuments = p.ProjectDocuments
                            .Where(pd => pd.IsDeleted != true)
                            .Select(pd => new
                            {
                                pd.DocumentId,
                                pd.DocumentName,
                                pd.DocumentType,
                                pd.FilePath,
                                pd.FileSize,
                                pd.Description,
                                pd.Version,
                                pd.IsActive,
                                pd.CreatedAt,
                                UploadedBy = pd.UploadedByUser != null ? new
                                {
                                    pd.UploadedByUser.UserId,
                                    pd.UploadedByUser.Email,
                                    ProfilePhotoUrl = pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                        pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).ProfilePhotoUrl : null,
                                    FirstName = pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                        pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).FirstName : null,
                                    LastName = pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                        pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).LastName : null
                                } : null
                            })
                            .ToList(),
                        TaskLists = p.TaskLists
                            .Where(tl => tl.IsDeleted != true)
                            .OrderBy(tl => tl.ListOrder)
                            .Select(tl => new
                            {
                                tl.TaskListId,
                                tl.ListName,
                                tl.Description,
                                tl.ListOrder,
                                tl.Status,
                                tl.IsActive,
                                tl.CreatedAt,
                                tl.UpdatedAt,
                                tl.StartDate,
                                tl.EndDate,
                                Tasks = tl.Tasks
                                .Where(t => t.IsDeleted != true && (t.ParentTaskId == null || t.ParentTaskId == 0)).OrderBy(t => t.TaskOrder)
                                    .Select(t => new
                                    {
                                        t.TaskId,
                                        t.Title,
                                        t.Description,
                                        StatusName = t.StatusNavigation.Name,
                                        t.EndDate,
                                        t.StartDate,
                                        PriorityName = t.Priority != null ? t.Priority.Name : null,
                                        t.CompanyId,
                                        t.TaskListId,
                                        t.EstimatedHours,
                                        t.ActualHours,
                                        t.TaskOrder,
                                        t.IsActive,
                                        t.CreatedAt,
                                        AssignedTo = t.AssignedToNavigation != null ? new
                                        {
                                            t.AssignedToNavigation.UserId,
                                            t.AssignedToNavigation.Email,
                                            ProfilePhotoUrl = t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId) != null ?
                                                t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId).ProfilePhotoUrl : null,
                                            FirstName = t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId) != null ?
                                                t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId).FirstName : null,
                                            LastName = t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId) != null ?
                                                t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId).LastName : null
                                        } : null,
                                        Progression = t.Progression,
                                        // Added task documents here
                                        TaskDocuments = t.TaskDocuments
                                            .Where(td => td.IsDeleted != true)
                                            .Select(td => new
                                            {
                                                td.DocumentId,
                                                td.TaskId,
                                                td.DocumentName,
                                                td.FilePath,
                                                td.DocumentType,
                                                td.Description,
                                                td.CreatedAt,
                                                td.FileSize,
                                                td.IsActive,
                                                UploadedBy = td.UploadedByUser != null ? new
                                                {
                                                    td.UploadedByUser.UserId,
                                                    td.UploadedByUser.Email,
                                                    ProfilePhotoUrl = td.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId) != null ?
                                                        td.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId).ProfilePhotoUrl : null,
                                                    FirstName = td.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId) != null ?
                                                        td.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId).FirstName : null,
                                                    LastName = td.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId) != null ?
                                                        td.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId).LastName : null
                                                } : null
                                            })
                                            .ToList(),
                                        SubTasks = t.SubTasks
                                            .Where(st => !st.IsDeleted)
                                            .OrderBy(st => st.TaskOrder)
                                            .Select(st => new
                                            {
                                                st.TaskId,
                                                st.Title,
                                                st.Description,
                                                StatusName = st.StatusNavigation.Name,
                                                st.EndDate,
                                                st.StartDate,
                                                PriorityName = st.Priority != null ? st.Priority.Name : null,
                                                st.CompanyId,
                                                st.TaskListId,
                                                st.EstimatedHours,
                                                st.ActualHours,
                                                st.TaskOrder,
                                                st.IsActive,
                                                st.CreatedAt,
                                                st.ParentTaskId, // Include for reference
                                                AssignedTo = st.AssignedToNavigation != null ? new
                                                {
                                                    st.AssignedToNavigation.UserId,
                                                    st.AssignedToNavigation.Email,
                                                    ProfilePhotoUrl = st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == st.CompanyId).ProfilePhotoUrl,
                                                    FirstName = st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == st.CompanyId).FirstName,
                                                    LastName = st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == st.CompanyId).LastName
                                                } : null,
                                            })
                                            .ToList()
                                    })
                            .ToList(),

                            })
                    })
                    .FirstOrDefaultAsync();

                if (project == null)
                {
                    _logger.LogWarning($"Project with ID {id} not found or access denied for User ID: {currentUserId}.");
                    return NotFound(new ApiResponse<object>($"Project with ID {id} not found or you do not have permission to view it.", "Error", 404));
                }

                _logger.LogInformation($"Successfully retrieved project with ID: {id} for User ID: {currentUserId}.");
                return Ok(new ApiResponse<object>("Project retrieved successfully", "Success", 200, project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while fetching project with ID: {id} with role-based access.");
                return StatusCode(500, new ApiResponse<object>("An error occurred while retrieving the project", "Error", 500, null));
            }
        }


        // GET: api/Project/{id}/members
        [HttpGet("{id}/members")]
        [Authorize(Policy = "CanProjectMemberRead")] // Policy for reading project members
        public async Task<ActionResult<ApiResponse<IEnumerable<object>>>> GetProjectMembers(int id)
        {
            try
            {
                _logger.LogInformation($"Fetching members for project ID: {id}.");

                var currentUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();

                if (!currentUserId.HasValue || !currentCompanyId.HasValue)
                {
                    return Error<IEnumerable<object>>("Authentication information (UserId, CompanyId) is missing.", StatusCodes.Status401Unauthorized);
                }

                var project = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.ProjectId == id && p.IsDeleted != true);
                if (project == null)
                {
                    _logger.LogWarning($"Project with ID {id} not found for members fetch.");
                    return Error<IEnumerable<object>>($"Project with ID {id} not found.", StatusCodes.Status404NotFound);
                }

                bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);
                bool canReadAllInCompany = User.HasClaim("permission", Permissions.ProjectReadAll);

                if (!isGlobalAdmin)
                {
                    if (project.CompanyId != currentCompanyId.Value)
                    {
                        return Error<IEnumerable<object>>("You do not have permission to view members for this project.", StatusCodes.Status403Forbidden);
                    }

                    if (!canReadAllInCompany)
                    {
                        bool isMember = await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == id && pm.UserId == currentUserId.Value && pm.IsDeleted != true);
                        if (!isMember)
                        {
                            return Error<IEnumerable<object>>("You do not have permission to view members for this project.", StatusCodes.Status403Forbidden);
                        }
                    }
                }

                var membersEntity = await _context.ProjectMembers
                    .Where(pm => pm.ProjectId == id && pm.IsDeleted != true)
                    .Include(pm => pm.User)
                        .ThenInclude(u => u.CompanyUserUsers)
                    .ToListAsync();

                var members = membersEntity.Select(pm => new
                {
                    pm.Id,
                    pm.UserId,
                    pm.JoinedAt,
                    pm.AddedBy,
                    User = pm.User != null ? new
                    {
                        pm.User.UserId,
                        pm.User.Email,
                        ProfilePhotoUrl = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId) != null ?
                                            pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId).ProfilePhotoUrl : null,
                        FirstName = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId) != null ?
                                    pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId).FirstName : null,
                        LastName = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId) != null ?
                                    pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId).LastName : null,
                        EmployeeCode = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId) != null ?
                                        pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId).EmployeeCode : null
                    } : null
                }).ToList();

                _logger.LogInformation($"Retrieved {members.Count} members for project ID: {id}.");
                return Success<IEnumerable<object>>("Members retrieved successfully.", members);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while fetching members for project ID: {id}");
                return Error<IEnumerable<object>>("An error occurred while retrieving project members", StatusCodes.Status500InternalServerError);
            }
        }

        // POST: api/Project
        [HttpPost]
        [Authorize(Policy = "CanProjectCreate")] // Policy for creating projects
        public async Task<ActionResult<ApiResponse<object>>> CreateProject([FromBody] ProjectInputDto dto)
        {
            try
            {
                _logger.LogInformation("Attempting to create a new project.");

                var createdByUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();

                if (!createdByUserId.HasValue || !currentCompanyId.HasValue)
                {
                    return Unauthorized(new ApiResponse<object>("Authentication information (UserId, CompanyId) is missing.", "Error", StatusCodes.Status401Unauthorized));
                }

                bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);
                if (!isGlobalAdmin && dto.CompanyId != currentCompanyId.Value)
                {
                    _logger.LogWarning($"User (User ID: {createdByUserId}, Company ID: {currentCompanyId}) attempted to create project for a different company (ID: {dto.CompanyId}).");
                    return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>("You do not have permission to create projects for another company.", "Error", StatusCodes.Status403Forbidden));
                }

                var companyExists = await _context.Companies.AnyAsync(c => c.CompanyId == dto.CompanyId);
                if (!companyExists)
                {
                    _logger.LogWarning($"Attempt to create project with non-existent CompanyId: {dto.CompanyId} by User ID: {createdByUserId}");
                    return BadRequest(new ApiResponse<object>($"Company with ID {dto.CompanyId} does not exist.", "Error", StatusCodes.Status400BadRequest));
                }

                // --- START: MODIFIED LOGIC TO RESOLVE STATUS NAME TO ID ---
                int? projectStatusId = null;
                if (!string.IsNullOrWhiteSpace(dto.Status))
                {
                    var companyStatus = await _context.CompanyTaskStatuses
                        .FirstOrDefaultAsync(s => s.CompanyId == dto.CompanyId &&
                                                     s.Name.ToLower() == dto.Status.ToLower());

                    if (companyStatus == null)
                    {
                        return BadRequest(new ApiResponse<object>($"Status '{dto.Status}' not found for company ID {dto.CompanyId}. Please provide a valid status name.", "Error", StatusCodes.Status400BadRequest));
                    }
                    projectStatusId = companyStatus.Id;
                }
                // --- END: MODIFIED LOGIC ---

                var project = new Project
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    Status = dto.Status ?? "Planned",
                    StatusId = projectStatusId,
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate,
                    CompanyId = dto.CompanyId,
                    IsDeleted = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = createdByUserId
                };

                // Automatically add the creator as a project member.
                project.ProjectMembers.Add(new ProjectMember
                {
                    UserId = createdByUserId.Value,
                    AddedBy = createdByUserId.Value,
                    JoinedAt = DateOnly.FromDateTime(DateTime.UtcNow),
                    IsDeleted = false
                });

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();
                
                // Fire-and-forget background task for notifications and emails
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await _taskNotificationService.SendProjectCreatedNotificationAsync(
                            project.ProjectId,
                            createdByUserId.Value
                        );

                        await _projectEmailService.SendProjectCreatedNotificationAsync(
                            project.ProjectId,
                            createdByUserId.Value
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send project created notifications in background for Project ID: {ProjectId}", project.ProjectId);
                    }
                });

                _logger.LogInformation($"Project created successfully with ID: {project.ProjectId} by user {createdByUserId}.");
                return CreatedAtAction(
                    nameof(GetProject),
                    new { id = project.ProjectId },
                    new ApiResponse<object>("Project created successfully", "Success", StatusCodes.Status201Created, new { projectId = project.ProjectId })
                );
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                _logger.LogError(dbEx, "DbUpdateException occurred while creating project. Inner Exception: {InnerMessage}", innerException?.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>("An error occurred while saving the project to the database.", "Error", StatusCodes.Status500InternalServerError, new { databaseError = dbEx.Message, innerError = innerException?.Message }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating project.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>("An unexpected error occurred while creating the project.", "Error", StatusCodes.Status500InternalServerError, null));
            }
        }

        // PUT: api/Project/5
        [HttpPut("{id}")]
        [Authorize(Policy = "CanProjectUpdate")] // Policy for updating projects
        public async Task<ActionResult<ApiResponse<object>>> UpdateProject(int id, [FromBody] ProjectUpdateDto dto)
        {
            try
            {
                Console.WriteLine($"[DEBUG] UpdateProject called for ID: {id}");
                _logger.LogInformation($"Attempting to update project with ID: {id}");

                var updatedByUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();

                if (!updatedByUserId.HasValue || !currentCompanyId.HasValue)
                {
                    Console.WriteLine("[DEBUG] Blocked: Authentication info missing.");
                    return Unauthorized(new ApiResponse<object>("Authentication information (UserId, CompanyId) is missing.", "Error", StatusCodes.Status401Unauthorized));
                }

                var existingProject = await _context.Projects
                    .FirstOrDefaultAsync(p => p.ProjectId == id && p.IsDeleted != true);

                if (existingProject == null)
                {
                    Console.WriteLine($"[DEBUG] Blocked: Project {id} not found.");
                    _logger.LogWarning($"Project with ID {id} not found for update by User ID: {updatedByUserId}");
                    return NotFound(new ApiResponse<object>($"Project with ID {id} not found", "Error", StatusCodes.Status404NotFound));
                }

                bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);

                // Authorization check for update
                if (isGlobalAdmin)
                {
                    Console.WriteLine($"[DEBUG] Auth: Global Admin access granted for User: {updatedByUserId}");
                    _logger.LogInformation($"User with global access (User ID: {updatedByUserId}) is updating project ID: {id}.");
                }
                else if (existingProject.CompanyId == currentCompanyId.Value)
                {
                    Console.WriteLine($"[DEBUG] Auth: Company match granted (Company: {currentCompanyId.Value})");
                    _logger.LogInformation($"User (User ID: {updatedByUserId}) is updating project ID: {id} within their company.");
                }
                else
                {
                    Console.WriteLine($"[DEBUG] Blocked: Unauthorized company access. Project Co: {existingProject.CompanyId}, User Co: {currentCompanyId.Value}");
                    _logger.LogWarning($"User ID: {updatedByUserId} attempted to update project ID: {id} which belongs to a different company.");
                    return StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>("You do not have permission to update projects outside your company.", "Error", StatusCodes.Status403Forbidden));
                }

                // --- START: MODIFIED LOGIC TO RESOLVE STATUS NAME TO ID FOR UPDATE ---
                int? projectStatusId = null;
                if (!string.IsNullOrWhiteSpace(dto.Status))
                {
                    Console.WriteLine($"[DEBUG] Resolving status: '{dto.Status}' for Company: {existingProject.CompanyId}");
                    var companyStatus = await _context.CompanyTaskStatuses
                        .FirstOrDefaultAsync(s => s.CompanyId == existingProject.CompanyId &&
                                                     s.Name.ToLower() == dto.Status.ToLower());

                    if (companyStatus == null)
                    {
                        Console.WriteLine($"[DEBUG] Blocked: Status '{dto.Status}' NOT FOUND in CompanyTaskStatuses for Company: {existingProject.CompanyId}");
                        return BadRequest(new ApiResponse<object>($"Status '{dto.Status}' not found for project's company ID {existingProject.CompanyId}. Please provide a valid status name.", "Error", StatusCodes.Status400BadRequest));
                    }
                    projectStatusId = companyStatus.Id;
                    Console.WriteLine($"[DEBUG] Status resolved successfully to ID: {projectStatusId}");
                }
                // --- END: MODIFIED LOGIC ---

                // Update allowed fields (preserve existing if DTO field is null)
                existingProject.Name = dto.Name ?? existingProject.Name;
                existingProject.Description = dto.Description ?? existingProject.Description;
                existingProject.Status = dto.Status ?? existingProject.Status;
                existingProject.StatusId = projectStatusId ?? existingProject.StatusId;
                existingProject.StartDate = dto.StartDate ?? existingProject.StartDate;
                existingProject.EndDate = dto.EndDate ?? existingProject.EndDate;
                existingProject.UpdatedAt = DateTime.UtcNow;
                existingProject.UpdatedBy = updatedByUserId;

                Console.WriteLine("[DEBUG] Saving changes to database...");
                await _context.SaveChangesAsync();
                Console.WriteLine("[DEBUG] Update successful.");

                _logger.LogInformation($"Project with ID {id} updated successfully by User ID: {updatedByUserId}.");
                return Ok(new ApiResponse<object>("Project updated successfully", "Success", StatusCodes.Status200OK, null));
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                _logger.LogError(dbEx, "DbUpdateException occurred while updating project with ID: {ProjectId}. Inner Exception: {InnerMessage}", id, innerException?.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>("An error occurred while saving project updates to the database.", "Error", StatusCodes.Status500InternalServerError, new { databaseError = dbEx.Message, innerError = innerException?.Message }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while updating project with ID: {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>("An unexpected error occurred while updating the project.", "Error", StatusCodes.Status500InternalServerError, null));
            }
        }

        // DELETE: api/Project/5
        [HttpDelete("{id}")]
        [Authorize(Policy = "CanProjectDelete")] // Policy for deleting projects
        public async Task<ActionResult<ApiResponse<object>>> DeleteProject(int id)
        {
            try
            {
                _logger.LogInformation($"Attempting to soft-delete project with ID: {id}");

                var updatedByUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();

                if (!updatedByUserId.HasValue || !currentCompanyId.HasValue)
                {
                    return Unauthorized(new ApiResponse<object>("Authentication information (UserId, CompanyId) is missing.", "Error", 401));
                }

                var project = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectId == id && p.IsDeleted != true);
                if (project == null)
                {
                    _logger.LogWarning($"Project with ID {id} not found for deletion by User ID: {updatedByUserId}");
                    return NotFound(new ApiResponse<object>($"Project with ID {id} not found", "Error", 404));
                }

                bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);

                if (isGlobalAdmin)
                {
                    _logger.LogInformation($"User with global access (User ID: {updatedByUserId}) is soft-deleting project ID: {id}.");
                }
                else if (project.CompanyId == currentCompanyId.Value)
                {
                    _logger.LogInformation($"User (User ID: {updatedByUserId}) is soft-deleting project ID: {id} within their company.");
                }
                else
                {
                    _logger.LogWarning($"User ID: {updatedByUserId} attempted to soft-delete project ID: {id} which belongs to a different company.");
                    return StatusCode(403, new ApiResponse<object>("You do not have permission to delete projects outside your company.", "Error", 403));
                }

                project.IsDeleted = true; // Soft delete
                project.UpdatedAt = DateTime.UtcNow;
                project.UpdatedBy = updatedByUserId;

                await _context.SaveChangesAsync();
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        //await _projectEmailService.SendProjectDeletedNotificationAsync(
                        //    id,
                        //    projectName,
                        //    updatedByUserId.Value,
                        //    "Project was deleted by authorized user"
                        //);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send project deleted email notification");
                    }
                });

                _logger.LogInformation($"Project with ID {id} soft-deleted successfully by User ID: {updatedByUserId}.");
                return Ok(new ApiResponse<object>("Project soft-deleted successfully.", "Success", 200, null));
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                _logger.LogError(dbEx, "DbUpdateException occurred while deleting project with ID: {ProjectId}. Inner Exception: {InnerMessage}", id, innerException?.Message);
                return StatusCode(500, new ApiResponse<object>("An error occurred while saving project deletion to the database.", "Error", 500, new { databaseError = dbEx.Message, innerError = innerException?.Message }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while deleting project with ID: {id}");
                return StatusCode(500, new ApiResponse<object>("An unexpected error occurred while deleting the project.", "Error", 500, null));
            }
        }

        // POST: api/Project/{projectId}/members
        [HttpPost("{projectId}/members")]
        [Authorize(Policy = "CanProjectMemberCreate")] // Policy for adding project members
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> AddProjectMember(int projectId, [FromBody] AddProjectMemberDto dto)
        {
            try
            {
                _logger.LogInformation($"Attempting to add member {dto.UserId} to project ID: {projectId}.");

                var currentUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();
                if (!currentUserId.HasValue || !currentCompanyId.HasValue)
                {
                    return Error<object>("Authentication information (UserId, CompanyId) is missing.", StatusCodes.Status401Unauthorized);
                }

                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.IsDeleted != true);
                if (project == null)
                {
                    return Error<object>($"Project with ID {projectId} not found.", StatusCodes.Status404NotFound);
                }

                bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);

                if (!isGlobalAdmin)
                {
                    if (project.CompanyId != currentCompanyId.Value)
                    {
                        return Error<object>("You do not have permission to add members to projects outside your company.", StatusCodes.Status403Forbidden);
                    }
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == dto.UserId && !u.IsDeleted);
                if (user == null)
                {
                    return Error<object>($"User with ID {dto.UserId} not found.", StatusCodes.Status404NotFound);
                }

                if (!isGlobalAdmin && user.CompanyId != currentCompanyId.Value)
                {
                    return Error<object>("Cannot add a user from another company.", StatusCodes.Status403Forbidden);
                }

                var existingMember = await _context.ProjectMembers
                    .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == dto.UserId && pm.IsDeleted != true);
                if (existingMember != null)
                {
                    return Error<object>("User is already a member of the project.", StatusCodes.Status400BadRequest, "fail");
                }

                var member = new ProjectMember
                {
                    ProjectId = projectId,
                    UserId = dto.UserId,
                    AddedBy = currentUserId,
                    JoinedAt = DateOnly.FromDateTime(DateTime.UtcNow),
                    IsDeleted = false
                };

                _context.ProjectMembers.Add(member);
                await _context.SaveChangesAsync();

                // --- NOTIFICATION LOGIC ---
                // Fire-and-forget background task for notifications and emails
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await _taskNotificationService.SendProjectMemberAddedNotificationAsync(
                            projectId,
                            dto.UserId,
                            currentUserId.Value
                        );

                        await _projectEmailService.SendProjectMemberAddedEmailAsync(
                            projectId,
                            dto.UserId,
                            currentUserId.Value
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send member added notifications in background for User: {UserId} to Project: {ProjectId}", dto.UserId, projectId);
                    }
                });

                return Success<object>("Member added to project successfully.", null, StatusCodes.Status201Created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while adding member to project ID: {projectId}");
                return Error<object>("An error occurred while adding the member to the project.", StatusCodes.Status500InternalServerError);
            }
        }

        // DELETE: api/Project/{projectId}/members/{userId}
        [HttpDelete("{projectId}/members/{userId}")]
        [Authorize(Policy = "CanProjectMemberDelete")] // Policy for deleting project members
        public async Task<ActionResult<ApiResponse<object>>> RemoveProjectMember(int projectId, int userId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();
                if (!currentUserId.HasValue || !currentCompanyId.HasValue)
                {
                    return Error<object>("Authentication information (UserId, CompanyId) is missing.", StatusCodes.Status401Unauthorized);
                }

                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.IsDeleted != true);
                if (project == null)
                {
                    return Error<object>($"Project with ID {projectId} not found.", StatusCodes.Status404NotFound);
                }

                bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);

                if (!isGlobalAdmin)
                {
                    if (project.CompanyId != currentCompanyId.Value)
                    {
                        return Error<object>("You do not have permission to remove project members from projects outside your company.", StatusCodes.Status403Forbidden);
                    }
                }

                var member = await _context.ProjectMembers
                    .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsDeleted != true);
                if (member == null)
                {
                    return Error<object>("Member not found in this project.", StatusCodes.Status404NotFound);
                }

                member.IsDeleted = true;
                member.AddedBy = currentUserId; // Can be a "RemovedBy" field if you add one

                await _context.SaveChangesAsync();
                //ADD NOTIFICATION LOGIC
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await _taskNotificationService.SendProjectMemberRemovedNotificationAsync(
                            projectId,
                            userId,
                            currentUserId.Value
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send project member removed notification");
                    }
                });

                return Success<object>("Member removed from project successfully.", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while removing member from project ID: {projectId}");
                return Error<object>("An error occurred while removing the member from the project.", StatusCodes.Status500InternalServerError);
            }
        }

        // POST: api/Project/{projectId}/documents
        [HttpPost("{projectId}/documents")]
        [Authorize(Policy = "CanProjectDocumentCreate")] // Policy for creating project documents
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ApiResponse<ProjectDocument>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ProjectDocument>>> UploadProjectDocument(
            int projectId,
            [FromForm] ProjectDocumentUploadDto dto)
        {
            try
            {
                _logger.LogInformation($"Attempting to upload document for project ID: {projectId}. Document Name: {dto.DocumentName}");

                var currentUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();

                if (!currentUserId.HasValue || !currentCompanyId.HasValue)
                {
                    return Error<ProjectDocument>("Authentication information (UserId, CompanyId) is missing.", StatusCodes.Status401Unauthorized);
                }

                var project = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId && p.IsDeleted != true);
                if (project == null)
                {
                    return Error<ProjectDocument>($"Project with ID {projectId} not found.", StatusCodes.Status404NotFound);
                }

                bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);
                bool canReadAllInCompany = User.HasClaim("permission", Permissions.ProjectReadAll);

                // Check if the current user has permission to upload documents for this project
                bool hasPermission = false;
                if (isGlobalAdmin)
                {
                    hasPermission = true;
                }
                else if (project.CompanyId == currentCompanyId.Value)
                {
                    if (canReadAllInCompany)
                    {
                        hasPermission = true;
                    }
                    else
                    {
                        bool isProjectMember = await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId.Value && pm.IsDeleted != true);
                        if (isProjectMember)
                        {
                            hasPermission = true;
                        }
                    }
                }

                if (!hasPermission)
                {
                    _logger.LogWarning($"User ID: {currentUserId} attempted to upload document for project ID: {projectId}. Access denied.");
                    return Error<ProjectDocument>("You do not have permission to upload documents for this project.", StatusCodes.Status403Forbidden);
                }

                var existingDocumentCount = await _context.ProjectDocuments.CountAsync(pd => pd.ProjectId == projectId && pd.IsDeleted != true);
                if (existingDocumentCount >= 5)
                {
                    _logger.LogWarning($"Attempt to upload document to project ID: {projectId} failed. Document limit of 5 reached.");
                    return Error<ProjectDocument>($"Cannot upload more documents. This project has reached its limit of 5 documents.", StatusCodes.Status400BadRequest);
                }

                // Validate DTO
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for project document upload: {Errors}", ModelState);
                    return BadRequest(new ApiResponse<object>("Invalid document data provided.", "Error", StatusCodes.Status400BadRequest, ModelState));
                }

                if (dto.File == null || dto.File.Length == 0)
                {
                    return Error<ProjectDocument>("No file uploaded or file is empty.", StatusCodes.Status400BadRequest);
                }

                // Use the UploadHandler service to handle the file saving and DB entry
                var uploadedDocument = await _uploadHandler.UploadProjectDocumentAsync(
                    currentUserId.Value,
                    currentCompanyId.Value,
                    projectId,
                    dto.File,
                    dto.DocumentName,
                    dto.Description,
                    dto.Version
                );

                _logger.LogInformation($"Document '{uploadedDocument.DocumentName}' (ID: {uploadedDocument.DocumentId}) uploaded successfully for project ID: {projectId}.");
                return Success("Project document uploaded successfully.", uploadedDocument, StatusCodes.Status201Created);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Validation error during project document upload for project ID {ProjectId}.", projectId);
                return Error<ProjectDocument>(ex.Message, StatusCodes.Status400BadRequest);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Argument error during project document upload for project ID {ProjectId}.", projectId);
                return Error<ProjectDocument>(ex.Message, StatusCodes.Status400BadRequest);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "File system error during project document upload for project ID {ProjectId}.", projectId);
                return Error<ProjectDocument>("A file system error occurred during upload. Please try again.", StatusCodes.Status500InternalServerError, ex.Message);
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                _logger.LogError(dbEx, "Database error during project document upload for project ID {ProjectId}. Inner Exception: {InnerMessage}", projectId, innerException?.Message);
                return Error<ProjectDocument>("A database error occurred while saving document information.", StatusCodes.Status500InternalServerError, new { databaseError = dbEx.Message, innerError = innerException?.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while uploading document for project ID: {projectId}");
                return Error<ProjectDocument>("An unexpected error occurred while uploading the project document.", StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("{projectId}/documents/{documentId}")]
        [Authorize(Policy = "CanProjectDocumentDelete")] // Policy for deleting project documents
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> DeleteProjectDocument(int projectId, int documentId)
        {
            try
            {
                _logger.LogInformation($"Attempting to soft-delete document ID: {documentId} for project ID: {projectId}.");

                var currentUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();

                if (!currentUserId.HasValue || !currentCompanyId.HasValue)
                {
                    return Error<object>("Authentication information (UserId, CompanyId) is missing.", StatusCodes.Status401Unauthorized);
                }

                var document = await _context.ProjectDocuments
                    .Include(pd => pd.Project)
                    .FirstOrDefaultAsync(pd => pd.DocumentId == documentId && pd.ProjectId == projectId && pd.IsDeleted != true);

                if (document == null)
                {
                    _logger.LogWarning($"Document ID {documentId} not found for project ID {projectId} or already deleted.");
                    return Error<object>($"Document with ID {documentId} not found for project {projectId} or already deleted.", StatusCodes.Status404NotFound);
                }

                bool isGlobalAdmin = User.HasClaim("permission", Permissions.CompanyReadAll);
                bool canReadAllInCompany = User.HasClaim("permission", Permissions.ProjectReadAll);

                // Authorization checks
                bool hasPermission = false;
                if (isGlobalAdmin)
                {
                    hasPermission = true;
                }
                else if (document.Project?.CompanyId == currentCompanyId.Value)
                {
                    if (canReadAllInCompany)
                    {
                        hasPermission = true;
                    }
                    else if (document.UploadedBy == currentUserId.Value &&
                             await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId.Value && pm.IsDeleted != true))
                    {
                        hasPermission = true;
                    }
                }

                if (!hasPermission)
                {
                    _logger.LogWarning($"User ID: {currentUserId} attempted to delete document ID: {documentId} for project ID: {projectId}. Access denied.");
                    return Error<object>("You do not have permission to delete this project document.", StatusCodes.Status403Forbidden);
                }

                document.IsDeleted = true;
                document.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Document ID: {documentId} for project ID: {projectId} soft-deleted successfully by User ID: {currentUserId}.");
                return Success<object>("Project document soft-deleted successfully.", null, StatusCodes.Status200OK);
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                _logger.LogError(dbEx, "DbUpdateException occurred while deleting project document ID: {DocumentId} for project ID: {ProjectId}. Inner Exception: {InnerMessage}", documentId, projectId, innerException?.Message);
                return Error<object>("A database error occurred while soft deleting the document.", StatusCodes.Status500InternalServerError, new { databaseError = dbEx.Message, innerError = innerException?.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while deleting project document ID: {documentId} for project ID: {projectId}.");
                return Error<object>("An unexpected error occurred while deleting the project document.", StatusCodes.Status500InternalServerError);
            }
        }
    }
}