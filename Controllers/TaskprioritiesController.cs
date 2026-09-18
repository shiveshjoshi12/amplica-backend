using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Data;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs; // Ensure this is correct for your DTOs
// Removed using AutoMapper;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BizfreeApp.Dtos;
// Removed using BizfreeApp.Dtos; // This seems to be an unnecessary or old reference now

namespace BizfreeApp.Controllers;

[Route("api/[controller]")] // Base route: /api/taskpriorities
[ApiController]
[Authorize] // Apply authorization for administrative access
public class TaskprioritiesController : ControllerBase
{
    private readonly Data.ApplicationDbContext _context;
    // Removed private readonly IMapper _mapper;
    private readonly ILogger<TaskprioritiesController> _logger;

    public TaskprioritiesController(Data.ApplicationDbContext context, ILogger<TaskprioritiesController> logger) // Removed IMapper mapper
    {
        _context = context;
        _logger = logger;
    }

    // Helper method to generate a standardized success response
    private ActionResult<ApiResponse<T>> Success<T>(string message, T? data, int statusCode = StatusCodes.Status200OK)
    {
        return StatusCode(statusCode, new ApiResponse<T>(message, "success", statusCode, data));
    }

    // Helper method to generate a standardized error response for generic ActionResult
    private ActionResult<ApiResponse<T>> Error<T>(string message, int statusCode, string status = "error")
    {
        _logger.LogError("API Error: {Message} - Status Code: {StatusCode}", message, statusCode); // Added logging for errors
        return StatusCode(statusCode, new ApiResponse<T>(message, status, statusCode, default(T)));
    }

    // Overloaded helper for non-generic IActionResult if needed (though not directly used for TaskprioritiesController's main actions)
    private IActionResult Error(string message, int statusCode, string status = "error")
    {
        _logger.LogError("API Error: {Message} - Status Code: {StatusCode}", message, statusCode); // Added logging for errors
        return StatusCode(statusCode, new ApiResponse<object>(message, status, statusCode, null));
    }


    // GET: api/Taskpriorities
    [HttpGet]
    // [AllowAnonymous] // Uncomment if this endpoint should be publicly accessible
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TaskpriorityDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<IEnumerable<TaskpriorityDto>>>> GetAllTaskpriorities()
    {
        try
        {
            _logger.LogInformation("Attempting to retrieve all task priorities.");
            var priorities = await _context.Taskpriorities.ToListAsync();

            // Manual mapping from Taskpriority model to TaskpriorityDto
            var priorityDtos = priorities.Select(p => new TaskpriorityDto
            {
                PriorityId = p.PriorityId,
                Name = p.Name,
                PriorityColor = p.PriorityColor // Assuming Taskpriority model has a PriorityColor property
            }).ToList(); // Changed to ToList() for explicit type matching if needed by Success helper

            _logger.LogInformation($"Successfully retrieved {priorityDtos.Count()} task priorities.");
            // Explicitly cast to IEnumerable<TaskpriorityDto> to match method signature
            return Success("Task priorities retrieved successfully.", (IEnumerable<TaskpriorityDto>)priorityDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while retrieving task priorities.");
            return Error<IEnumerable<TaskpriorityDto>>("An error occurred while retrieving task priorities.", StatusCodes.Status500InternalServerError);
        }
    }

    // GET: api/Taskpriorities/5
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<TaskpriorityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<TaskpriorityDto>>> GetTaskpriority(int id)
    {
        try
        {
            _logger.LogInformation($"Attempting to retrieve task priority with ID: {id}");
            var taskpriority = await _context.Taskpriorities.FindAsync(id);

            if (taskpriority == null)
            {
                _logger.LogWarning($"Task priority with ID {id} not found.");
                return Error<TaskpriorityDto>($"Task priority with ID {id} not found.", StatusCodes.Status404NotFound, "not_found");
            }

            _logger.LogInformation($"Successfully retrieved task priority with ID: {id}");
            // Manual mapping from Taskpriority model to TaskpriorityDto
            var taskpriorityDto = new TaskpriorityDto
            {
                PriorityId = taskpriority.PriorityId,
                Name = taskpriority.Name,
                PriorityColor = taskpriority.PriorityColor // Assuming Taskpriority model has a PriorityColor property
            };
            return Success("Task priority retrieved successfully.", taskpriorityDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error occurred while retrieving task priority with ID: {id}");
            return Error<TaskpriorityDto>($"An error occurred while retrieving task priority with ID {id}.", StatusCodes.Status500InternalServerError);
        }
    }

    // POST: api/Taskpriorities
    [HttpPost]
    // [Authorize(Roles = "Admin")] // Example: uncomment if you need specific role authorization
    [ProducesResponseType(typeof(ApiResponse<TaskpriorityDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<TaskpriorityDto>>> CreateTaskpriority([FromBody] CreateTaskpriorityDto createTaskpriorityDto)
    {
        try
        {
            _logger.LogInformation($"Attempting to create a new task priority: {createTaskpriorityDto.Name}");

            // Check for duplicate name to maintain data integrity
            if (await _context.Taskpriorities.AnyAsync(tp => tp.Name == createTaskpriorityDto.Name))
            {
                _logger.LogWarning($"Attempted to create duplicate task priority name: {createTaskpriorityDto.Name}");
                return Error<TaskpriorityDto>($"A task priority with the name '{createTaskpriorityDto.Name}' already exists.", StatusCodes.Status409Conflict, "conflict");
            }

            // Manual mapping from CreateTaskpriorityDto to Taskpriority model
            var taskpriority = new Taskpriority
            {
                Name = createTaskpriorityDto.Name,
                PriorityColor = createTaskpriorityDto.PriorityColor // Assuming CreateTaskpriorityDto has PriorityColor
            };

            // If you had audit fields like CreatedAt/CreatedBy in Taskpriority, you'd set them here.
            // For example: taskpriority.CreatedAt = DateTime.UtcNow;

            _context.Taskpriorities.Add(taskpriority);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Task priority '{taskpriority.Name}' created successfully with ID: {taskpriority.PriorityId}");
            // Manual mapping back to TaskpriorityDto for the response
            var responseDto = new TaskpriorityDto
            {
                PriorityId = taskpriority.PriorityId,
                Name = taskpriority.Name,
                PriorityColor = taskpriority.PriorityColor // Assuming Taskpriority model has a PriorityColor property
            };
            return Success("Task priority created successfully.", responseDto, StatusCodes.Status201Created);
        }
        catch (DbUpdateException ex)
        {
            var innerException = ex.InnerException as Npgsql.PostgresException; // Assuming PostgreSQL. Adjust if using SQL Server (SqlException)
            if (innerException != null && innerException.SqlState == "23505") // Unique violation
            {
                _logger.LogError(ex, $"Database conflict while creating task priority '{createTaskpriorityDto.Name}'.");
                return Error<TaskpriorityDto>($"A priority with this name already exists or ID conflicts: {innerException.Message}", StatusCodes.Status409Conflict, "conflict");
            }
            _logger.LogError(ex, $"A database error occurred while creating task priority '{createTaskpriorityDto.Name}'.");
            return Error<TaskpriorityDto>("A database error occurred while creating the task priority.", StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An unexpected error occurred while creating task priority '{createTaskpriorityDto.Name}'.");
            return Error<TaskpriorityDto>("An unexpected error occurred while creating the task priority.", StatusCodes.Status500InternalServerError);
        }
    }

    // PUT: api/Taskpriorities/5
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<TaskpriorityDto>), StatusCodes.Status200OK)] // Or 204 NoContent, depending on preference
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<TaskpriorityDto>>> UpdateTaskpriority(int id, [FromBody] UpdateTaskpriorityDto updateTaskpriorityDto)
    {
        try
        {
            _logger.LogInformation($"Attempting to update task priority with ID: {id}");

            if (id != updateTaskpriorityDto.PriorityId)
            {
                _logger.LogWarning($"Mismatch between URL ID ({id}) and body ID ({updateTaskpriorityDto.PriorityId}) for task priority update.");
                return Error<TaskpriorityDto>("Priority ID in URL does not match ID in body.", StatusCodes.Status400BadRequest, "bad_request");
            }

            var taskpriority = await _context.Taskpriorities.FindAsync(id);
            if (taskpriority == null)
            {
                _logger.LogWarning($"Task priority with ID {id} not found for update.");
                return Error<TaskpriorityDto>($"Task priority with ID {id} not found.", StatusCodes.Status404NotFound, "not_found");
            }

            // Check for duplicate name if the name is being changed
            if (taskpriority.Name != updateTaskpriorityDto.Name && await _context.Taskpriorities.AnyAsync(tp => tp.Name == updateTaskpriorityDto.Name))
            {
                _logger.LogWarning($"Attempted to update task priority ID {id} to duplicate name: {updateTaskpriorityDto.Name}");
                return Error<TaskpriorityDto>($"A task priority with the name '{updateTaskpriorityDto.Name}' already exists.", StatusCodes.Status409Conflict, "conflict");
            }

            // Manual mapping from UpdateTaskpriorityDto to Taskpriority model
            taskpriority.Name = updateTaskpriorityDto.Name;
            taskpriority.PriorityColor = updateTaskpriorityDto.PriorityColor; // Assuming UpdateTaskpriorityDto has PriorityColor

            // If you had audit fields like UpdatedAt/UpdatedBy in Taskpriority, you'd set them here.
            // For example: taskpriority.UpdatedAt = DateTime.UtcNow;

            _context.Entry(taskpriority).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Task priority with ID: {id} updated successfully.");
            // Manual mapping back to TaskpriorityDto for the response
            var responseDto = new TaskpriorityDto
            {
                PriorityId = taskpriority.PriorityId,
                Name = taskpriority.Name,
                PriorityColor = taskpriority.PriorityColor // Assuming Taskpriority model has a PriorityColor property
            };
            return Success("Task priority updated successfully.", responseDto);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (!TaskpriorityExists(id))
            {
                _logger.LogError(ex, $"Task priority with ID {id} not found during concurrency check.");
                return Error<TaskpriorityDto>($"Task priority with ID {id} not found during concurrency check.", StatusCodes.Status404NotFound, "not_found");
            }
            else
            {
                _logger.LogError(ex, $"Concurrency conflict while updating task priority with ID: {id}.");
                return Error<TaskpriorityDto>("Concurrency error: The task priority was modified by another user.", StatusCodes.Status409Conflict, "concurrency_conflict");
            }
        }
        catch (DbUpdateException ex)
        {
            var innerException = ex.InnerException as Npgsql.PostgresException; // Assuming PostgreSQL. Adjust if using SQL Server (SqlException)
            if (innerException != null && innerException.SqlState == "23505") // Unique violation
            {
                _logger.LogError(ex, $"Database conflict while updating task priority with ID: {id}.");
                return Error<TaskpriorityDto>($"A priority with this name already exists or ID conflicts: {innerException.Message}", StatusCodes.Status409Conflict, "conflict");
            }
            _logger.LogError(ex, $"A database error occurred while updating task priority with ID: {id}.");
            return Error<TaskpriorityDto>("A database error occurred while updating the task priority.", StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An unexpected error occurred while updating task priority with ID: {id}.");
            return Error<TaskpriorityDto>("An unexpected error occurred while updating the task priority.", StatusCodes.Status500InternalServerError);
        }
    }

    // DELETE: api/Taskpriorities/5
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTaskpriority(int id)
    {
        try
        {
            _logger.LogInformation($"Attempting to delete task priority with ID: {id}");
            var taskpriority = await _context.Taskpriorities.FindAsync(id);
            if (taskpriority == null)
            {
                _logger.LogWarning($"Task priority with ID {id} not found for deletion.");
                return Error<object>($"Task priority with ID {id} not found.", StatusCodes.Status404NotFound, "not_found");
            }

            // This check implicitly uses the PriorityId from the existing taskpriority object
            var tasksUsingPriority = await _context.Tasks.AnyAsync(t => t.PriorityId == id);
            if (tasksUsingPriority)
            {
                _logger.LogWarning($"Attempted to delete task priority ID {id} which is in use by tasks.");
                return Error<object>("Cannot delete priority as it is currently used by one or more tasks. Consider deactivating it instead.", StatusCodes.Status400BadRequest, "bad_request");
            }

            _context.Taskpriorities.Remove(taskpriority);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Task priority with ID: {id} deleted successfully.");
            return Success<object>("Task priority deleted successfully.", null, StatusCodes.Status200OK);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, $"A database error occurred while deleting task priority with ID: {id}.");
            return Error<object>("A database error occurred while deleting the task priority.", StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An unexpected error occurred while deleting task priority with ID: {id}.");
            return Error<object>("An unexpected error occurred while deleting the task priority.", StatusCodes.Status500InternalServerError);
        }
    }

    private bool TaskpriorityExists(int id)
    {
        return _context.Taskpriorities.Any(e => e.PriorityId == id);
    }
}