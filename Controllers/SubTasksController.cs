using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; // Add this for [Authorize] attribute
using System.Security.Claims;
using System;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;
using BizfreeApp.Constants; // Add this to access your Permissions constants

namespace BizfreeApp.Controllers
{
    [Authorize] // All endpoints in this controller require authentication
    [Route("api/tasks/{parentTaskId}/subtasks")] // Base route for subtasks, nested under a parent task
    [ApiController]
    public class SubtasksController : ControllerBase
    {
        private readonly Data.ApplicationDbContext _context;

        public SubtasksController(Data.ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper Method to get the current Company's ID from claims
        private int? GetCompanyIdFromClaims()
        {
            var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
            if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out int companyId))
            {
                return companyId;
            }
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
            return StatusCode(statusCode, new ApiResponse<object>(message, status, statusCode, null));
        }

        // Helper method to get a Task (which could be a parent task) specific to the company by its TaskId
        private async Task<Models.Task?> GetTaskForCompany(int taskId, int companyId)
        {
            return await _context.Tasks
                                 .Where(t => t.TaskId == taskId && t.CompanyId == companyId && !t.IsDeleted)
                                 .FirstOrDefaultAsync();
        }

        // Helper method to get a SubTask by its ID, ensuring it belongs to the specified parent task and company
        private async Task<Models.Task?> GetSubTaskForParentAndCompany(int subtaskId, int parentTaskId, int companyId)
        {
            return await _context.Tasks
                                 .Include(t => t.TaskList)
                                 .Include(t => t.StatusNavigation)
                                 .Include(t => t.Priority)
                                 .Include(t => t.AssignedToNavigation)
                                 .Include(t => t.Company)
                                 .Where(t => t.TaskId == subtaskId &&
                                             t.ParentTaskId == parentTaskId &&
                                             t.CompanyId == companyId &&
                                             !t.IsDeleted)
                                 .FirstOrDefaultAsync();
        }

        // GET: api/tasks/{parentTaskId}/subtasks
        // This endpoint retrieves all subtasks for a given parent task, with optional filtering, paging, and sorting.
        [HttpGet]
        [Authorize(Policy = "CanTaskReadAll")] // Requires 'task:read:all' permission
        [ProducesResponseType(typeof(ApiResponse<PagedResult<TaskDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)] // Added for clarity
        public async Task<ActionResult<ApiResponse<PagedResult<TaskDto>>>> GetSubtasks(
            int parentTaskId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 5,
            [FromQuery] string sortBy = "TaskOrder",
            [FromQuery] string sortOrder = "asc",
            [FromQuery] string? search = null,
            [FromQuery] int? statusId = null,
            [FromQuery] int? priorityId = null,
            [FromQuery] int? assignedToUserId = null,
            [FromQuery] DateOnly? dueDateFrom = null,
            [FromQuery] DateOnly? dueDateTo = null)
        {
            var companyId = GetCompanyIdFromClaims();
            if (!companyId.HasValue)
            {
                return Error<PagedResult<TaskDto>>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            // Verify that the parent task exists and belongs to the current company
            var parentTask = await GetTaskForCompany(parentTaskId, companyId.Value);
            if (parentTask == null)
            {
                return Error<PagedResult<TaskDto>>("Parent task not found or not accessible.", StatusCodes.Status404NotFound);
            }

            IQueryable<Models.Task> query = _context.Tasks
                .Include(t => t.StatusNavigation)
                .Include(t => t.Priority)
                .Include(t => t.TaskList)
                .Include(t => t.AssignedToNavigation)
                .Include(t => t.Company)
                .Where(t => t.ParentTaskId == parentTaskId && // Key condition for subtasks
                             t.CompanyId == companyId.Value &&
                             !t.IsDeleted);

            // Apply filters
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t => t.Title!.Contains(search) || (t.Description != null && t.Description.Contains(search)));
            }
            if (statusId.HasValue)
            {
                query = query.Where(t => t.Status == statusId.Value);
            }
            if (priorityId.HasValue)
            {
                query = query.Where(t => t.PriorityId == priorityId.Value);
            }
            if (assignedToUserId.HasValue)
            {
                query = query.Where(t => t.AssignedTo == assignedToUserId.Value);
            }
            if (dueDateFrom.HasValue)
            {
                query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value >= dueDateFrom.Value);
            }
            if (dueDateTo.HasValue)
            {
                query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value <= dueDateTo.Value);
            }

            var totalSubtasks = await query.CountAsync();

            // Sorting logic (can reuse from main TasksController if common)
            var taskPropertyMap = new Dictionary<string, Expression<Func<Models.Task, object>>>
            {
                { "taskid", t => t.TaskId },
                { "title", t => t.Title! },
                { "statusname", t => t.StatusNavigation!.Name! },
                { "duedate", t => t.DueDate! },
                { "priorityname", t => t.Priority!.Name! },
                { "assignedtouserid", t => t.AssignedTo! },
                { "listname", t => t.TaskList!.ListName! },
                { "taskorder", t => t.TaskOrder! },
                { "companyname", t => t.Company!.CompanyName! },
                { "createdat", t => t.CreatedAt },
                { "updatedat", t => t.UpdatedAt }
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
                query = query.OrderBy(t => t.TaskOrder).ThenBy(t => t.TaskId); // Default sort for subtasks
            }

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TaskDto
                {
                    TaskId = t.TaskId,
                    Title = t.Title,
                    StatusName = t.StatusNavigation != null ? t.StatusNavigation.Name : null,
                    EndDate = t.EndDate,
                    PriorityName = t.Priority != null ? t.Priority.Name : null,
                    Description = t.Description,
                    AssignedToUserId = t.AssignedTo,
                    TaskListId = t.TaskList != null ? t.TaskList.TaskListId : null,
                    ListName = t.TaskList != null ? t.TaskList.ListName : null,
                    TaskListDescription = t.TaskList != null ? t.TaskList.Description : null,
                    ListOrder = t.TaskList != null ? t.TaskList.ListOrder : null,
                    ProjectId = t.ProjectId,
                    CompanyName = t.Company != null ? t.Company.CompanyName : null,
                    ParentTaskId = t.ParentTaskId
                })
                .ToListAsync();

            var pagedResult = new PagedResult<TaskDto>
            {
                Items = items,
                TotalCount = totalSubtasks,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalSubtasks / pageSize),
                TotalTasks = totalSubtasks
            };

            return Success("Subtasks retrieved successfully.", pagedResult);
        }

        // GET: api/tasks/{parentTaskId}/subtasks/{subtaskId}
        // This endpoint retrieves a specific subtask by its ID under a given parent task.
        [HttpGet("{subtaskId}")]
        [Authorize(Policy = "CanTaskReadAll")] // Requires 'task:read:all' permission
        [ProducesResponseType(typeof(ApiResponse<TaskDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)] // Added for clarity
        public async Task<ActionResult<ApiResponse<TaskDto>>> GetSubtaskById(int parentTaskId, int subtaskId)
        {
            var companyId = GetCompanyIdFromClaims();
            if (!companyId.HasValue)
            {
                return Error<TaskDto>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            // Verify parent task exists and is accessible
            var parentTask = await GetTaskForCompany(parentTaskId, companyId.Value);
            if (parentTask == null)
            {
                return Error<TaskDto>("Parent task not found or not accessible.", StatusCodes.Status404NotFound);
            }

            // Retrieve the specific subtask
            var subtask = await GetSubTaskForParentAndCompany(subtaskId, parentTaskId, companyId.Value);

            if (subtask == null || subtask.IsDeleted)
            {
                return Error<TaskDto>("Subtask not found or not accessible under the specified parent task.", StatusCodes.Status404NotFound);
            }

            var subtaskDto = new TaskDto
            {
                TaskId = subtask.TaskId,
                Title = subtask.Title,
                StatusName = subtask.StatusNavigation?.Name,
                EndDate = subtask.EndDate,
                PriorityName = subtask.Priority?.Name,
                Description = subtask.Description,
                AssignedToUserId = subtask.AssignedTo,
                TaskListId = subtask.TaskList?.TaskListId,
                ListName = subtask.TaskList?.ListName,
                TaskListDescription = subtask.TaskList?.Description,
                ListOrder = subtask.TaskList?.ListOrder,
                ProjectId = subtask.ProjectId,
                CompanyName = subtask.Company?.CompanyName,
                ParentTaskId = subtask.ParentTaskId
            };

            return Success("Subtask retrieved successfully.", subtaskDto);
        }


        // POST: api/tasks/{parentTaskId}/subtasks
        // This endpoint allows creating a new subtask for a specific parent task.
        [HttpPost]
        [Authorize(Policy = "CanTaskCreate")] // Requires 'task:create' permission
        [ProducesResponseType(typeof(ApiResponse<TaskDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)] // Added for clarity
        public async Task<ActionResult<ApiResponse<TaskDto>>> CreateSubtask(int parentTaskId, TaskInputDto dto)
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

            // 1. Verify parent task exists and is accessible
            var parentTask = await GetTaskForCompany(parentTaskId, companyId.Value);
            if (parentTask == null)
            {
                return Error<TaskDto>("Parent task not found or not accessible.", StatusCodes.Status404NotFound);
            }

            // 2. Ensure the subtask is associated with the parent's TaskList and Project
            if (dto.TaskListId.HasValue && dto.TaskListId.Value != parentTask.TaskListId)
            {
                return Error<TaskDto>("Subtask's TaskListId must match its parent task's TaskListId.", StatusCodes.Status400BadRequest);
            }
            if (dto.ParentTaskId.HasValue && dto.ParentTaskId.Value != parentTaskId)
            {
                return Error<TaskDto>("ParentTaskId in body must match parentTaskId in URL path for subtask creation.", StatusCodes.Status400BadRequest);
            }


            // 3. Validate assigned user if provided
            int? assignedTo = dto.AssignedToUserId;
            if (!assignedTo.HasValue)
            {
                assignedTo = currentUserId.Value;
            }

            if (assignedTo.HasValue)
            {
                var assignedUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == assignedTo.Value && u.CompanyId == companyId.Value);
                if (assignedUser == null)
                {
                    return Error<TaskDto>("Assigned user not found or does not belong to your company.", StatusCodes.Status400BadRequest);
                }
            }

            // Create the new subtask
            var subtask = new Models.Task
            {
                Title = dto.Title,
                Status = dto.StatusId,
                EndDate = dto.EndDate,
                PriorityId = dto.PriorityId,
                Description = dto.Description,
                EstimatedHours = dto.EstimatedHours,
                ActualHours = dto.ActualHours,
                TaskOrder = dto.TaskOrder,
                ParentTaskId = parentTaskId,
                TaskListId = parentTask.TaskListId,
                ProjectId = parentTask.ProjectId,
                CompanyId = companyId.Value,
                AssignedTo = assignedTo,
                CreatedBy = currentUserId.Value,
                CreatedAt = DateTimeOffset.UtcNow,
                IsActive = true,
                IsDeleted = false
            };

            _context.Tasks.Add(subtask);
            await _context.SaveChangesAsync();

            // Load navigation properties for the DTO conversion
            await _context.Entry(subtask).Reference(t => t.StatusNavigation).LoadAsync();
            await _context.Entry(subtask).Reference(t => t.Priority).LoadAsync();
            await _context.Entry(subtask).Reference(t => t.TaskList).LoadAsync();
            await _context.Entry(subtask).Reference(t => t.AssignedToNavigation).LoadAsync();
            await _context.Entry(subtask).Reference(t => t.Company).LoadAsync();

            var subtaskDto = new TaskDto
            {
                TaskId = subtask.TaskId,
                Title = subtask.Title,
                StatusName = subtask.StatusNavigation?.Name,
                EndDate = subtask.EndDate,
                PriorityName = subtask.Priority?.Name,
                Description = subtask.Description,
                AssignedToUserId = subtask.AssignedTo,
                TaskListId = subtask.TaskList?.TaskListId,
                ListName = subtask.TaskList?.ListName,
                TaskListDescription = subtask.TaskList?.Description,
                ListOrder = subtask.TaskList?.ListOrder,
                ProjectId = subtask.ProjectId,
                CompanyName = subtask.Company?.CompanyName,
                ParentTaskId = subtask.ParentTaskId
            };

            return Success("Subtask created successfully.", subtaskDto, StatusCodes.Status201Created);
        }

        // PUT: api/tasks/{parentTaskId}/subtasks/{subtaskId}
        // This endpoint updates an existing subtask under a specific parent task.
        [HttpPut("{subtaskId}")]
        [Authorize(Policy = "CanTaskUpdate")] // Requires 'task:update' permission
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)] // Added for clarity
        public async Task<IActionResult> UpdateSubtask(int parentTaskId, int subtaskId, TaskInputDto dto)
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

            // 1. Verify parent task exists and is accessible
            var parentTask = await GetTaskForCompany(parentTaskId, companyId.Value);
            if (parentTask == null)
            {
                return Error("Parent task not found or not accessible.", StatusCodes.Status404NotFound);
            }

            // 2. Retrieve the subtask, ensuring it belongs to this parent and company
            var subtask = await GetSubTaskForParentAndCompany(subtaskId, parentTaskId, companyId.Value);

            if (subtask == null || subtask.IsDeleted)
            {
                return Error("Subtask not found or not accessible under the specified parent task.", StatusCodes.Status404NotFound);
            }

            // 3. Prevent changing ParentTaskId via this endpoint
            if (dto.ParentTaskId.HasValue && dto.ParentTaskId.Value != parentTaskId)
            {
                return Error("ParentTaskId cannot be changed via this subtask update endpoint.", StatusCodes.Status400BadRequest);
            }

            // 4. If TaskListId is provided in DTO, it must match the current TaskListId
            if (dto.TaskListId.HasValue && dto.TaskListId.Value != subtask.TaskListId)
            {
                return Error("Subtask's TaskListId cannot be changed via this endpoint to a different TaskList.", StatusCodes.Status400BadRequest);
            }

            // 5. Validate assigned user if provided
            if (dto.AssignedToUserId.HasValue)
            {
                var assignedUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == dto.AssignedToUserId.Value && u.CompanyId == companyId.Value);
                if (assignedUser == null)
                {
                    return Error("The assigned user does not exist or does not belong to your company.", StatusCodes.Status400BadRequest);
                }
                subtask.AssignedTo = dto.AssignedToUserId.Value;
            }
            else
            {
                subtask.AssignedTo = null;
            }

            // Update subtask properties
            subtask.Title = dto.Title;
            subtask.Status = dto.StatusId;
            subtask.EndDate = dto.EndDate;
            subtask.PriorityId = dto.PriorityId;
            subtask.Description = dto.Description;
            subtask.EstimatedHours = dto.EstimatedHours;
            subtask.ActualHours = dto.ActualHours;
            subtask.TaskOrder = dto.TaskOrder;
            subtask.UpdatedAt = DateTimeOffset.UtcNow;
            subtask.UpdatedBy = currentUserId.Value;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/tasks/{parentTaskId}/subtasks/{subtaskId}
        // This endpoint performs a soft delete on a specific subtask.
        [HttpDelete("{subtaskId}")]
        [Authorize(Policy = "CanTaskDelete")] // Requires 'task:delete' permission
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)] // Added for clarity
        public async Task<IActionResult> DeleteSubtask(int parentTaskId, int subtaskId)
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

            // 1. Verify parent task exists and is accessible
            var parentTask = await GetTaskForCompany(parentTaskId, companyId.Value);
            if (parentTask == null)
            {
                return Error("Parent task not found or not accessible.", StatusCodes.Status404NotFound);
            }

            // 2. Retrieve the subtask, ensuring it belongs to this parent and company
            var subtask = await GetSubTaskForParentAndCompany(subtaskId, parentTaskId, companyId.Value);

            if (subtask == null || subtask.IsDeleted)
            {
                return Error("Subtask not found or already deleted under the specified parent task.", StatusCodes.Status404NotFound);
            }

            subtask.IsDeleted = true;
            subtask.UpdatedAt = DateTimeOffset.UtcNow;
            subtask.UpdatedBy = currentUserId.Value;

            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}