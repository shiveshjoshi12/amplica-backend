using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using BizfreeApp.Data;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs; // Ensure this namespace is correctly referenced
using System.Security.Claims;
using System.Linq.Expressions; // Needed for dynamic sorting
using Microsoft.AspNetCore.Http; // Required for StatusCodes

namespace BizfreeApp.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class ProjectTimesheetController : ControllerBase
    {
        private readonly Data.ApplicationDbContext _context;
        private readonly ILogger<ProjectTimesheetController> _logger;

        public ProjectTimesheetController(Data.ApplicationDbContext context, ILogger<ProjectTimesheetController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Helper method for consistent success responses.
        // This method correctly returns IActionResult.
        // The type parameter <T> allows flexibility in the data payload.
        private IActionResult Success<T>(string message, T? data, int statusCode = StatusCodes.Status200OK)
        {
            return Ok(new ApiResponse<T>(message, "Success", statusCode, data));
        }

        // Helper method for consistent error responses.
        // This method correctly returns IActionResult.
        // The type parameter <T> allows flexibility in the data payload, typically default(T) for errors.
        private IActionResult Error<T>(string message, int statusCode, object? errorDetails = null)
        {
            _logger.LogError("API Error: {Message} - Status Code: {StatusCode} - Details: {Details}", message, statusCode, errorDetails);
            return StatusCode(statusCode, new ApiResponse<T>(message, "Error", statusCode, data: default(T), errorDetails: errorDetails));
        }

        // Helper method to get UserId from claims for reusability and null handling.
        // Returns null if claim is missing or unparsable.
        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogWarning("UserId claim not found or could not be parsed.");
                return null;
            }
            return userId;
        }


        // --- Endpoint 1: GetMyProjectsAndTasks with Paging, Sorting, Filtering ---
        /// <summary>
        /// Retrieves projects and tasks assigned to the current authenticated user, with optional filtering, sorting, and pagination.
        /// </summary>
        /// <param name="page">The page number for pagination (default: 1).</param>
        /// <param name="pageSize">The number of items per page (default: 10).</param>
        /// <param name="sortBy">The field to sort by (e.g., "task_name", "start_date").</param>
        /// <param name="sortOrder">The sort order ("asc" for ascending, "desc" for descending, default: "asc").</param>
        /// <param name="searchKeyword">A keyword to filter task name, project name, or code.</param>
        /// <returns>A paged list of projects with their assigned tasks.</returns>
        [HttpGet("my-projects-tasks")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMyProjectsAndTasks(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string sortOrder = "asc",
            [FromQuery] string? searchKeyword = null)
        {
            try
            {
                int? optionalUserId = GetCurrentUserId();
                if (!optionalUserId.HasValue)
                {
                    return Unauthorized(new ApiResponse<object>("Invalid token. User ID is missing or invalid.", "Error", StatusCodes.Status401Unauthorized));
                }
                int loggedInUserId = optionalUserId.Value;

                _logger.LogInformation("Fetching projects and tasks for user {UserId} with page: {Page}, pageSize: {PageSize}, sortBy: {SortBy}, sortOrder: {SortOrder}, searchKeyword: {SearchKeyword}", loggedInUserId, page, pageSize, sortBy, sortOrder, searchKeyword);

                var query = (from task in _context.Tasks
                             where task.AssignedTo == loggedInUserId && !task.IsDeleted
                             join project in _context.Projects on task.ProjectId equals project.ProjectId
                             join companyUser in _context.CompanyUsers on task.AssignedTo equals companyUser.UserId
                             select new
                             {
                                 project_id = project.ProjectId,
                                 project_name = project.Name,
                                 code = project.Code,
                                 task_id = task.TaskId,
                                 task_name = task.Title,
                                 owner = companyUser.FirstName + " " + companyUser.LastName,
                                 start_date = task.StartDate,
                                 end_date = task.EndDate,
                                 start_time = task.StartTime,
                                 end_time = task.EndTime,
                                 daily_log = task.DailyLog,
                                 task_code = project.Code + "-" + task.TaskId.ToString(),
                                 IsActive = task.IsActive
                             });

                // --- Apply Filtering ---
                if (!string.IsNullOrEmpty(searchKeyword))
                {
                    string lowerSearch = searchKeyword.ToLower();
                    query = query.Where(t => (t.task_name != null && t.task_name.ToLower().Contains(lowerSearch)) ||
                                             (t.project_name != null && t.project_name.ToLower().Contains(lowerSearch)) ||
                                             (t.code != null && t.code.ToLower().Contains(lowerSearch)));
                }

                var totalTasksCount = await query.CountAsync();

                // --- Apply Sorting ---
                switch (sortBy?.ToLower())
                {
                    case "task_name":
                        query = sortOrder.ToLower() == "desc" ? query.OrderByDescending(t => t.task_name) : query.OrderBy(t => t.task_name);
                        break;
                    case "start_date":
                        query = sortOrder.ToLower() == "desc" ? query.OrderByDescending(t => t.start_date) : query.OrderBy(t => t.start_date);
                        break;
                    case "project_name":
                        query = sortOrder.ToLower() == "desc" ? query.OrderByDescending(t => t.project_name) : query.OrderBy(t => t.project_name);
                        break;
                    case "code":
                        query = sortOrder.ToLower() == "desc" ? query.OrderByDescending(t => t.code) : query.OrderBy(t => t.code);
                        break;
                    default:
                        query = query.OrderBy(t => t.project_name).ThenBy(t => t.start_date);
                        break;
                }

                // --- Apply Pagination ---
                var pagedQuery = query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize);

                var userProjectTasks = await pagedQuery.ToListAsync();

                if (!userProjectTasks.Any())
                {
                    return NotFound(new ApiResponse<object>("No tasks found for this user matching the criteria.", "Error", StatusCodes.Status404NotFound));
                }

                var groupedResult = userProjectTasks
                    .GroupBy(t => new { t.project_id, t.project_name, t.code })
                    .Select(g => new
                    {
                        project_id = g.Key.project_id,
                        project_name = g.Key.project_name,
                        code = g.Key.code,
                        tasks = g.Select(t => new
                        {
                            t.task_id,
                            t.task_name,
                            t.owner,
                            t.start_date,
                            t.end_date,
                            t.start_time,
                            t.end_time,
                            t.daily_log,
                            code = t.task_code
                        }).ToList()
                    })
                    .ToList();

                var totalPages = (int)Math.Ceiling((double)totalTasksCount / pageSize);

                var pagedResponse = new PagedResult<object>
                {
                    Items = groupedResult,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    TotalCount = groupedResult.Count(),
                    TotalTasks = totalTasksCount,
                    SortBy = sortBy,
                    SortOrder = sortOrder,
                    SearchKeyword = searchKeyword
                };

                // FIX: Explicitly specify the type argument for Success.
                return Success<PagedResult<object>>("Projects and tasks retrieved successfully.", pagedResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the user's projects and tasks.");
                return Error<PagedResult<object>>("An error occurred while fetching the user's projects and tasks.", StatusCodes.Status500InternalServerError, ex.Message);
            }
        }


        // --- Endpoint 2: GetProjectTimesheets with Paging, Sorting, Filtering ---
        /// <summary>
        /// Retrieves timesheet data for a specific project and user, with optional filtering, sorting, and pagination.
        /// </summary>
        /// <param name="projectId">The ID of the project.</param>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="page">The page number for pagination (default: 1).</param>
        /// <param name="pageSize">The number of items per page (default: 10).</param>
        /// <param name="sortBy">The field to sort by (e.g., "task_name", "start_date").</param>
        /// <param name="sortOrder">The sort order ("asc" for ascending, "desc" for descending, default: "asc").</param>
        /// <param name="searchKeyword">A keyword to filter task name, project name, or code.</param>
        /// <returns>A paged list of timesheet tasks.</returns>
        [HttpGet("{projectId}/timesheets/{userId}")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetProjectTimesheets(
            int projectId,
            int userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string sortOrder = "asc",
            [FromQuery] string? searchKeyword = null)
        {
            try
            {
                int? optionalLoggedInUserId = GetCurrentUserId();
                if (!optionalLoggedInUserId.HasValue)
                {
                    return Unauthorized(new ApiResponse<object>("Authentication information is missing.", "Error", StatusCodes.Status401Unauthorized));
                }
                // int loggedInUserId = optionalLoggedInUserId.Value; // This is only used for the optional security check below.

                _logger.LogInformation("Fetching timesheet data for ProjectId {ProjectId} and UserId {UserId} with page: {Page}, pageSize: {PageSize}, sortBy: {SortBy}, sortOrder: {SortOrder}, searchKeyword: {SearchKeyword}", projectId, userId, page, pageSize, sortBy, sortOrder, searchKeyword);


                var query = (from task in _context.Tasks
                             where task.ProjectId == projectId &&
                                   task.AssignedTo == userId &&
                                   !task.IsDeleted
                             join project in _context.Projects
                                 on task.ProjectId equals project.ProjectId
                             join companyUser in _context.CompanyUsers
                                 on task.AssignedTo equals companyUser.UserId
                             select new
                             {
                                 task_id = task.TaskId,
                                 task_name = task.Title,
                                 project_id = task.ProjectId,
                                 project_name = project.Name,
                                 owner = companyUser.FirstName + " " + companyUser.LastName,
                                 start_date = task.StartDate,
                                 end_date = task.EndDate,
                                 start_time = task.StartTime,
                                 end_time = task.EndTime,
                                 daily_log = task.DailyLog,
                                 code = project.Code + "-" + task.TaskId.ToString(),
                                 IsActive = task.IsActive
                             });

                // --- Apply Filtering ---
                if (!string.IsNullOrEmpty(searchKeyword))
                {
                    string lowerSearch = searchKeyword.ToLower();
                    query = query.Where(t => (t.task_name != null && t.task_name.ToLower().Contains(lowerSearch)) ||
                                             (t.project_name != null && t.project_name.ToLower().Contains(lowerSearch)) ||
                                             (t.code != null && t.code.ToLower().Contains(lowerSearch)));
                }

                var totalRecords = await query.CountAsync();

                // --- Apply Sorting ---
                switch (sortBy?.ToLower())
                {
                    case "task_name":
                        query = sortOrder.ToLower() == "desc" ? query.OrderByDescending(t => t.task_name) : query.OrderBy(t => t.task_name);
                        break;
                    case "start_date":
                        query = sortOrder.ToLower() == "desc" ? query.OrderByDescending(t => t.start_date) : query.OrderBy(t => t.start_date);
                        break;
                    case "project_name":
                        query = sortOrder.ToLower() == "desc" ? query.OrderByDescending(t => t.project_name) : query.OrderBy(t => t.project_name);
                        break;
                    case "code":
                        query = sortOrder.ToLower() == "desc" ? query.OrderByDescending(t => t.code) : query.OrderBy(t => t.code);
                        break;
                    default:
                        query = query.OrderBy(t => t.start_date);
                        break;
                }

                // --- Apply Pagination ---
                var pagedData = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                if (!pagedData.Any())
                {
                    return NotFound(new ApiResponse<object>("No timesheet data found for the given project and user matching the criteria.", "Error", StatusCodes.Status404NotFound));
                }

                var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

                var pagedResponse = new PagedResult<object>
                {
                    Items = pagedData,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    TotalCount = totalRecords,
                    TotalTasks = totalRecords,
                    SortBy = sortBy,
                    SortOrder = sortOrder,
                    SearchKeyword = searchKeyword
                };

                // FIX: Explicitly specify the type argument for Success.
                return Success<PagedResult<object>>("Timesheet data retrieved successfully.", pagedResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching timesheet data.");
                return Error<PagedResult<object>>("An error occurred while processing your request.", StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // --- Endpoint 3: CreateTaskForTimesheet (Updated for AssignedTo and Status Codes) ---
        /// <summary>
        /// Creates a new task for timesheet tracking within a specific project, assigned to the current user.
        /// </summary>
        /// <param name="projectId">The ID of the project the task belongs to.</param>
        /// <param name="dto">The DTO containing information for the new task.</param>
        /// <returns>An ActionResult indicating the result of the creation.</returns>
        [HttpPost("timesheets/project/{projectId}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateTaskForTimesheet(int projectId, TimesheetTaskCreateDto dto)
        {
            int? optionalUserId = GetCurrentUserId();
            if (!optionalUserId.HasValue)
            {
                return Unauthorized(new ApiResponse<object>("Invalid token. User ID is missing or invalid.", "Error", StatusCodes.Status401Unauthorized));
            }
            int loggedInUserId = optionalUserId.Value;

            var projectExists = await _context.Projects.AnyAsync(p => p.ProjectId == projectId);
            if (!projectExists)
            {
                return NotFound(new ApiResponse<object>($"Project with ID {projectId} does not exist.", "Error", StatusCodes.Status404NotFound));
            }

            var now = DateTimeOffset.UtcNow;

            var newTask = new Models.Task
            {
                ProjectId = projectId,
                Title = dto.TaskName,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                DailyLog = dto.DailyLog,
                AssignedTo = loggedInUserId,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
                IsDeleted = false
            };

            _context.Tasks.Add(newTask);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully created timesheet task {TaskId} for ProjectId {ProjectId}, AssignedTo {AssignedTo}.", newTask.TaskId, projectId, loggedInUserId);

                // FIX: Explicitly specify 'object' for the anonymous type in Success call.
                return Success<object>("Timesheet task created successfully.", new { task_id = newTask.TaskId }, StatusCodes.Status200OK);
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                _logger.LogError(dbEx, "Database error creating timesheet task for ProjectId {ProjectId}, UserId {UserId}. Inner exception: {InnerExceptionMessage}", projectId, loggedInUserId, innerException?.Message);
                return Error<object>("A database error occurred while creating the task. Please check foreign key constraints (e.g., ProjectId, AssignedTo).", StatusCodes.Status500InternalServerError, new { databaseError = dbEx.Message, innerError = innerException?.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating the timesheet task for ProjectId {ProjectId}, UserId {UserId}.", projectId, loggedInUserId);
                return Error<object>("An unexpected error occurred while creating the timesheet task.", StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // --- Endpoint 4: UpdateTaskTimesheet ---
        /// <summary>
        /// Updates an existing timesheet task identified by its ID.
        /// </summary>
        /// <param name="taskId">The ID of the task to update.</param>
        /// <param name="dto">The DTO containing updated task information.</param>
        /// <returns>An ActionResult indicating the result of the update.</returns>
        [HttpPut("timesheets/{taskId}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)] // Missing user ID claim for audit
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)] // If trying to update someone else's task without permission
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)] // For concurrency errors
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateTaskTimesheet(int taskId, [FromBody] TimesheetTaskCreateDto dto)
        {
            try
            {
                int? optionalUserId = GetCurrentUserId();
                if (!optionalUserId.HasValue)
                {
                    return Unauthorized(new ApiResponse<object>("Invalid token. User ID is missing or invalid.", "Error", StatusCodes.Status401Unauthorized));
                }
                int loggedInUserId = optionalUserId.Value;

                var task = await _context.Tasks
                                         .FirstOrDefaultAsync(t => t.TaskId == taskId && t.AssignedTo == loggedInUserId && !t.IsDeleted);

                if (task == null)
                {
                    _logger.LogWarning("Update attempt: Task {TaskId} not found, already deleted, or not assigned to user {UserId}.", taskId, loggedInUserId);
                    return NotFound(new ApiResponse<object>($"Task with ID {taskId} not found, already deleted, or you don't have permission to update it.", "Error", StatusCodes.Status404NotFound));
                }

                task.Title = dto.TaskName;
                task.StartDate = dto.StartDate;
                task.EndDate = dto.EndDate;
                task.StartTime = dto.StartTime;
                task.EndTime = dto.EndTime;
                task.DailyLog = dto.DailyLog;
                task.UpdatedAt = DateTimeOffset.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated timesheet task {TaskId} by user {UserId}.", taskId, loggedInUserId);

                // FIX: Explicitly specify 'object' for the null data payload in Success call.
                return Success<object>("Timesheet task updated successfully.", null, StatusCodes.Status200OK);
            }
            catch (DbUpdateConcurrencyException dbConcEx)
            {
                _logger.LogError(dbConcEx, "Concurrency conflict updating task {TaskId}.", taskId);
                return Conflict(new ApiResponse<object>("Concurrency conflict: The task was modified or deleted by another user.", "Error", StatusCodes.Status409Conflict, new { databaseError = dbConcEx.Message }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating timesheet task {TaskId} by user {UserId}.", taskId, GetCurrentUserId());
                return Error<object>("An error occurred while updating the task.", StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // --- Endpoint 5: DeleteTaskTimesheet (Soft Delete) ---
        /// <summary>
        /// Soft deletes a timesheet task identified by its ID.
        /// </summary>
        /// <param name="taskId">The ID of the task to soft delete.</param>
        /// <returns>An ActionResult indicating the result of the deletion.</returns>
        [HttpDelete("timesheets/{taskId}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteTaskTimesheet(int taskId)
        {
            try
            {
                int? optionalUserId = GetCurrentUserId();
                if (!optionalUserId.HasValue)
                {
                    return Unauthorized(new ApiResponse<object>("Invalid token. User ID is missing or invalid.", "Error", StatusCodes.Status401Unauthorized));
                }
                int loggedInUserId = optionalUserId.Value;

                var task = await _context.Tasks
                                         .FirstOrDefaultAsync(t => t.TaskId == taskId && t.AssignedTo == loggedInUserId && !t.IsDeleted);

                if (task == null)
                {
                    _logger.LogWarning("Delete attempt: Task {TaskId} not found, already deleted, or not assigned to user {UserId}.", taskId, loggedInUserId);
                    return NotFound(new ApiResponse<object>($"Task with ID {taskId} not found, already deleted, or you don't have permission to delete it.", "Error", StatusCodes.Status404NotFound));
                }

                task.IsDeleted = true;
                task.UpdatedAt = DateTimeOffset.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully soft-deleted timesheet task {TaskId} by user {UserId}.", taskId, loggedInUserId);

                // FIX: Explicitly specify 'object' for the null data payload in Success call.
                return Success<object>("Timesheet task deleted successfully.", null, StatusCodes.Status200OK);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft deleting timesheet task {TaskId} by user {UserId}.", taskId, GetCurrentUserId());
                return Error<object>("An error occurred while deleting the task.", StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // --- Endpoint 6: GetMyProjects with Paging, Sorting, Filtering ---
        /// <summary>
        /// Retrieves distinct projects assigned to the current authenticated user, with optional filtering, sorting, and pagination.
        /// </summary>
        /// <param name="page">The page number for pagination (default: 1).</param>
        /// <param name="pageSize">The number of items per page (default: 10).</param>
        /// <param name="sortBy">The field to sort by (e.g., "project_name", "start_date").</param>
        /// <param name="sortOrder">The sort order ("asc" for ascending, "desc" for descending, default: "asc").</param>
        /// <param name="searchKeyword">A keyword to filter project name or code.</param>
        /// <returns>A paged list of distinct projects.</returns>
        [HttpGet("my-projects")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMyProjects(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = null,
            [FromQuery] string sortOrder = "asc",
            [FromQuery] string? searchKeyword = null)
        {
            try
            {
                int? optionalUserId = GetCurrentUserId();
                if (!optionalUserId.HasValue)
                {
                    return Unauthorized(new ApiResponse<object>("Invalid token. User ID is missing or invalid.", "Error", StatusCodes.Status401Unauthorized));
                }
                int loggedInUserId = optionalUserId.Value;


                _logger.LogInformation("Fetching projects for user {UserId} with page: {Page}, pageSize: {PageSize}, sortBy: {SortBy}, sortOrder: {SortOrder}, searchKeyword: {SearchKeyword}", loggedInUserId, page, pageSize, sortBy, sortOrder, searchKeyword);

                var query = (from task in _context.Tasks
                             where task.AssignedTo == loggedInUserId && !task.IsDeleted
                             join project in _context.Projects
                                 on task.ProjectId equals project.ProjectId
                             select new
                             {
                                 project_id = project.ProjectId,
                                 project_name = project.Name,
                                 code = project.Code,
                                 description = project.Description,
                                 start_date = project.StartDate,
                                 end_date = project.EndDate,
                                 created_at = project.CreatedAt // Include for sorting
                             }).Distinct();

                // --- Apply Filtering (example: search by project name or code) ---
                if (!string.IsNullOrWhiteSpace(searchKeyword))
                {
                    string lowerSearch = searchKeyword.ToLower();
                    query = query.Where(p => (p.project_name != null && p.project_name.ToLower().Contains(lowerSearch)) || 
                                             (p.code != null && p.code.ToLower().Contains(lowerSearch)));
                }

                var totalProjects = await query.CountAsync();

                // --- Apply Sorting ---
                switch (sortBy?.ToLower())
                {
                    case "project_name":
                        query = sortOrder.ToLower() == "desc" ? query.OrderByDescending(p => p.project_name) : query.OrderBy(p => p.project_name);
                        break;
                    case "start_date":
                        query = sortOrder.ToLower() == "desc" ? query.OrderByDescending(p => p.start_date) : query.OrderBy(p => p.start_date);
                        break;
                    case "end_date":
                        query = sortOrder.ToLower() == "desc" ? query.OrderByDescending(p => p.end_date) : query.OrderBy(p => p.end_date);
                        break;
                    case "created_at":
                        query = sortOrder.ToLower() == "desc" ? query.OrderByDescending(p => p.created_at) : query.OrderBy(p => p.created_at);
                        break;
                    default:
                        query = query.OrderByDescending(p => p.created_at);
                        break;
                }

                var pagedProjects = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                if (!pagedProjects.Any())
                {
                    return NotFound(new ApiResponse<object>("No projects found for the user matching the criteria.", "Error", StatusCodes.Status404NotFound));
                }

                var totalPages = (int)Math.Ceiling((double)totalProjects / pageSize);

                var pagedResponse = new PagedResult<object>
                {
                    Items = pagedProjects,
                    TotalCount = totalProjects,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    TotalTasks = 0,
                    SortBy = sortBy,
                    SortOrder = sortOrder,
                    SearchKeyword = searchKeyword
                };

                // FIX: Explicitly specify the type argument for Success.
                return Success<PagedResult<object>>("Projects retrieved successfully.", pagedResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user projects.");
                return Error<PagedResult<object>>("Internal server error while fetching projects.", StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
