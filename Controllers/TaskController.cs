using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs; // Assuming DTOs are in this namespace
using System.Collections.Generic;
using System.Linq;
using Task = System.Threading.Tasks.Task;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http; // For IFormFile and StatusCodes
using BizfreeApp.Services; // To inject IUploadHandler
using Microsoft.Extensions.Logging; // For ILogger
using System.IO; // For Path.GetFileNameWithoutExtension
using BizfreeApp.Constants;

namespace BizfreeApp.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly Data.ApplicationDbContext _context;
        private readonly IUploadHandler _uploadHandler; // Inject IUploadHandler
        private readonly ILogger<TasksController> _logger; // Inject ILogger
        private readonly ITaskNotificationService _taskNotificationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITaskEmailService _taskEmailService;

        public TasksController(Data.ApplicationDbContext context, IUploadHandler uploadHandler, ILogger<TasksController> logger, ITaskNotificationService taskNotificationService, IServiceProvider serviceProvider, ITaskEmailService taskEmailService)
        {
            _context = context;
            _uploadHandler = uploadHandler;
            _logger = logger;
            _taskNotificationService = taskNotificationService;
            _serviceProvider = serviceProvider;
            _taskEmailService = taskEmailService; // Initialize
        }

        // Helper Method to get the current Company's ID from claims
        private int? GetCompanyIdFromClaims()
        {
            var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
            if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out int companyId))
            {
                return companyId;
            }
            _logger.LogWarning("Company ID claim not found or could not be parsed.");
            return null;
        }

        // Helper Method to get the current User's ID from claims
        private int? GetCurrentUserIdFromClaims()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            _logger.LogWarning("User ID claim not found or could not be parsed.");
            return null;
        }

        // Helper method to generate a standardized success response for ActionResult<ApiResponse<T>>
        private ActionResult<ApiResponse<T>> Success<T>(string message, T? data, int statusCode = StatusCodes.Status200OK)
        {
            return StatusCode(statusCode, new ApiResponse<T>(message, "success", statusCode, data));
        }

        // Overloaded Helper method to generate a standardized error response for ActionResult<ApiResponse<T>>
        private ActionResult<ApiResponse<T>> Error<T>(string message, int statusCode, string status = "error")
        {
            return StatusCode(statusCode, new ApiResponse<T>(message, status, statusCode, default(T)));
        }

        // Overloaded Helper method to generate a standardized error response for plain IActionResult
        private IActionResult Error(string message, int statusCode, string status = "error")
        {
            // Explicitly create an ApiResponse<object> for non-generic IActionResult returns
            return StatusCode(statusCode, new ApiResponse<object>(message, status, statusCode, null));
        }

        private bool HasPermission(string permission)
        {
            return User.HasClaim("permission", permission);
        }


        // Helper method to get a Project specific to the company
        private async Task<Project?> GetProjectForCompany(int projectId, int companyId)
        {
            return await _context.Projects
                                 .Where(p => p.ProjectId == projectId && p.CompanyId == companyId)
                                 .FirstOrDefaultAsync();
        }

        // Helper method to get a TaskList specific to the company and project
        private async Task<TaskList?> GetTaskListForCompanyAndProject(int taskListId, int projectId, int companyId)
        {
            return await _context.TaskLists
                                 .Where(tl => tl.TaskListId == taskListId && tl.ProjectId == projectId && tl.CompanyId == companyId)
                                 .FirstOrDefaultAsync();
        }

        // Helper method to get a TaskList by its ID and Company ID, regardless of Project ID in the path.
        private async Task<TaskList?> GetTaskListForCompany(int taskListId, int companyId)
        {
            return await _context.TaskLists
                                 .Where(tl => tl.TaskListId == taskListId && tl.CompanyId == companyId)
                                 .FirstOrDefaultAsync();
        }

        // Helper method to get a Task specific to the company by its TaskId (flatter API)
        private async Task<Models.Task?> GetTaskForCompany(int taskId, int companyId)
        {
            return await _context.Tasks
                                 .Include(t => t.TaskList)
                                 .Include(t => t.StatusNavigation)
                                 .Include(t => t.Priority)
                                 // Include User and then the specific CompanyUser for the name
                                 .Include(t => t.AssignedToNavigation) // This is User
                                     .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId)) // Filter for current company's user entry
                                 .Include(t => t.Company)
                                 .Include(t => t.Project) // ADDED: Include Project for ProjectName
                                                          // Include SubTasks and their related navigation properties
                                 .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                                     .ThenInclude(st => st.StatusNavigation)
                                 .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                                     .ThenInclude(st => st.Priority)
                                 .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                                     .ThenInclude(st => st.AssignedToNavigation)
                                         .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId)) // For subtask assigned user name
                                 .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                                     .ThenInclude(st => st.Project) // ADDED: Include Project for subtasks' ProjectName
                                                                    // Include Documents and their related navigation properties (e.g., CreatedByNavigation for CreatedByName)
                                 .Include(t => t.TaskDocuments.Where(td => !td.IsDeleted)) // Assuming IsDeleted on TaskDocument
                                 .Where(t => t.TaskId == taskId && t.CompanyId == companyId && !t.IsDeleted)
                                 .FirstOrDefaultAsync();
        }

        private async Task<List<AssignedUserInfo>> GetAssignedUsersForTask(Models.Task task, int companyId)
        {
            if (task == null || string.IsNullOrEmpty(task.AssignedUserIds))
                return new List<AssignedUserInfo>();

            var userIds = task.AssignedUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.Parse(id.Trim()))
                .ToList();

            var assignedUsers = await _context.Users
                .Where(u => userIds.Contains(u.UserId))
                .Include(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId))
                .Select(u => new AssignedUserInfo
                {
                    UserId = u.UserId,
                    UserName = u.CompanyUserUsers
                        .Where(cu => cu.CompanyId == companyId)
                        .Select(cu => $"{cu.FirstName} {cu.LastName}".Trim())
                        .FirstOrDefault(),
                    AvatarUrl = u.CompanyUserUsers
                        .FirstOrDefault(cu => cu.CompanyId == companyId).ProfilePhotoUrl
                })
                .ToListAsync();

            return assignedUsers;
        }

        // GET: api/Tasks/tasklists/{taskListId}/tasks
        // This endpoint retrieves all tasks within a specific task list for the authorized company, with filtering, paging, and sorting.
        [HttpGet("tasklists/{taskListId}/tasks")]
        [Authorize(Policy = "CanTaskReadAll")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<TaskDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PagedResult<TaskDto>>>> GetTasksByTaskList(
             int taskListId,
             [FromQuery] int page = 1,
             [FromQuery] int pageSize = 5,
             [FromQuery] string sortBy = "endDate",
             [FromQuery] string sortOrder = "desc",
             [FromQuery] string? search = null,
             [FromQuery] string? statusNames = null,
             [FromQuery] int? priorityId = null,
             [FromQuery] int? assignedToUserId = null,
             [FromQuery] string? memberUserIds = null,
             [FromQuery] DateOnly? dueDateFrom = null,
             [FromQuery] DateOnly? dueDateTo = null)
        {
            try
            {
                var companyId = GetCompanyIdFromClaims();
                if (!companyId.HasValue)
                {
                    return Error<PagedResult<TaskDto>>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                var taskList = await GetTaskListForCompany(taskListId, companyId.Value);
                if (taskList == null || taskList.IsDeleted)
                {
                    return Error<PagedResult<TaskDto>>("Task List not found or not accessible.", StatusCodes.Status404NotFound);
                }

                IQueryable<Models.Task> query = _context.Tasks
                    .Include(t => t.StatusNavigation) // Required for Task.Progression
                    .Include(t => t.Priority)
                    .Include(t => t.TaskList)
                    .Include(t => t.AssignedToNavigation) // Include User
                        .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId.Value)) // ThenInclude CompanyUser for name
                    .Include(t => t.Company)
                    .Include(t => t.Project)
                    // Include subtasks and their related navigation properties for Progression calculation
                    .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                        .ThenInclude(st => st.StatusNavigation) // Crucial for subtask progression
                    .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                        .ThenInclude(st => st.Priority)
                    .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                        .ThenInclude(st => st.AssignedToNavigation) // User
                            .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId.Value)) // CompanyUser for subtask assigned user name
                    .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                        .ThenInclude(st => st.Project)
                    // Include documents and their related navigation properties
                    .Include(t => t.TaskDocuments.Where(td => !td.IsDeleted))
                    .Where(t => t.TaskListId == taskListId &&
                                 t.ProjectId == taskList.ProjectId &&
                                 t.CompanyId == companyId.Value &&
                                 !t.IsDeleted &&
                                 !t.ParentTaskId.HasValue); // Filter to get only top-level tasks

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(t => t.Title!.Contains(search) || (t.Description != null && t.Description.Contains(search)));
                }

                if (!string.IsNullOrWhiteSpace(statusNames))
                {
                    var parsedStatusNames = statusNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                         .Select(s => s.Trim())
                                                         .ToList();

                    if (parsedStatusNames.Any())
                    {
                        query = query.Where(t => t.StatusNavigation != null && parsedStatusNames.Contains(t.StatusNavigation.Name));
                    }
                }

                if (priorityId.HasValue)
                {
                    query = query.Where(t => t.PriorityId == priorityId.Value);
                }

                var userIdsToFilter = new List<string>();
                if (assignedToUserId.HasValue) userIdsToFilter.Add(assignedToUserId.Value.ToString());
                if (!string.IsNullOrWhiteSpace(memberUserIds)) 
                {
                    userIdsToFilter.AddRange(memberUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                }
                userIdsToFilter = userIdsToFilter.Distinct().ToList();

                if (userIdsToFilter.Any())
                {
                    query = query.Where(BuildMemberFilterExpression(userIdsToFilter));
                }

                if (dueDateFrom.HasValue)
                {
                    query = query.Where(t => t.EndDate.HasValue && t.EndDate.Value >= dueDateFrom.Value); // Changed to EndDate.Date for DateOnly comparison
                }

                if (dueDateTo.HasValue)
                {
                    query = query.Where(t => t.EndDate.HasValue && t.EndDate.Value <= dueDateTo.Value); // Changed to EndDate.Date for DateOnly comparison
                }

                var totalTasks = await query.CountAsync();

                var taskPropertyMap = new Dictionary<string, Expression<Func<Models.Task, object>>>
                {
                    { "taskid", t => t.TaskId },
                    { "title", t => t.Title! },
                    { "statusname", t => t.StatusNavigation!.Name! },
                    { "duedate", t => t.EndDate! }, // Assuming dueDate maps to EndDate
                    { "priorityname", t => t.Priority!.Name! },
                    { "assignedtouserid", t => t.AssignedTo! },
                    { "listname", t => t.TaskList!.ListName! },
                    { "taskorder", t => t.TaskOrder! },
                    { "companyname", t => t.Company!.CompanyName! },
                    { "startdate", t => t.StartDate! },
                    { "enddate", t => t.EndDate! },
                    { "estimatedhours", t => t.EstimatedHours! }
                };

                if (taskPropertyMap.TryGetValue(sortBy.ToLower(), out var sortExpression))
                {
                    if (sortOrder.ToLower() == "desc")
                    {
                        query = query.OrderByDescending(sortExpression);
                    }
                    else
                    {
                        query = query.OrderBy(sortExpression);
                    }
                }
                else
                {
                    query = query.OrderBy(t => t.TaskId); // Default sort if invalid sortBy is provided
                }

                var items = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new TaskDto
                    {
                        TaskId = t.TaskId,
                        Title = t.Title,
                        StatusName = t.StatusNavigation != null ? t.StatusNavigation.Name : null,
                        StartDate = t.StartDate,
                        EndDate = t.EndDate,
                        PriorityName = t.Priority != null ? t.Priority.Name : null,
                        Description = t.Description,
                        AssignedToUserId = t.AssignedTo,
                        AssignedToUserName = t.AssignedToNavigation != null && t.AssignedToNavigation.CompanyUserUsers.Any()
                                            ? $"{t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.FirstName} {t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.LastName}".Trim()
                                            : null,
                        TaskListId = t.TaskList != null ? t.TaskList.TaskListId : null,
                        ListName = t.TaskList != null ? t.TaskList.ListName : null,
                        TaskListDescription = t.TaskList != null ? t.TaskList.Description : null,
                        ListOrder = t.TaskList != null ? t.TaskList.ListOrder : null,
                        ProjectId = t.ProjectId,
                        Name = t.Project != null ? t.Project.Name : null,
                        CompanyName = t.Company != null ? t.Company.CompanyName : null,
                        ParentTaskId = t.ParentTaskId,
                        EstimatedHours = t.EstimatedHours,
                        Progression = t.Progression, // Include Progression from the model
                        Subtasks = t.SubTasks.Select(st => new TaskDto
                        {
                            TaskId = st.TaskId,
                            Title = st.Title,
                            StatusName = st.StatusNavigation != null ? st.StatusNavigation.Name : null,
                            StartDate = st.StartDate,
                            EndDate = st.EndDate,
                            PriorityName = st.Priority != null ? st.Priority.Name : null,
                            Description = st.Description,
                            AssignedToUserId = st.AssignedTo,
                            AssignedToUserName = st.AssignedToNavigation != null && st.AssignedToNavigation.CompanyUserUsers.Any()
                                                ? $"{st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.FirstName} {st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.LastName}".Trim()
                                                : null,
                            TaskListId = st.TaskListId,
                            ListName = st.TaskList != null ? st.TaskList.ListName : null,
                            TaskListDescription = st.TaskList != null ? st.TaskList.Description : null,
                            ListOrder = st.TaskList != null ? st.TaskList.ListOrder : null,
                            ProjectId = st.ProjectId,
                            Name = st.Project != null ? st.Project.Name : null,
                            CompanyName = st.Company != null ? st.Company.CompanyName : null,
                            ParentTaskId = st.ParentTaskId,
                            EstimatedHours = st.EstimatedHours,
                            Progression = st.Progression // Include Progression for subtasks
                        }).ToList(),
                        Documents = t.TaskDocuments.Select(td => new TaskDocumentDto
                        {
                            DocumentId = td.DocumentId,
                            TaskId = td.TaskId,
                            DocumentName = td.DocumentName,
                            FilePath = td.FilePath,
                            DocumentType = td.DocumentType,
                            Description = td.Description,
                            CreatedAt = td.CreatedAt,
                        }).ToList()
                    })
                    .ToListAsync();
                foreach (var taskDto in items)
                {
                    var task = await _context.Tasks
                        .FirstOrDefaultAsync(t => t.TaskId == taskDto.TaskId);
                    if (task != null)
                    {
                        taskDto.AssignedUsers = await GetAssignedUsersForTask(task, companyId.Value);
                    }
                }

                var pagedResult = new PagedResult<TaskDto>
                {
                    Items = items,
                    TotalCount = totalTasks,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalTasks / pageSize),
                    TotalTasks = totalTasks
                };

                return Success("My tasks retrieved successfully.", pagedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting tasks in GetAllTasks for current user ID {GetCurrentUserIdFromClaims()}.");
                return Error<PagedResult<TaskDto>>("An error occurred while retrieving your tasks.", StatusCodes.Status500InternalServerError);
            }
        }


        // GET: api/Tasks/{taskId}
        // This endpoint retrieves a specific task by its ID (flatter API).
        [HttpGet("{id}")]
        [Authorize(Policy = "CanTaskReadOrAssigned")]
        [ProducesResponseType(typeof(ApiResponse<TaskDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<TaskDto>>> GetTaskById(int id)
        {
            try
            {
                var companyId = GetCompanyIdFromClaims();
                if (!companyId.HasValue)
                {
                    return Error<TaskDto>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                var task = await GetTaskForCompany(id, companyId.Value);

                if (task != null && !task.IsDeleted)
                {
                    _logger.LogInformation($"Found regular Task with ID: {id}. Raw Status ID: {task.Status}, CompanyId: {companyId.Value}");
                    var taskDto = new TaskDto
                    {
                        TaskId = task.TaskId,
                        Title = task.Title,
                        StatusName = task.StatusNavigation?.Name ?? (task.Status.HasValue ? $"Unknown Status (ID: {task.Status})" : "No Status"),
                        StartDate = task.StartDate,
                        EndDate = task.EndDate,
                        PriorityName = task.Priority?.Name,
                        Description = task.Description,
                        AssignedToUserId = task.AssignedTo,
                        AssignedToUserName = task.AssignedToNavigation != null && task.AssignedToNavigation.CompanyUserUsers.Any()
                                            ? $"{task.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.FirstName} {task.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.LastName}".Trim()
                                            : null,
                        AssignedToUserAvatarUrl = task.AssignedToNavigation?.CompanyUserUsers
                            .FirstOrDefault(cu => cu.CompanyId == companyId.Value)?.ProfilePhotoUrl,

                        // NEW: Add assigned users here for regular tasks
                        AssignedUsers = await GetAssignedUsersForTask(task, companyId.Value),

                        TaskListId = task.TaskList?.TaskListId,
                        ListName = task.TaskList?.ListName,
                        TaskListDescription = task.TaskList?.Description,
                        ListOrder = task.TaskList?.ListOrder,
                        ProjectId = task.ProjectId,
                        Name = task.Project?.Name,
                        CompanyName = task.Company?.CompanyName,
                        ParentTaskId = task.ParentTaskId,
                        EstimatedHours = task.EstimatedHours,
                        Progression = task.Progression,
                        //Type = "Task",
                        Subtasks = task.SubTasks.Select(st => new TaskDto
                        {
                            TaskId = st.TaskId,
                            Title = st.Title,
                            StatusName = st.StatusNavigation != null ? st.StatusNavigation.Name : null,
                            StartDate = st.StartDate,
                            EndDate = st.EndDate,
                            PriorityName = st.Priority != null ? st.Priority.Name : null,
                            Description = st.Description,
                            AssignedToUserId = st.AssignedTo,
                            AssignedToUserName = st.AssignedToNavigation != null && st.AssignedToNavigation.CompanyUserUsers.Any()
                                                ? $"{st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.FirstName} {st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.LastName}".Trim()
                                                : null,
                            TaskListId = st.TaskListId,
                            ListName = st.TaskList != null ? st.TaskList.ListName : null,
                            TaskListDescription = st.TaskList != null ? st.TaskList.Description : null,
                            ListOrder = st.TaskList != null ? st.TaskList.ListOrder : null,
                            ProjectId = st.ProjectId,
                            Name = st.Project != null ? st.Project.Name : null,
                            CompanyName = st.Company != null ? st.Company.CompanyName : null,
                            ParentTaskId = st.ParentTaskId,
                            EstimatedHours = st.EstimatedHours,
                            Progression = st.Progression,
                            //Type = "Task"
                        }).ToList(),
                        Documents = task.TaskDocuments.Select(td => new TaskDocumentDto
                        {
                            DocumentId = td.DocumentId,
                            TaskId = td.TaskId,
                            DocumentName = td.DocumentName,
                            FilePath = td.FilePath,
                            DocumentType = td.DocumentType,
                            Description = td.Description,
                            CreatedAt = td.CreatedAt
                        }).ToList()
                    };
                    return Success("Task retrieved successfully.", taskDto);
                }

                var taskTodo = await _context.TaskTodos
                                             .Include(tt => tt.StatusNavigation)
                                             .Include(tt => tt.Priority)
                                             .Include(tt => tt.AssignedToNavigation) // This is User
                                                 .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId.Value)) // Filter for current company's user entry
                                             .Include(tt => tt.Company)
                                             .Include(tt => tt.Project)
                                             .Include(tt => tt.SubTasks.Where(st => !st.IsDeleted))
                                                 .ThenInclude(st => st.StatusNavigation)
                                             .Include(tt => tt.SubTasks.Where(st => !st.IsDeleted))
                                                 .ThenInclude(st => st.Priority)
                                             .Include(tt => tt.SubTasks.Where(st => !st.IsDeleted))
                                                 .ThenInclude(st => st.AssignedToNavigation) // User
                                                    .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId.Value)) // CompanyUser for sub-todo assigned user name
                                             .Where(tt => tt.TaskTodoId == id && tt.CompanyId == companyId.Value && !tt.IsDeleted)
                                             .FirstOrDefaultAsync();

                if (taskTodo != null)
                {
                    _logger.LogInformation($"Found TaskTodo with ID: {id}. Raw Status ID: {taskTodo.Status}, CompanyId: {companyId.Value}");
                    // Project TaskTodo to TaskDto
                    var taskDto = new TaskDto
                    {
                        TaskId = taskTodo.TaskTodoId, // Map TaskTodoId to TaskId
                        Title = taskTodo.Title,
                        StatusName = taskTodo.StatusNavigation?.Name ?? (taskTodo.Status.HasValue ? $"Unknown Status (ID: {taskTodo.Status})" : "No Status"),
                        StartDate = taskTodo.StartDate,
                        EndDate = taskTodo.EndDate,
                        PriorityName = taskTodo.Priority?.Name,
                        Description = taskTodo.Description,
                        AssignedToUserId = taskTodo.AssignedTo,
                        AssignedToUserName = taskTodo.AssignedToNavigation != null && taskTodo.AssignedToNavigation.CompanyUserUsers.Any()
                                            ? $"{taskTodo.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.FirstName} {taskTodo.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.LastName}".Trim()
                                            : null,

                        // For TaskTodos, return empty list since they don't have AssignedUserIds field yet
                        AssignedUsers = new List<AssignedUserInfo>(),

                        TaskListId = null,
                        ListName = null,
                        TaskListDescription = null,
                        ListOrder = null,
                        ProjectId = null,  // Explicitly null for todos
                        Name = null,       // Explicitly null for todos
                        CompanyName = taskTodo.Company?.CompanyName,
                        ParentTaskId = taskTodo.ParentTaskId,
                        EstimatedHours = taskTodo.EstimatedHours,
                        Progression = taskTodo.Progression,
                        Subtasks = taskTodo.SubTasks.Select(st => new TaskDto
                        {
                            TaskId = st.TaskTodoId, // Map sub-todo TaskTodoId to TaskId
                            Title = st.Title,
                            StatusName = st.StatusNavigation != null ? st.StatusNavigation.Name : null,
                            StartDate = st.StartDate,
                            EndDate = st.EndDate,
                            PriorityName = st.Priority != null ? st.Priority.Name : null,
                            Description = st.Description,
                            AssignedToUserId = st.AssignedTo,
                            AssignedToUserName = st.AssignedToNavigation != null && st.AssignedToNavigation.CompanyUserUsers.Any()
                                                ? $"{st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.FirstName} {st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.LastName}".Trim()
                                                : null,
                            TaskListId = null,
                            ListName = null,
                            TaskListDescription = null,
                            ListOrder = null,
                            ProjectId = null,
                            Name = null,
                            CompanyName = st.Company?.CompanyName,
                            ParentTaskId = st.ParentTaskId,
                            EstimatedHours = st.EstimatedHours,
                            Progression = st.Progression,
                        }).ToList(),
                        Documents = new List<TaskDocumentDto>() // Assuming TaskTodoDocuments are removed
                    };
                    return Success("Task Todo retrieved successfully.", taskDto);
                }

                // If not found as either
                return Error<TaskDto>("Item not found or not accessible.", StatusCodes.Status404NotFound);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting item by ID {id}.");
                return Error<TaskDto>("An error occurred while retrieving the item.", StatusCodes.Status500InternalServerError);
            }
        }

        // GET: api/Tasks/users/mytasks
        // This endpoint retrieves tasks assigned only to the current logged-in user.
        [HttpGet("users/mytasks")]
        [Authorize(Policy = "CanTaskReadAssigned")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<TaskDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PagedResult<TaskDto>>>> GetMyTasks(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string sortBy = "endDate",
            [FromQuery] string sortOrder = "desc",
            [FromQuery] string? search = null,
            [FromQuery] string? statusNames = "Active,Delayed,InProgress,InReview,Open,NotStarted,Cancelled",
            [FromQuery] int? priorityId = null,
            [FromQuery] DateOnly? startDateFrom = null,
            [FromQuery] DateOnly? startDateTo = null,
            [FromQuery] DateOnly? endDateFrom = null,
            [FromQuery] DateOnly? endDateTo = null,
            [FromQuery] string type = "all") // New parameter: all, tasks, todos
        {
            try
            {
                var companyId = GetCompanyIdFromClaims();
                if (!companyId.HasValue)
                    return Error<PagedResult<TaskDto>>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);

                var currentUserId = GetCurrentUserIdFromClaims();
                if (!currentUserId.HasValue)
                    return Error<PagedResult<TaskDto>>("User ID not found in claims.", StatusCodes.Status401Unauthorized);

                _logger.LogInformation("GetMyTasks - User {UserId} viewing {Type} assigned tasks.", currentUserId.Value, type);

                bool fetchTasks = string.Equals(type, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "tasks", StringComparison.OrdinalIgnoreCase);
                bool fetchTodos = string.Equals(type, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "todos", StringComparison.OrdinalIgnoreCase);

                var taskDtos = new List<TaskDto>();
                var taskTodoDtos = new List<TaskDto>();

                if (fetchTasks)
                {
                    // 1. Query Tasks assigned to the user
                    var tasksQuery = _context.Tasks
                        .Include(t => t.StatusNavigation)
                        .Include(t => t.Priority)
                        .Include(t => t.TaskList)
                        .Include(t => t.AssignedToNavigation)
                            .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId.Value))
                        .Include(t => t.Company)
                        .Include(t => t.Project)
                        .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                            .ThenInclude(st => st.StatusNavigation)
                        .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                            .ThenInclude(st => st.Priority)
                        .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                            .ThenInclude(st => st.AssignedToNavigation)
                                .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId.Value))
                        .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                            .ThenInclude(st => st.Project)
                        .Include(t => t.TaskDocuments.Where(td => !td.IsDeleted))
                        .Where(t => t.CompanyId == companyId.Value &&
                                     t.AssignedTo == currentUserId.Value &&
                                     !t.IsDeleted &&
                                     !t.ParentTaskId.HasValue &&
                                     (t.Project == null || t.Project.IsDeleted == false));

                    var taskEntities = await tasksQuery.ToListAsync();
                    taskDtos = taskEntities.Select(t => new TaskDto
                    {
                        TaskId = t.TaskId,
                        Title = t.Title,
                        StatusName = t.StatusNavigation?.Name,
                        StartDate = t.StartDate,
                        EndDate = t.EndDate,
                        PriorityName = t.Priority?.Name,
                        Description = t.Description,
                        AssignedToUserId = t.AssignedTo,
                        AssignedToUserName = t.AssignedToNavigation != null && t.AssignedToNavigation.CompanyUserUsers.Any()
                            ? $"{t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.FirstName} {t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.LastName}".Trim()
                            : null,
                        AssignedToUserAvatarUrl = t.AssignedToNavigation != null && t.AssignedToNavigation.CompanyUserUsers.Any()
                            ? t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)?.ProfilePhotoUrl
                            : null,
                        TaskListId = t.TaskList?.TaskListId,
                        ListName = t.TaskList?.ListName,
                        TaskListDescription = t.TaskList?.Description,
                        ListOrder = t.TaskList?.ListOrder,
                        ProjectId = t.ProjectId,
                        Name = t.Project?.Name,
                        CompanyName = t.Company?.CompanyName,
                        ParentTaskId = t.ParentTaskId,
                        EstimatedHours = t.EstimatedHours,
                        Progression = t.Progression,
                        Subtasks = t.SubTasks.Select(st => new TaskDto
                        {
                            TaskId = st.TaskId,
                            Title = st.Title,
                            StatusName = st.StatusNavigation?.Name,
                            StartDate = st.StartDate,
                            EndDate = st.EndDate,
                            PriorityName = st.Priority?.Name,
                            Description = st.Description,
                            AssignedToUserId = st.AssignedTo,
                            AssignedToUserName = st.AssignedToNavigation != null && st.AssignedToNavigation.CompanyUserUsers.Any()
                                ? $"{st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.FirstName} {st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.LastName}".Trim()
                                : null,
                            AssignedToUserAvatarUrl = st.AssignedToNavigation != null && st.AssignedToNavigation.CompanyUserUsers.Any()
                                ? st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)?.ProfilePhotoUrl
                                : null,
                            TaskListId = st.TaskListId,
                            ListName = st.TaskList?.ListName,
                            TaskListDescription = st.TaskList?.Description,
                            ListOrder = st.TaskList?.ListOrder,
                            ProjectId = st.ProjectId,
                            Name = st.Project?.Name,
                            CompanyName = st.Company?.CompanyName,
                            ParentTaskId = st.ParentTaskId,
                            EstimatedHours = st.EstimatedHours,
                            Progression = st.Progression
                        }).ToList(),
                        Documents = t.TaskDocuments.Select(td => new TaskDocumentDto
                        {
                            DocumentId = td.DocumentId,
                            TaskId = td.TaskId,
                            DocumentName = td.DocumentName,
                            FilePath = td.FilePath,
                            DocumentType = td.DocumentType,
                            Description = td.Description,
                            CreatedAt = td.CreatedAt,
                        }).ToList()
                    }).ToList();

                    foreach (var taskDto in taskDtos)
                    {
                        var task = taskEntities.FirstOrDefault(t => t.TaskId == taskDto.TaskId);
                        if (task != null)
                            taskDto.AssignedUsers = await GetAssignedUsersForTask(task, companyId.Value);
                    }
                }

                if (fetchTodos)
                {
                    // 2. Query Todos assigned to the user
                    var taskTodosQuery = _context.TaskTodos
                        .Include(tt => tt.StatusNavigation)
                        .Include(tt => tt.Priority)
                        .Include(tt => tt.TaskList)
                        .Include(tt => tt.AssignedToNavigation)
                            .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId.Value))
                        .Include(tt => tt.Company)
                        .Include(tt => tt.Project)
                        .Include(tt => tt.SubTasks.Where(st => !st.IsDeleted))
                            .ThenInclude(st => st.StatusNavigation)
                        .Include(tt => tt.SubTasks.Where(st => !st.IsDeleted))
                            .ThenInclude(st => st.Priority)
                        .Include(tt => tt.SubTasks.Where(st => !st.IsDeleted))
                            .ThenInclude(st => st.AssignedToNavigation)
                                .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId.Value))
                        .Include(tt => tt.SubTasks.Where(st => !st.IsDeleted))
                            .ThenInclude(st => st.Project)
                        .Where(tt => tt.CompanyId == companyId.Value &&
                                      tt.AssignedTo == currentUserId.Value &&
                                      !tt.IsDeleted &&
                                      !tt.ParentTaskId.HasValue &&
                                      (tt.Project == null || tt.Project.IsDeleted == false));

                    var todoEntities = await taskTodosQuery.ToListAsync();
                    taskTodoDtos = todoEntities.Select(tt => new TaskDto
                    {
                        TaskId = tt.TaskTodoId,
                        Title = tt.Title,
                        StatusName = tt.StatusNavigation?.Name,
                        StartDate = tt.StartDate,
                        EndDate = tt.EndDate,
                        PriorityName = tt.Priority?.Name,
                        Description = tt.Description,
                        AssignedToUserId = tt.AssignedTo,
                        AssignedToUserName = tt.AssignedToNavigation != null && tt.AssignedToNavigation.CompanyUserUsers.Any()
                            ? $"{tt.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.FirstName} {tt.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.LastName}".Trim()
                            : null,
                        AssignedToUserAvatarUrl = tt.AssignedToNavigation != null && tt.AssignedToNavigation.CompanyUserUsers.Any()
                            ? tt.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)?.ProfilePhotoUrl
                            : null,
                        CompanyName = tt.Company?.CompanyName,
                        ParentTaskId = tt.ParentTaskId,
                        EstimatedHours = tt.EstimatedHours,
                        Progression = tt.Progression,
                        Subtasks = tt.SubTasks.Select(st => new TaskDto
                        {
                            TaskId = st.TaskTodoId,
                            Title = st.Title,
                            StatusName = st.StatusNavigation?.Name,
                            StartDate = st.StartDate,
                            EndDate = st.EndDate,
                            PriorityName = st.Priority?.Name,
                            Description = st.Description,
                            AssignedToUserId = st.AssignedTo,
                            AssignedToUserName = st.AssignedToNavigation != null && st.AssignedToNavigation.CompanyUserUsers.Any()
                                ? $"{st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.FirstName} {st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.LastName}".Trim()
                                : null,
                            AssignedToUserAvatarUrl = st.AssignedToNavigation != null && st.AssignedToNavigation.CompanyUserUsers.Any()
                                ? st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)?.ProfilePhotoUrl
                                : null,
                            CompanyName = st.Company?.CompanyName,
                            ParentTaskId = st.ParentTaskId,
                            EstimatedHours = st.EstimatedHours,
                            Progression = st.Progression
                        }).ToList()
                    }).ToList();
                }

                var combined = taskDtos.Concat(taskTodoDtos).AsQueryable();

                // Apply in-memory filters
                if (!string.IsNullOrWhiteSpace(search))
                {
                    combined = combined.Where(t => t.Title!.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || (t.Description != null && t.Description.Contains(search, StringComparison.OrdinalIgnoreCase)));
                }

                if (!string.IsNullOrWhiteSpace(statusNames))
                {
                    var statusList = statusNames.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                    combined = combined.Where(t => t.StatusName != null && statusList.Contains(t.StatusName));
                }

                if (priorityId.HasValue)
                {
                    var priorityName = await _context.Taskpriorities.Where(p => p.PriorityId == priorityId.Value).Select(p => p.Name).FirstOrDefaultAsync();
                    if (!string.IsNullOrEmpty(priorityName))
                        combined = combined.Where(t => t.PriorityName == priorityName);
                }

                if (startDateFrom.HasValue) combined = combined.Where(t => t.StartDate.HasValue && t.StartDate.Value >= startDateFrom.Value);
                if (startDateTo.HasValue) combined = combined.Where(t => t.StartDate.HasValue && t.StartDate.Value <= startDateTo.Value);
                if (endDateFrom.HasValue) combined = combined.Where(t => t.EndDate.HasValue && t.EndDate.Value >= endDateFrom.Value);
                if (endDateTo.HasValue) combined = combined.Where(t => t.EndDate.HasValue && t.EndDate.Value <= endDateTo.Value);

                // Sorting and Paging
                combined = sortOrder.ToLower() == "desc" ? combined.OrderByDescending(GetSortExpression(sortBy)) : combined.OrderBy(GetSortExpression(sortBy));
                var totalCount = combined.Count();
                var items = combined.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                var pagedResult = new PagedResult<TaskDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    TotalTasks = totalCount
                };

                return Success("Your tasks retrieved successfully.", pagedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetMyTasks for user {GetCurrentUserIdFromClaims()}.");
                return Error<PagedResult<TaskDto>>("An error occurred while retrieving your tasks.", StatusCodes.Status500InternalServerError);
            }
        }

        // GET: api/Tasks/users/alltasks
        // This endpoint retrieves tasks across the company (Admin/Manager view).
        [HttpGet("users/alltasks")]
        [Authorize(Policy = "CanTaskReadAll")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<TaskDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PagedResult<TaskDto>>>> GetAllTasks(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 5,
            [FromQuery] string sortBy = "endDate",
            [FromQuery] string sortOrder = "desc",
            [FromQuery] string? search = null,
            [FromQuery] string? statusNames = "Active,Delayed,InProgress,InReview,Open,NotStarted,Cancelled",
            [FromQuery] int? priorityId = null,
            [FromQuery] DateOnly? startDateFrom = null,
            [FromQuery] DateOnly? startDateTo = null,
            [FromQuery] DateOnly? endDateFrom = null,
            [FromQuery] DateOnly? endDateTo = null,
            [FromQuery] int? memberUserId = null,
            [FromQuery] string? memberUserIds = null)
        {
            try
            {
                var companyId = GetCompanyIdFromClaims();
                if (!companyId.HasValue)
                    return Error<PagedResult<TaskDto>>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);

                var currentUserId = GetCurrentUserIdFromClaims();
                if (!currentUserId.HasValue)
                    return Error<PagedResult<TaskDto>>("User ID not found in claims.", StatusCodes.Status401Unauthorized);

                var canReadCompanyTasks = HasPermission(Permissions.TaskReadAll);
                var canReadAssignedTasks = HasPermission(Permissions.TaskReadAssigned);

                var managedDepartmentIds = await _context.Departments
                    .Where(d => d.CompanyId == companyId.Value &&
                                d.DepartmentHead != null &&
                                d.DepartmentHead.UserId == currentUserId.Value &&
                                !(d.IsDeleted ?? false))
                    .Select(d => d.DeptId)
                    .ToListAsync();

                IQueryable<Models.Task> query = _context.Tasks
                    .Where(t => t.CompanyId == companyId.Value && !t.IsDeleted && !t.ParentTaskId.HasValue);

                var filterIds = new List<string>();
                if (memberUserId.HasValue) filterIds.Add(memberUserId.Value.ToString());
                if (!string.IsNullOrWhiteSpace(memberUserIds)) 
                {
                    filterIds.AddRange(memberUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                }
                filterIds = filterIds.Distinct().ToList();

                // Capability-based filtering
                if (canReadCompanyTasks)
                {
                    if (filterIds.Any())
                        query = query.Where(BuildMemberFilterExpression(filterIds));
                }
                else if (managedDepartmentIds.Any())
                {
                    var departmentUserIds = await _context.CompanyUsers
                        .Where(cu => cu.CompanyId == companyId.Value &&
                                    cu.DepartmentId.HasValue &&
                                    managedDepartmentIds.Contains(cu.DepartmentId.Value) &&
                                    !(cu.IsDeleted ?? false))
                        .Select(cu => cu.UserId)
                        .ToListAsync();

                    if (filterIds.Any())
                    {
                        // Ensure all requested users are in the department
                        foreach(var fid in filterIds)
                        {
                            if (int.TryParse(fid, out int parsedFid) && !departmentUserIds.Contains(parsedFid))
                            {
                                return Error<PagedResult<TaskDto>>($"User {parsedFid} is not in your managed department.", StatusCodes.Status403Forbidden);
                            }
                        }

                        query = query.Where(BuildMemberFilterExpression(filterIds));
                    }
                    else
                    {
                        // No specific members requested, but must only see department users' tasks
                        var deptUserIdsStrings = departmentUserIds.Select(id => id.ToString()).ToList();
                        if (deptUserIdsStrings.Any())
                        {
                            query = query.Where(BuildMemberFilterExpression(deptUserIdsStrings));
                        }
                        else
                        {
                            query = query.Where(t => false); 
                        }
                    }
                }
                else if (canReadAssignedTasks)
                {
                    query = query.Where(BuildMemberFilterExpression(new List<string> { currentUserId.Value.ToString() }));

                    if (filterIds.Any())
                    {
                        // Ensure they are only filtering for themselves
                        if (filterIds.Count > 1 || (filterIds.Count == 1 && filterIds[0] != currentUserId.Value.ToString()))
                        {
                            return Error<PagedResult<TaskDto>>("You can only filter your own assigned tasks.", StatusCodes.Status403Forbidden);
                        }
                    }
                }
                else
                {
                    return Error<PagedResult<TaskDto>>("You don't have permission to view tasks.", StatusCodes.Status403Forbidden);
                }

                // Application filtering
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(t => t.Title!.Contains(search) || (t.Description != null && t.Description.Contains(search)));
                }

                if (!string.IsNullOrWhiteSpace(statusNames))
                {
                    var statusList = statusNames.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                    query = query.Where(t => t.StatusNavigation != null && statusList.Contains(t.StatusNavigation.Name));
                }

                if (priorityId.HasValue)
                {
                    query = query.Where(t => t.PriorityId == priorityId.Value);
                }

                if (startDateFrom.HasValue) query = query.Where(t => t.StartDate >= startDateFrom.Value);
                if (startDateTo.HasValue) query = query.Where(t => t.StartDate <= startDateTo.Value);
                if (endDateFrom.HasValue) query = query.Where(t => t.EndDate >= endDateFrom.Value);
                if (endDateTo.HasValue) query = query.Where(t => t.EndDate <= endDateTo.Value);

                int totalCount = await query.CountAsync();
                List<Models.Task> pagedEntities;

                if (sortBy.Equals("progression", StringComparison.OrdinalIgnoreCase))
                {
                    // For non-mapped progression, we fetch minimal info to sort in-memory
                    var itemsToCalculate = await query
                        .Select(t => new
                        {
                            Entity = t,
                            StatusName = t.StatusNavigation != null ? t.StatusNavigation.Name : "",
                            SubtaskStatuses = t.SubTasks.Where(st => !st.IsDeleted).Select(st => st.StatusNavigation != null ? st.StatusNavigation.Name : "")
                        }).ToListAsync();

                    var sorted = sortOrder.ToLower() == "desc"
                        ? itemsToCalculate.OrderByDescending(x => CalculateProgression(x.StatusName, x.SubtaskStatuses))
                        : itemsToCalculate.OrderBy(x => CalculateProgression(x.StatusName, x.SubtaskStatuses));

                    pagedEntities = sorted.Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.Entity).ToList();
                }
                else
                {
                    query = ApplySortToQuery(query, sortBy, sortOrder);
                    pagedEntities = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                }

                // Optimized fetching of paged data
                var pagedIds = pagedEntities.Select(t => t.TaskId).ToList();
                var tasksWithIncludes = await _context.Tasks
                    .Include(t => t.StatusNavigation)
                    .Include(t => t.Priority)
                    .Include(t => t.TaskList)
                    .Include(t => t.AssignedToNavigation)
                        .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId.Value))
                    .Include(t => t.Company)
                    .Include(t => t.Project)
                    .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                        .ThenInclude(st => st.StatusNavigation)
                    .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                        .ThenInclude(st => st.Priority)
                    .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                        .ThenInclude(st => st.AssignedToNavigation)
                            .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId.Value))
                    .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                        .ThenInclude(st => st.Project)
                    .Include(t => t.TaskDocuments.Where(td => !td.IsDeleted))
                    .Where(t => pagedIds.Contains(t.TaskId))
                    .ToListAsync();

                // Maintain the sort order from pagedEntities
                var taskEntities = pagedIds.Select(id => tasksWithIncludes.First(t => t.TaskId == id)).ToList();

                var taskDtos = taskEntities.Select(t => MapTaskToDto(t, companyId.Value)).ToList();

                // Resolve AssignedUsers for paged tasks in a single query
                await ResolveAssignedUsers(taskDtos, taskEntities, companyId.Value);

                var pagedResult = new PagedResult<TaskDto>
                {
                    Items = taskDtos,
                    TotalCount = totalCount,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    TotalTasks = totalCount
                };

                _logger.LogInformation($"Retrieved {taskDtos.Count} tasks (Total: {totalCount}) for User ID: {currentUserId}");
                return Success("Company tasks retrieved successfully.", pagedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetAllTasks for company {GetCompanyIdFromClaims()}.");
                return Error<PagedResult<TaskDto>>("An error occurred while retrieving company tasks.", StatusCodes.Status500InternalServerError);
            }
        }

        private int CalculateProgression(string statusName, IEnumerable<string> subtaskStatuses)
        {
            if (subtaskStatuses.Any())
            {
                double total = subtaskStatuses.Sum(s => s.Equals("Completed", StringComparison.OrdinalIgnoreCase) ? 100 : 0);
                return (int)Math.Round(total / subtaskStatuses.Count());
            }
            return statusName.Equals("Completed", StringComparison.OrdinalIgnoreCase) ? 100 : 0;
        }

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

        private IQueryable<Models.Task> ApplySortToQuery(IQueryable<Models.Task> query, string sortBy, string sortOrder)
        {
            bool desc = sortOrder.ToLower() == "desc";
            return sortBy.ToLower() switch
            {
                "taskid" => desc ? query.OrderByDescending(t => t.TaskId) : query.OrderBy(t => t.TaskId),
                "title" => desc ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title),
                "statusname" => desc ? query.OrderByDescending(t => t.StatusNavigation.Name) : query.OrderBy(t => t.StatusNavigation.Name),
                "enddate" => desc ? query.OrderByDescending(t => t.EndDate) : query.OrderBy(t => t.EndDate),
                "priorityname" => desc ? query.OrderByDescending(t => t.Priority.Name) : query.OrderBy(t => t.Priority.Name),
                "assignedtouserid" => desc ? query.OrderByDescending(t => t.AssignedTo) : query.OrderBy(t => t.AssignedTo),
                "listname" => desc ? query.OrderByDescending(t => t.TaskList.ListName) : query.OrderBy(t => t.TaskList.ListName),
                "taskorder" => desc ? query.OrderByDescending(t => t.TaskOrder) : query.OrderBy(t => t.TaskOrder),
                "companyname" => desc ? query.OrderByDescending(t => t.Company.CompanyName) : query.OrderBy(t => t.Company.CompanyName),
                "startdate" => desc ? query.OrderByDescending(t => t.StartDate) : query.OrderBy(t => t.StartDate),
                "estimatedhours" => desc ? query.OrderByDescending(t => t.EstimatedHours) : query.OrderBy(t => t.EstimatedHours),
                _ => desc ? query.OrderByDescending(t => t.TaskId) : query.OrderBy(t => t.TaskId)
            };
        }

        private TaskDto MapTaskToDto(Models.Task t, int companyId)
        {
            return new TaskDto
            {
                TaskId = t.TaskId,
                Title = t.Title,
                StatusName = t.StatusNavigation?.Name,
                StartDate = t.StartDate,
                EndDate = t.EndDate,
                PriorityName = t.Priority?.Name,
                Description = t.Description,
                AssignedToUserId = t.AssignedTo,
                AssignedToUserName = t.AssignedToNavigation != null && t.AssignedToNavigation.CompanyUserUsers.Any()
                                    ? $"{t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId)!.FirstName} {t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId)!.LastName}".Trim()
                                    : null,
                AssignedToUserAvatarUrl = t.AssignedToNavigation != null && t.AssignedToNavigation.CompanyUserUsers.Any()
                                    ? t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId)?.ProfilePhotoUrl
                                    : null,
                TaskListId = t.TaskList?.TaskListId,
                ListName = t.TaskList?.ListName,
                TaskListDescription = t.TaskList?.Description,
                ListOrder = t.TaskList?.ListOrder,
                ProjectId = t.ProjectId,
                Name = t.Project?.Name,
                CompanyName = t.Company?.CompanyName,
                ParentTaskId = t.ParentTaskId,
                EstimatedHours = t.EstimatedHours,
                Progression = t.Progression,
                Subtasks = t.SubTasks.Select(st => new TaskDto
                {
                    TaskId = st.TaskId,
                    Title = st.Title,
                    StatusName = st.StatusNavigation?.Name,
                    StartDate = st.StartDate,
                    EndDate = st.EndDate,
                    PriorityName = st.Priority?.Name,
                    Description = st.Description,
                    AssignedToUserId = st.AssignedTo,
                    AssignedToUserName = st.AssignedToNavigation != null && st.AssignedToNavigation.CompanyUserUsers.Any()
                                        ? $"{st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId)!.FirstName} {st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId)!.LastName}".Trim()
                                        : null,
                    AssignedToUserAvatarUrl = st.AssignedToNavigation != null && st.AssignedToNavigation.CompanyUserUsers.Any()
                                        ? st.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId)?.ProfilePhotoUrl
                                        : null,
                    TaskListId = st.TaskListId,
                    ListName = st.TaskList?.ListName,
                    TaskListDescription = st.TaskList?.Description,
                    ListOrder = st.TaskList?.ListOrder,
                    ProjectId = st.ProjectId,
                    Name = st.Project?.Name,
                    CompanyName = st.Company?.CompanyName,
                    ParentTaskId = st.ParentTaskId,
                    EstimatedHours = st.EstimatedHours,
                    Progression = st.Progression
                }).ToList(),
                Documents = t.TaskDocuments.Select(td => new TaskDocumentDto
                {
                    DocumentId = td.DocumentId,
                    TaskId = td.TaskId,
                    DocumentName = td.DocumentName,
                    FilePath = td.FilePath,
                    DocumentType = td.DocumentType,
                    Description = td.Description,
                    CreatedAt = td.CreatedAt,
                }).ToList()
            };
        }

        private async Task ResolveAssignedUsers(List<TaskDto> taskDtos, List<Models.Task> taskEntities, int companyId)
        {
            var tasksWithAssignedIds = taskEntities.Where(t => !string.IsNullOrEmpty(t.AssignedUserIds)).ToList();
            if (tasksWithAssignedIds.Any())
            {
                var allAssignedUserIds = tasksWithAssignedIds
                    .SelectMany(t => t.AssignedUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    .Select(id => int.Parse(id.Trim()))
                    .Distinct()
                    .ToList();

                var usersData = await _context.Users
                    .Where(u => allAssignedUserIds.Contains(u.UserId))
                    .Select(u => new AssignedUserInfo
                    {
                        UserId = u.UserId,
                        UserName = u.CompanyUserUsers
                            .Where(cu => cu.CompanyId == companyId)
                            .Select(cu => $"{cu.FirstName} {cu.LastName}".Trim())
                            .FirstOrDefault(),
                        AvatarUrl = u.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId).ProfilePhotoUrl
                    }).ToListAsync();

                foreach (var dto in taskDtos)
                {
                    var task = taskEntities.First(t => t.TaskId == dto.TaskId);
                    if (!string.IsNullOrEmpty(task.AssignedUserIds))
                    {
                        var ids = task.AssignedUserIds.Split(',').Select(id => int.Parse(id.Trim())).ToList();
                        dto.AssignedUsers = usersData.Where(u => ids.Contains(u.UserId)).ToList();
                    }
                    else
                    {
                        dto.AssignedUsers = new List<AssignedUserInfo>();
                    }
                }
            }
        }


        private static Expression<Func<TaskDto, object>> GetSortExpression(string sortBy)
        {
            return sortBy.ToLower() switch
            {
                "taskid" => t => t.TaskId,
                "title" => t => t.Title!,
                "statusname" => t => t.StatusName!,
                "enddate" => t => t.EndDate!,
                "priorityname" => t => t.PriorityName!,
                "assignedtouserid" => t => t.AssignedToUserId!,
                "listname" => t => t.ListName!,
                "taskorder" => t => t.ListOrder!,
                "companyname" => t => t.CompanyName!,
                "startdate" => t => t.StartDate!,
                "estimatedhours" => t => t.EstimatedHours!,
                "progression" => t => t.Progression,
                "assignedtousername" => t => t.AssignedToUserName!,
                _ => t => t.TaskId
            };
        }

        // POST: api/Tasks/tasklists/{taskListId}/tasks
        // This endpoint allows creating a new task within a specific task list.
        [HttpPost("tasklists/{taskListId}/tasks")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<TaskDto>>> CreateTask(int taskListId, TaskInputDto dto)
        {
            try
            {
                var companyId = GetCompanyIdFromClaims();
                if (!companyId.HasValue)
                {
                    return Error<TaskDto>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                var currentUserId = GetCurrentUserIdFromClaims();
                if (!currentUserId.HasValue)
                {
                    return Error<TaskDto>("User ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                bool isTaskTodo = (!dto.ProjectId.HasValue || dto.ProjectId.Value == 0) &&
                                  (!dto.TaskListId.HasValue || dto.TaskListId.Value == 0) &&
                                  taskListId == 0;

                if (isTaskTodo)
                {
                    // --- Logic for creating a personal "TaskTodo" ---
                    _logger.LogInformation("Attempting to create a personal TaskTodo for user {currentUserId}.", currentUserId.Value);

                    // Validation for personal todos
                    if (dto.AssignedToUserId.HasValue && dto.AssignedToUserId.Value != currentUserId.Value)
                    {
                        return Error<TaskDto>("Personal tasks (todos) must be assigned to the user creating them.", StatusCodes.Status400BadRequest);
                    }
                    if (dto.ParentTaskId.HasValue)
                    {
                        bool parentTodoExists = await _context.TaskTodos.AnyAsync(tt => tt.TaskTodoId == dto.ParentTaskId.Value && tt.CompanyId == companyId.Value && !tt.IsDeleted);
                        if (!parentTodoExists)
                        {
                            return Error<TaskDto>("Parent TaskTodo not found or not accessible.", StatusCodes.Status400BadRequest);
                        }
                    }

                    var taskTodo = new Models.TaskTodo
                    {
                        Title = dto.Title,
                        Status = dto.StatusId,
                        StartDate = (!dto.StartDate.HasValue || dto.StartDate.Value.Year == 1) ? DateOnly.FromDateTime(DateTime.Now) : dto.StartDate,
                        EndDate = dto.EndDate,
                        PriorityId = dto.PriorityId,
                        Description = dto.Description,
                        CompanyId = companyId.Value,
                        AssignedTo = currentUserId.Value, // Always assign to the creator
                        EstimatedHours = dto.EstimatedHours,
                        ActualHours = dto.ActualHours,
                        TaskOrder = dto.TaskOrder,
                        ParentTaskId = dto.ParentTaskId,
                        CreatedBy = currentUserId.Value,
                        CreatedAt = DateTimeOffset.UtcNow,
                        IsActive = true,
                        IsDeleted = false
                    };

                    _context.TaskTodos.Add(taskTodo);
                    await _context.SaveChangesAsync();

                    // *** REFACTORED ***
                    // Re-fetch the created todo with related data in a single, efficient query to build the DTO.
                    var taskDto = await _context.TaskTodos
                        .AsNoTracking()
                        .Where(tt => tt.TaskTodoId == taskTodo.TaskTodoId)
                        .Select(tt => new TaskDto
                        {
                            TaskId = tt.TaskTodoId, // Map TaskTodoId to TaskId
                            Title = tt.Title,
                            StatusName = tt.StatusNavigation.Name,
                            StartDate = tt.StartDate,
                            EndDate = tt.EndDate,
                            PriorityName = tt.Priority.Name,
                            Description = tt.Description,
                            AssignedToUserId = tt.AssignedTo,
                            AssignedToUserName = tt.AssignedToNavigation.CompanyUserUsers
                                                  .Where(cu => cu.CompanyId == companyId.Value)
                                                  .Select(cu => $"{cu.FirstName} {cu.LastName}".Trim())
                                                  .FirstOrDefault(),
                            CompanyName = tt.Company.CompanyName,
                            ParentTaskId = tt.ParentTaskId,
                            EstimatedHours = tt.EstimatedHours,
                            Progression = tt.Progression,
                            // Initialize empty lists for a new item
                            Subtasks = new List<TaskDto>(),
                            Documents = new List<TaskDocumentDto>()
                        })
                        .FirstOrDefaultAsync();

                    return Success("Personal Task (Todo) created successfully.", taskDto, StatusCodes.Status201Created);
                }
                else
                {
                    // --- SEQUENTIAL VALIDATIONS ---
                    var taskListEntity = await GetTaskListForCompany(taskListId, companyId.Value);
                    if (taskListEntity == null || taskListEntity.IsDeleted)
                    {
                        return Error<TaskDto>("The specified TaskList does not exist or is not accessible.", StatusCodes.Status404NotFound);
                    }

                    if (dto.TaskListId.HasValue && dto.TaskListId.Value != taskListId)
                    {
                        return Error<TaskDto>("TaskListId in body must match TaskListId in URL.", StatusCodes.Status400BadRequest);
                    }

                    if (dto.ParentTaskId.HasValue)
                    {
                        var parentTaskExists = await _context.Tasks.AnyAsync(t => t.TaskId == dto.ParentTaskId.Value && t.TaskListId == taskListId && !t.IsDeleted);
                        if (!parentTaskExists)
                        {
                            return Error<TaskDto>("Parent Task not found or not accessible within this TaskList.", StatusCodes.Status400BadRequest);
                        }
                    }

                    // Prepare user assignment list
                    List<int> userIdsToAssign = new List<int>();
                    if (dto.AssignedUserIds != null && dto.AssignedUserIds.Any())
                    {
                        userIdsToAssign = dto.AssignedUserIds.Distinct().ToList();
                    }
                    else if (dto.AssignedToUserId.HasValue)
                    {
                        userIdsToAssign.Add(dto.AssignedToUserId.Value);
                    }
                    else
                    {
                        userIdsToAssign.Add(currentUserId.Value);
                    }

                    var validUsersCount = await _context.CompanyUsers
                        .CountAsync(cu => userIdsToAssign.Contains(cu.UserId)
                            && cu.CompanyId == companyId.Value
                            && !(cu.IsDeleted ?? false));

                    if (validUsersCount != userIdsToAssign.Count)
                    {
                        // Identify which users are invalid (rare case, so we do it only if count mismatch)
                        var validUserIds = await _context.CompanyUsers
                            .Where(cu => userIdsToAssign.Contains(cu.UserId) && cu.CompanyId == companyId.Value && !(cu.IsDeleted ?? false))
                            .Select(cu => cu.UserId)
                            .ToListAsync();
                        var invalidUsers = userIdsToAssign.Except(validUserIds).ToList();
                        return Error<TaskDto>($"Users with IDs {string.Join(", ", invalidUsers)} not found or do not belong to your company.", StatusCodes.Status400BadRequest);
                    }

                    var task = new Models.Task
                    {
                        Title = dto.Title,
                        Status = dto.StatusId,
                        StartDate = (!dto.StartDate.HasValue || dto.StartDate.Value.Year == 1) ? DateOnly.FromDateTime(DateTime.Now) : dto.StartDate,
                        EndDate = dto.EndDate,
                        PriorityId = dto.PriorityId,
                        TaskListId = taskListId,
                        Description = dto.Description,
                        ProjectId = taskListEntity.ProjectId,
                        CompanyId = companyId.Value,
                        AssignedTo = userIdsToAssign.First(),
                        AssignedUserIds = string.Join(",", userIdsToAssign),
                        EstimatedHours = dto.EstimatedHours,
                        ActualHours = dto.ActualHours,
                        TaskOrder = dto.TaskOrder,
                        ParentTaskId = dto.ParentTaskId,
                        CreatedBy = currentUserId.Value,
                        CreatedAt = DateTimeOffset.UtcNow,
                        IsActive = true,
                        IsDeleted = false
                    };

                    _context.Tasks.Add(task);
                    await _context.SaveChangesAsync();

                    // --- BACKGROUND NOTIFICATIONS ---
                    _ = Task.Run(async () =>
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var emailService = scope.ServiceProvider.GetRequiredService<ITaskEmailService>();
                        var notificationService = scope.ServiceProvider.GetRequiredService<ITaskNotificationService>();
                        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TasksController>>();

                        try
                        {
                            await emailService.SendTaskCreatedEmailAsync(task.TaskId, currentUserId.Value);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Background: Failed task creation email for TaskId {TaskId}", task.TaskId);
                        }

                        foreach (var userId in userIdsToAssign)
                        {
                            try
                            {
                                await notificationService.SendTaskAssignmentNotificationAsync(task.TaskId, userId);
                                await emailService.SendTaskAssignedEmailAsync(task.TaskId, userId, currentUserId.Value);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Background: Failed to notify user {UserId} for TaskId {TaskId}", userId, task.TaskId);
                            }
                        }
                    });

                    var createdTaskDto = await _context.Tasks
                        .AsNoTracking()
                        .Where(t => t.TaskId == task.TaskId)
                        .Select(t => new TaskDto
                        {
                            TaskId = t.TaskId,
                            Title = t.Title,
                            StatusName = t.StatusNavigation.Name,
                            StartDate = t.StartDate,
                            EndDate = t.EndDate,
                            PriorityName = t.Priority.Name,
                            Description = t.Description,
                            AssignedToUserId = t.AssignedTo,
                            AssignedToUserName = t.AssignedToNavigation.CompanyUserUsers
                                                  .Where(cu => cu.CompanyId == companyId.Value)
                                                  .Select(cu => $"{cu.FirstName} {cu.LastName}".Trim())
                                                  .FirstOrDefault(),
                            TaskListId = t.TaskList.TaskListId,
                            ListName = t.TaskList.ListName,
                            TaskListDescription = t.TaskList.Description,
                            ListOrder = t.TaskList.ListOrder,
                            ProjectId = t.ProjectId,
                            Name = t.Project.Name,
                            CompanyName = t.Company.CompanyName,
                            ParentTaskId = t.ParentTaskId,
                            EstimatedHours = t.EstimatedHours,
                            Progression = t.Progression,
                            Subtasks = new List<TaskDto>(),
                            Documents = new List<TaskDocumentDto>()
                        })
                        .FirstOrDefaultAsync();

                    return Success("Task created successfully.", createdTaskDto, StatusCodes.Status201Created);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating task in list {taskListId}.", taskListId);
                return Error<TaskDto>("An unexpected error occurred while creating the item.", StatusCodes.Status500InternalServerError);
            }
        }

        // PUT: api/Tasks/{taskId}
        // This endpoint updates an existing task by its ID (flatter API).
        [HttpPut("{id}")]
        [Authorize(Policy = "CanTaskUpdate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateTask(int id, TaskInputDto dto)
        {
            try
            {
                var companyId = GetCompanyIdFromClaims();
                if (!companyId.HasValue)
                    return Error("Company ID not found in claims.", StatusCodes.Status401Unauthorized);

                var currentUserId = GetCurrentUserIdFromClaims();
                if (!currentUserId.HasValue)
                    return Error("User ID not found in claims.", StatusCodes.Status401Unauthorized);

                // --- 1. ATTEMPT TO UPDATE AS A REGULAR TASK ---
                var task = await _context.Tasks
                    .Where(t => t.TaskId == id && t.CompanyId == companyId.Value && !t.IsDeleted)
                    .FirstOrDefaultAsync();

                if (task != null)
                {
                    _logger.LogInformation($"Updating regular Task {id}.");

                    var originalStatusId = task.Status;
                    var originalAssignedUserIds = string.IsNullOrEmpty(task.AssignedUserIds)
                        ? new List<int>()
                        : task.AssignedUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(uid => int.Parse(uid.Trim())).ToList();

                    // Basic Validations
                    if (task.ParentTaskId == null && dto.ParentTaskId.HasValue)
                        return Error("Cannot change a top-level task into a subtask.", StatusCodes.Status400BadRequest);
                    
                    if (task.ParentTaskId.HasValue && (!dto.ParentTaskId.HasValue || dto.ParentTaskId.Value != task.ParentTaskId.Value))
                        return Error("Cannot change a subtask's parent via this endpoint.", StatusCodes.Status400BadRequest);

                    if (!dto.TaskListId.HasValue || dto.TaskListId.Value == 0)
                        return Error("TaskListId is required.", StatusCodes.Status400BadRequest);

                    var newTaskList = await GetTaskListForCompany(dto.TaskListId.Value, companyId.Value);
                    if (newTaskList == null || newTaskList.IsDeleted)
                        return Error("Invalid TaskList.", StatusCodes.Status400BadRequest);

                    // Update Fields
                    task.TaskListId = dto.TaskListId.Value;
                    task.ProjectId = newTaskList.ProjectId;
                    task.Title = dto.Title;
                    task.Status = dto.StatusId;
                    task.StartDate = (dto.StartDate.HasValue && dto.StartDate.Value.Year == 1) ? task.StartDate : dto.StartDate;
                    task.EndDate = dto.EndDate;
                    task.PriorityId = dto.PriorityId;
                    task.Description = dto.Description;
                    task.EstimatedHours = dto.EstimatedHours;
                    task.ActualHours = dto.ActualHours;
                    task.TaskOrder = dto.TaskOrder;
                    task.UpdatedAt = DateTimeOffset.UtcNow;
                    task.UpdatedBy = currentUserId.Value;

                    // Handle Assigned Users
                    List<int> newUserIds = new List<int>();
                    if (dto.AssignedUserIds != null && dto.AssignedUserIds.Any())
                    {
                        newUserIds = dto.AssignedUserIds.Distinct().ToList();
                    }
                    else if (dto.AssignedToUserId.HasValue)
                    {
                        newUserIds.Add(dto.AssignedToUserId.Value);
                    }

                    if (newUserIds.Any())
                    {
                        var validUsers = await _context.CompanyUsers
                            .Where(cu => newUserIds.Contains(cu.UserId) && cu.CompanyId == companyId.Value && !(cu.IsDeleted ?? false))
                            .Select(cu => cu.UserId)
                            .ToListAsync();

                        if (validUsers.Count != newUserIds.Count)
                            return Error("Some assigned users are invalid.", StatusCodes.Status400BadRequest);

                        task.AssignedUserIds = string.Join(",", newUserIds);
                        task.AssignedTo = newUserIds.First();
                    }
                    else
                    {
                        task.AssignedUserIds = null;
                        task.AssignedTo = null;
                    }

                    await _context.SaveChangesAsync();

                    // --- BACKGROUND NOTIFICATIONS ---
                    _ = Task.Run(async () =>
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var notificationService = scope.ServiceProvider.GetRequiredService<ITaskNotificationService>();
                        var dbContext = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();
                        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TasksController>>();

                        // 1. Status Change Notification
                        if (originalStatusId != task.Status)
                        {
                            try
                            {
                                var statusName = await dbContext.CompanyTaskStatuses
                                    .Where(ts => ts.Id == task.Status && ts.CompanyId == companyId.Value)
                                    .Select(ts => ts.Name).FirstOrDefaultAsync() ?? "Unknown";
                                await notificationService.SendTaskStatusChangeNotificationAsync(task.TaskId, statusName);
                            }
                            catch (Exception ex) { logger.LogError(ex, "Background Update: Failed status notification"); }
                        }

                        // 2. New Assignment Notifications
                        var newlyAssigned = newUserIds.Except(originalAssignedUserIds).ToList();
                        foreach (var uid in newlyAssigned)
                        {
                            try { await notificationService.SendTaskAssignmentNotificationAsync(task.TaskId, uid); }
                            catch (Exception ex) { logger.LogError(ex, "Background Update: Failed assignment notification"); }
                        }
                    });

                    return NoContent();
                }

                // --- 2. ATTEMPT TO UPDATE AS A TASK TODO ---
                var taskTodo = await _context.TaskTodos
                    .Where(tt => tt.TaskTodoId == id && tt.CompanyId == companyId.Value && !tt.IsDeleted)
                    .FirstOrDefaultAsync();

                if (taskTodo != null)
                {
                    _logger.LogInformation($"Updating TaskTodo {id}.");
                    var originalStatusId = taskTodo.Status;

                    // Validations
                    if (taskTodo.ParentTaskId == null && dto.ParentTaskId.HasValue)
                        return Error("Cannot change a top-level todo into a sub-todo.", StatusCodes.Status400BadRequest);
                    
                    if (dto.ProjectId.HasValue && dto.ProjectId.Value != 0)
                        return Error("TaskTodo cannot have a ProjectId.", StatusCodes.Status400BadRequest);

                    // Update Fields
                    taskTodo.Title = dto.Title;
                    taskTodo.Status = dto.StatusId;
                    taskTodo.StartDate = (dto.StartDate.HasValue && dto.StartDate.Value.Year == 1) ? taskTodo.StartDate : dto.StartDate;
                    taskTodo.EndDate = dto.EndDate;
                    taskTodo.PriorityId = dto.PriorityId;
                    taskTodo.Description = dto.Description;
                    taskTodo.EstimatedHours = dto.EstimatedHours;
                    taskTodo.ActualHours = dto.ActualHours;
                    taskTodo.TaskOrder = dto.TaskOrder;
                    taskTodo.UpdatedAt = DateTimeOffset.UtcNow;
                    taskTodo.UpdatedBy = currentUserId.Value;

                    await _context.SaveChangesAsync();

                    // Notifications
                    if (originalStatusId != taskTodo.Status)
                    {
                        try {
                            var statusName = await _context.CompanyTaskStatuses
                                .Where(ts => ts.Id == taskTodo.Status && ts.CompanyId == companyId.Value)
                                .Select(ts => ts.Name).FirstOrDefaultAsync() ?? "Unknown";
                            await _taskNotificationService.SendTaskStatusChangeNotificationAsync(taskTodo.TaskTodoId, statusName);
                        } catch (Exception ex) { _logger.LogError(ex, "Failed todo notification"); }
                    }

                    return NoContent();
                }

                return Error("Item not found.", StatusCodes.Status404NotFound);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating item {id}.");
                return Error("An unexpected error occurred.", StatusCodes.Status500InternalServerError);
            }
        }


        // DELETE: api/Tasks/{taskId}
        // This endpoint performs a soft delete on a task by setting IsDeleted to true (flatter API).
        [HttpDelete("{id}")]
        [Authorize(Policy = "CanTaskDelete")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteTask(int id)
        {
            try
            {
                var companyId = GetCompanyIdFromClaims();
                if (!companyId.HasValue)
                {
                    return Error("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                var currentUserId = GetCurrentUserIdFromClaims();
                if (!currentUserId.HasValue)
                {
                    return Error("User ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                // --- ATTEMPT TO DELETE AS A REGULAR TASK FIRST ---
                var task = await _context.Tasks
                                         .Include(t => t.SubTasks.Where(st => !st.IsDeleted)) // Include subtasks for soft delete
                                         .Where(t => t.TaskId == id && t.CompanyId == companyId.Value && !t.IsDeleted)
                                         .FirstOrDefaultAsync();

                if (task != null)
                {
                    _logger.LogInformation($"Attempting to soft-delete regular Task with ID: {id}.");

                    // Soft delete all direct subtasks
                    foreach (var subtask in task.SubTasks)
                    {
                        subtask.IsDeleted = true;
                        subtask.UpdatedAt = DateTimeOffset.UtcNow;
                        subtask.UpdatedBy = currentUserId.Value;
                    }

                    // Also soft delete all documents related to this task
                    var documentsToDelete = await _context.TaskDocuments
                                                          .Where(td => td.TaskId == id && td.CompanyId == companyId.Value && !td.IsDeleted)
                                                          .ToListAsync();
                    foreach (var document in documentsToDelete)
                    {
                        document.IsDeleted = true;
                        document.UpdatedAt = DateTimeOffset.UtcNow;
                    }

                    task.IsDeleted = true;
                    task.UpdatedAt = DateTimeOffset.UtcNow;
                    task.UpdatedBy = currentUserId.Value;

                    await _context.SaveChangesAsync();
                    return NoContent();
                }

                // --- IF NOT A REGULAR TASK, ATTEMPT TO DELETE AS A TASK TODO ---
                var taskTodo = await _context.TaskTodos
                                             .Include(tt => tt.SubTasks.Where(st => !st.IsDeleted)) // Include sub-todos
                                             .Where(tt => tt.TaskTodoId == id && tt.CompanyId == companyId.Value && !tt.IsDeleted)
                                             .FirstOrDefaultAsync();

                if (taskTodo != null)
                {
                    _logger.LogInformation($"Attempting to soft-delete TaskTodo with ID: {id}.");

                    // Soft delete all direct sub-todos
                    foreach (var subTodo in taskTodo.SubTasks)
                    {
                        subTodo.IsDeleted = true;
                        subTodo.UpdatedAt = DateTimeOffset.UtcNow;
                        subTodo.UpdatedBy = currentUserId.Value;
                    }

                    taskTodo.IsDeleted = true;
                    taskTodo.UpdatedAt = DateTimeOffset.UtcNow;
                    taskTodo.UpdatedBy = currentUserId.Value;

                    await _context.SaveChangesAsync();
                    return NoContent();
                }

                // If not found as either a Task or TaskTodo
                return Error("Item not found or already deleted.", StatusCodes.Status404NotFound);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting item with ID {id}.");
                return Error("An error occurred while deleting the item.", StatusCodes.Status500InternalServerError);
            }
        }

        // POST: api/Tasks/projects/{projectId}/tasklists
        // This endpoint creates a new task list within a specific project.
        [HttpPost("projects/{projectId}/tasklists")]
        [Authorize(Policy = "CanTaskListCreate")]
        [ProducesResponseType(typeof(ApiResponse<TaskListDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)] // Added for consistency
        public async Task<ActionResult<ApiResponse<TaskListDto>>> CreateTaskList(int projectId, TaskListInputDto dto)
        {
            try
            {
                var companyId = GetCompanyIdFromClaims();
                if (!companyId.HasValue)
                {
                    return Error<TaskListDto>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                var project = await GetProjectForCompany(projectId, companyId.Value);
                if (project == null)
                {
                    return Error<TaskListDto>("Project not found or not accessible.", StatusCodes.Status404NotFound);
                }

                var currentUserId = GetCurrentUserIdFromClaims();
                if (!currentUserId.HasValue)
                {
                    return Error<TaskListDto>("User ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                var taskList = new TaskList
                {
                    ListName = dto.ListName,
                    Description = dto.Description,
                    ListOrder = dto.ListOrder,
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate,
                    ProjectId = projectId,
                    CompanyId = companyId.Value,
                    Status = "active",
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = currentUserId.Value,
                    IsActive = true,
                    IsDeleted = false
                };

                _context.TaskLists.Add(taskList);
                await _context.SaveChangesAsync();

                var taskListDto = new TaskListDto
                {
                    TaskListId = taskList.TaskListId,
                    ListName = taskList.ListName,
                    Description = taskList.Description,
                    ListOrder = taskList.ListOrder,
                    Status = taskList.Status,
                    ProjectId = taskList.ProjectId,
                    CompanyId = taskList.CompanyId,
                    StartDate = taskList.StartDate,
                    EndDate = taskList.EndDate
                };

                return Success("Task List created successfully.", taskListDto, StatusCodes.Status201Created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating task list in project ID {projectId}.");
                return Error<TaskListDto>("An error occurred while creating the task list.", StatusCodes.Status500InternalServerError);
            }
        }

        // GET: api/Tasks/projects/{projectId}/tasklists
        // This endpoint retrieves all task lists for a specific project, with filtering, paging, and sorting.
        [HttpGet("projects/{projectId}/tasklists")]
        [Authorize(Policy = "CanTaskListRead")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<TaskListDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PagedResult<TaskListDto>>>> GetTaskLists(
           int projectId,
           [FromQuery] int page = 1,
           [FromQuery] int pageSize = 5,
           [FromQuery] string sortBy = "DueDate",
           [FromQuery] string sortOrder = "desc",
           [FromQuery] string? search = null,
           [FromQuery] string? status = null,
           [FromQuery] DateOnly? startDateFrom = null,
           [FromQuery] DateOnly? startDateTo = null,
           [FromQuery] DateOnly? endDateFrom = null,
           [FromQuery] DateOnly? endDateTo = null)
        {
            try
            {
                var companyId = GetCompanyIdFromClaims();
                if (!companyId.HasValue)
                {
                    return Error<PagedResult<TaskListDto>>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                var project = await GetProjectForCompany(projectId, companyId.Value);
                if (project == null)
                {
                    return Error<PagedResult<TaskListDto>>("Project not found or not accessible.", StatusCodes.Status404NotFound);
                }

                IQueryable<TaskList> query = _context.TaskLists
                    .Where(tl => tl.CompanyId == companyId.Value && tl.ProjectId == projectId && !tl.IsDeleted);

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(tl => tl.Status == status);
                }
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(tl => tl.ListName.Contains(search) ||
                                              (tl.Description != null && tl.Description.Contains(search)));
                }
                if (startDateFrom.HasValue)
                {
                    query = query.Where(tl => tl.StartDate.HasValue && tl.StartDate.Value >= startDateFrom.Value);
                }
                if (startDateTo.HasValue)
                {
                    query = query.Where(tl => tl.StartDate.HasValue && tl.StartDate.Value <= startDateTo.Value);
                }
                if (endDateFrom.HasValue)
                {
                    query = query.Where(tl => tl.EndDate.HasValue && tl.EndDate.Value >= endDateFrom.Value);
                }
                if (endDateTo.HasValue)
                {
                    query = query.Where(tl => tl.EndDate.HasValue && tl.EndDate.Value <= endDateTo.Value);
                }

                var totalCount = await query.CountAsync();

                var taskListPropertyMap = new Dictionary<string, Expression<Func<TaskList, object>>>
                {
                    { "tasklistid", tl => tl.TaskListId },
                    { "listname", tl => tl.ListName },
                    { "listorder", tl => tl.ListOrder! },
                    { "status", tl => tl.Status },
                    { "startdate", tl => tl.StartDate! },
                    { "enddate", tl => tl.EndDate! }
                };

                if (taskListPropertyMap.TryGetValue(sortBy.ToLower(), out var sortExpression))
                {
                    if (sortOrder.ToLower() == "desc")
                    {
                        query = query.OrderByDescending(sortExpression);
                    }
                    else
                    {
                        query = query.OrderBy(sortExpression);
                    }
                }
                else
                {
                    query = query.OrderBy(tl => tl.TaskListId);
                }

                var items = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(tl => new TaskListDto
                    {
                        TaskListId = tl.TaskListId,
                        ListName = tl.ListName,
                        Description = tl.Description,
                        ListOrder = tl.ListOrder,
                        Status = tl.Status,
                        ProjectId = tl.ProjectId,
                        CompanyId = tl.CompanyId,
                        StartDate = tl.StartDate,
                        EndDate = tl.EndDate,
                        Tasks = tl.Tasks.Where(t => !t.IsDeleted)
                                        .Select(t => new TaskDto // Assuming tasks have an IsDeleted property
                                        {
                                            TaskId = t.TaskId,
                                            Title = t.Title,
                                            StatusName = t.StatusNavigation != null ? t.StatusNavigation.Name : null,
                                            Description = t.Description,
                                            StartDate = t.StartDate,
                                            EndDate = t.EndDate,
                                            Name = t.Project != null ? t.Project.Name : null,
                                            EstimatedHours = t.EstimatedHours,
                                            // Ensure AssignedToNavigation is loaded for nested TaskDto
                                            AssignedToUserId = t.AssignedTo,
                                            AssignedToUserName = t.AssignedToNavigation != null && t.AssignedToNavigation.CompanyUserUsers.Any()
                                                                ? $"{t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.FirstName} {t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.LastName}".Trim()
                                                                : null,
                                            PriorityName = t.Priority != null ? t.Priority.Name : null,

                                        }).ToList()
                    })
                    .ToListAsync();

                return Success("Task lists retrieved successfully.", new PagedResult<TaskListDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    TotalTasks = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting task lists for project ID {projectId}.");
                return Error<PagedResult<TaskListDto>>("An error occurred while retrieving task lists.", StatusCodes.Status500InternalServerError);
            }
        }

        // GET: api/Tasks/projects/{projectId}/tasklists/{id}
        // This endpoint retrieves a specific task list by its ID within a given project.
        [HttpGet("projects/{projectId}/tasklists/{id}")]
        [Authorize(Policy = "CanTaskListRead")]
        [ProducesResponseType(typeof(ApiResponse<TaskListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<TaskListDto>>> GetTaskListById(int projectId, int id)
        {
            try
            {
                var companyId = GetCompanyIdFromClaims();
                if (!companyId.HasValue)
                {
                    return Error<TaskListDto>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                var project = await GetProjectForCompany(projectId, companyId.Value);
                if (project == null)
                {
                    return Error<TaskListDto>("Project not found or not accessible.", StatusCodes.Status404NotFound);
                }

                var taskList = await _context.TaskLists
                    .Include(tl => tl.Tasks.Where(t => !t.IsDeleted))
                        .ThenInclude(t => t.Project)
                    .Include(tl => tl.Tasks.Where(t => !t.IsDeleted))
                        .ThenInclude(t => t.StatusNavigation)
                    .Include(tl => tl.Tasks.Where(t => !t.IsDeleted))
                        .ThenInclude(t => t.Priority)
                    .Include(tl => tl.Tasks.Where(t => !t.IsDeleted))
                        .ThenInclude(t => t.AssignedToNavigation) // Include User
                            .ThenInclude(u => u.CompanyUserUsers.Where(cu => cu.CompanyId == companyId.Value)) // ThenInclude CompanyUser for name
                    .FirstOrDefaultAsync(tl => tl.TaskListId == id && tl.ProjectId == projectId && tl.CompanyId == companyId.Value && !tl.IsDeleted);


                if (taskList == null)
                {
                    return Error<TaskListDto>("Task List not found or not accessible within this project.", StatusCodes.Status404NotFound);
                }

                var taskListDto = new TaskListDto
                {
                    TaskListId = taskList.TaskListId,
                    ListName = taskList.ListName,
                    Description = taskList.Description,
                    ListOrder = taskList.ListOrder,
                    Status = taskList.Status,
                    ProjectId = taskList.ProjectId,
                    CompanyId = taskList.CompanyId,
                    StartDate = taskList.StartDate,
                    EndDate = taskList.EndDate,
                    Tasks = taskList.Tasks
                                         .Select(t => new TaskDto
                                         {
                                             TaskId = t.TaskId,
                                             Title = t.Title,
                                             Description = t.Description,
                                             StartDate = t.StartDate,
                                             EndDate = t.EndDate,
                                             TaskListId = t.TaskListId,
                                             ListName = t.TaskList?.ListName,
                                             Name = t.Project != null ? t.Project.Name : null,
                                             StatusName = t.StatusNavigation != null ? t.StatusNavigation.Name : null,
                                             PriorityName = t.Priority != null ? t.Priority.Name : null,
                                             AssignedToUserId = t.AssignedTo,
                                             AssignedToUserName = t.AssignedToNavigation != null && t.AssignedToNavigation.CompanyUserUsers.Any()
                                                                 ? $"{t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.FirstName} {t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == companyId.Value)!.LastName}".Trim()
                                                                 : null,
                                             EstimatedHours = t.EstimatedHours
                                         })
                                         .ToList()
                };

                return Success("Task List retrieved successfully.", taskListDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting task list ID {id} for project ID {projectId}.");
                return Error<TaskListDto>("An error occurred while retrieving the task list.", StatusCodes.Status500InternalServerError);
            }
        }

        // PUT: api/Tasks/projects/{projectId}/tasklists/{id}
        // This endpoint updates an existing task list within a specific project.
        [HttpPut("projects/{projectId}/tasklists/{id}")]
        [Authorize(Policy = "CanTaskListUpdate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateTaskList(int projectId, int id, TaskListInputDto dto)
        {
            try
            {
                var companyId = GetCompanyIdFromClaims();
                if (!companyId.HasValue)
                {
                    return Error("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                var currentUserId = GetCurrentUserIdFromClaims();
                if (!currentUserId.HasValue)
                {
                    return Error("User ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                var taskList = await GetTaskListForCompanyAndProject(id, projectId, companyId.Value);
                if (taskList == null || taskList.IsDeleted)
                {
                    return Error("Task List not found or not accessible within this project.", StatusCodes.Status404NotFound);
                }

                if (projectId != taskList.ProjectId)
                {
                    return Error("Cannot change TaskList's ProjectId via this endpoint.", StatusCodes.Status400BadRequest);
                }

                taskList.ListName = dto.ListName;
                taskList.Description = dto.Description;
                taskList.ListOrder = dto.ListOrder;
                taskList.StartDate = dto.StartDate;
                taskList.EndDate = dto.EndDate;
                taskList.UpdatedAt = DateTimeOffset.UtcNow;
                taskList.UpdatedBy = currentUserId.Value;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating task list ID {id} for project ID {projectId}.");
                return Error("An error occurred while updating the task list.", StatusCodes.Status500InternalServerError);
            }
        }

        // DELETE: api/Tasks/projects/{projectId}/tasklists/{id}
        [HttpDelete("projects/{projectId}/tasklists/{id}")]
        [Authorize(Policy = "CanTaskListDelete")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteTaskList(int projectId, int id)
        {
            try
            {
                var companyId = GetCompanyIdFromClaims();
                if (!companyId.HasValue)
                {
                    return Error("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                var currentUserId = GetCurrentUserIdFromClaims();
                if (!currentUserId.HasValue)
                {
                    return Error("User ID not found in claims.", StatusCodes.Status401Unauthorized);
                }

                var taskList = await GetTaskListForCompanyAndProject(id, projectId, companyId.Value);
                if (taskList == null || taskList.IsDeleted)
                {
                    return Error("Task List not found or already deleted within this project.", StatusCodes.Status404NotFound);
                }

                // Soft delete all tasks and subtasks within this task list
                var tasksToDelete = await _context.Tasks
                                                 .Where(t => t.TaskListId == id && t.CompanyId == companyId.Value && !t.IsDeleted)
                                                 .ToListAsync();
                foreach (var task in tasksToDelete)
                {
                    task.IsDeleted = true;
                    task.UpdatedAt = DateTimeOffset.UtcNow;
                    task.UpdatedBy = currentUserId.Value;

                    // Also soft delete documents related to these tasks
                    var documentsToDelete = await _context.TaskDocuments
                                                          .Where(td => td.TaskId == task.TaskId && td.CompanyId == companyId.Value && !td.IsDeleted)
                                                          .ToListAsync();
                    foreach (var document in documentsToDelete)
                    {
                        document.IsDeleted = true;
                        document.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                }


                taskList.IsDeleted = true;
                taskList.UpdatedAt = DateTimeOffset.UtcNow;
                taskList.UpdatedBy = currentUserId.Value;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting task list ID {id} for project ID {projectId}.");
                return Error("An error occurred while deleting the task list.", StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("{taskId}/documents")]
        [Consumes("multipart/form-data")] // Essential for file uploads
        [Authorize(Policy = "CanTaskDocumentCreate")]
        [ProducesResponseType(typeof(ApiResponse<TaskDocumentUploadResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<TaskDocumentUploadResponseDto>>> UploadTaskDocument(
            int taskId,
            [FromForm] TaskDocumentUploadDto dto) // Using the new TaskDocumentUploadDto
        {
            _logger.LogInformation($"Attempting to upload document for Task ID: {taskId}. Document Name: {dto.DocumentName}");

            var currentUserId = GetCurrentUserIdFromClaims();
            var companyId = GetCompanyIdFromClaims();

            if (!currentUserId.HasValue || !companyId.HasValue)
            {
                return Error<TaskDocumentUploadResponseDto>("Authentication information (UserId, CompanyId) is missing.", StatusCodes.Status401Unauthorized);
            }

            var task = await _context.Tasks
                                     .AsNoTracking() // Use AsNoTracking as we only need to read its properties
                                     .FirstOrDefaultAsync(t => t.TaskId == taskId && t.CompanyId == companyId.Value && !t.IsDeleted);

            if (task == null)
            {
                return Error<TaskDocumentUploadResponseDto>($"Task with ID {taskId} not found or not accessible within your company.", StatusCodes.Status404NotFound);
            }

            var canManageCompanyTaskDocuments = HasPermission(Permissions.TaskRead);
            var canManageAssignedTaskDocuments = HasPermission(Permissions.TaskReadAssigned) &&
                                                 task.AssignedTo == currentUserId.Value;

            if (!canManageCompanyTaskDocuments && !canManageAssignedTaskDocuments)
            {
                return Error<TaskDocumentUploadResponseDto>("You do not have permission to upload documents for this task.", StatusCodes.Status403Forbidden);
            }
            var existingDocumentCount = await _context.TaskDocuments.CountAsync(td => td.TaskId == taskId && !td.IsDeleted);
            if (existingDocumentCount >= 5)
            {
                _logger.LogWarning($"Attempt to upload document to Task ID: {taskId} failed. Document limit of 5 reached.");
                return Error<TaskDocumentUploadResponseDto>($"Cannot upload more documents. This task has reached its limit of 5 documents.", StatusCodes.Status400BadRequest);
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for task document upload: {Errors}", ModelState);
                return BadRequest(new ApiResponse<object>("Invalid document data provided.", "Error", StatusCodes.Status400BadRequest, ModelState));
            }

            if (dto.File == null || dto.File.Length == 0)
            {
                return Error<TaskDocumentUploadResponseDto>("No file uploaded or file is empty.", StatusCodes.Status400BadRequest);
            }

            try
            {
                var uploadedDocument = await _uploadHandler.UploadTaskDocumentAsync(
                    currentUserId.Value,
                    companyId.Value,
                    taskId,
                    dto.File,
                    dto.DocumentName,
                    dto.Description,
                    dto.Version
                );

                _logger.LogInformation($"Document '{uploadedDocument.DocumentName}' (ID: {uploadedDocument.DocumentId}) uploaded successfully for Task ID: {taskId}.");

                // Return the lightweight DTO within the ApiResponse
                var responseDto = new TaskDocumentUploadResponseDto
                {
                    DocumentId = uploadedDocument.DocumentId,
                    TaskId = uploadedDocument.TaskId,
                    DocumentName = uploadedDocument.DocumentName,
                    FilePath = uploadedDocument.FilePath,
                    DocumentType = uploadedDocument.DocumentType,
                    FileSize = uploadedDocument.FileSize,
                    Description = uploadedDocument.Description,
                    Version = uploadedDocument.Version,
                    CreatedAt = uploadedDocument.CreatedAt // Assuming CreatedAt is desired in response
                };

                return Success("Task document uploaded successfully.", responseDto, StatusCodes.Status201Created);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, $"Task document upload failed due to invalid operation for Task ID {taskId}.");
                return Error<TaskDocumentUploadResponseDto>(ex.Message, StatusCodes.Status400BadRequest);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, $"Task document upload failed due to bad arguments for Task ID {taskId}.");
                return Error<TaskDocumentUploadResponseDto>(ex.Message, StatusCodes.Status400BadRequest);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, $"Task document upload failed due to I/O error for Task ID {taskId}.");
                return Error<TaskDocumentUploadResponseDto>($"Failed to save document due to a file system error: {ex.Message}", StatusCodes.Status500InternalServerError);
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                _logger.LogError(dbEx, "Database error during task document upload for Task ID {TaskId}. Inner Exception: {InnerMessage}", taskId, innerException?.Message);
                return Error<TaskDocumentUploadResponseDto>("A database error occurred while saving document information.", StatusCodes.Status500InternalServerError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while uploading document for Task ID: {taskId}");
                return Error<TaskDocumentUploadResponseDto>("An unexpected error occurred while uploading the task document.", StatusCodes.Status500InternalServerError);
            }
        }

        // DELETE: api/Tasks/{taskId}/documents/{documentId}
        [HttpDelete("{taskId}/documents/{documentId}")]
        [Authorize(Policy = "CanTaskDocumentDelete")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteTaskDocument(int taskId, int documentId)
        {
            _logger.LogInformation($"Attempting to soft-delete document ID: {documentId} for Task ID: {taskId}.");

            var currentUserId = GetCurrentUserIdFromClaims();
            var companyId = GetCompanyIdFromClaims();

            if (!currentUserId.HasValue || !companyId.HasValue)
            {
                return Error("Authentication information (UserId, CompanyId) is missing.", StatusCodes.Status401Unauthorized);
            }

            try
            {
                // Retrieve the document, ensuring it belongs to the specified task and company
                var document = await _context.TaskDocuments
                    .Include(td => td.Task) // Include Task to check ProjectId and CompanyId
                    .FirstOrDefaultAsync(td => td.DocumentId == documentId &&
                                               td.TaskId == taskId &&
                                               td.CompanyId == companyId.Value &&
                                               !td.IsDeleted);

                if (document == null)
                {
                    _logger.LogWarning($"Task document ID {documentId} not found for task ID {taskId}, or it's already deleted.");
                    return Error($"Task document with ID {documentId} not found for task {taskId} or it's already deleted.", StatusCodes.Status404NotFound);
                }

                var canManageCompanyTaskDocuments = HasPermission(Permissions.TaskRead);
                var canManageAssignedTaskDocuments = HasPermission(Permissions.TaskReadAssigned) &&
                                                     (document.Task?.AssignedTo == currentUserId.Value ||
                                                      document.UploadedBy == currentUserId.Value);

                if (!canManageCompanyTaskDocuments && !canManageAssignedTaskDocuments)
                {
                    _logger.LogWarning($"User ID: {currentUserId} attempted to delete task document ID: {documentId} for Task ID: {taskId}. Access denied.");
                    return Error("You do not have permission to delete this task document.", StatusCodes.Status403Forbidden);
                }

                // Perform soft delete
                document.IsDeleted = true;
                document.UpdatedAt = DateTimeOffset.UtcNow;
                //document.UpdatedBy = currentUserId; // Record who soft-deleted it

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Task document ID: {documentId} for Task ID: {taskId} soft-deleted successfully by User ID: {currentUserId}.");
                return NoContent(); // 204 No Content for successful deletion with no body
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                _logger.LogError(dbEx, "Database error during soft-deletion of task document ID: {DocumentId} for Task ID: {TaskId}. Inner Exception: {InnerMessage}", documentId, taskId, innerException?.Message);
                return Error("A database error occurred while soft deleting the task document.", StatusCodes.Status500InternalServerError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while deleting task document ID: {documentId} for Task ID: {taskId}.");
                return Error("An unexpected error occurred while deleting the task document.", StatusCodes.Status500InternalServerError);
            }
        }
    }
}
