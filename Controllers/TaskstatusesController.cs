using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Data;
using BizfreeApp.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using BizfreeApp.Models.DTOs; // Ensure this namespace is correct for your DTOs
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text.RegularExpressions; // Required for slug generation
// using BizfreeApp.Dtos; // This seems unused now, you can remove it if confirmed

namespace BizfreeApp.Controllers;

[Route("api/[controller]")] // Base route: /api/taskstatuses (remains the same)
[ApiController]
[Authorize] // Apply authorization for administrative access - Uncomment when ready
public class TaskstatusesController : ControllerBase
{
    private readonly Data.ApplicationDbContext _context;
    private readonly ILogger<TaskstatusesController> _logger;

    public TaskstatusesController(Data.ApplicationDbContext context, ILogger<TaskstatusesController> logger)
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
        _logger.LogError("API Error: {Message} - Status Code: {StatusCode}", message, statusCode);
        return StatusCode(statusCode, new ApiResponse<T>(message, status, statusCode, default(T)));
    }

    // Helper to get current user ID (needs to be implemented based on your auth setup)
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId"); // Or ClaimTypes.NameIdentifier
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedUserId))
        {
            return parsedUserId;
        }
        _logger.LogWarning("UserId claim not found or could not be parsed for audit fields.");
        return null; // Return null if user ID cannot be determined
    }

    // Helper to get Company ID from claims or query parameter if applicable
    private int? GetCompanyId(int? companyIdFromQuery = null)
    {
        if (companyIdFromQuery.HasValue && companyIdFromQuery.Value > 0)
        {
            return companyIdFromQuery.Value;
        }

        var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
        if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out int parsedCompanyId))
        {
            return parsedCompanyId;
        }
        _logger.LogWarning("CompanyId not provided in query or found in claims.");
        return null;
    }

    // Helper method to generate a base slug from a phrase
    private string GenerateSlug(string phrase)
    {
        string str = phrase.ToLowerInvariant();
        str = Regex.Replace(str, @"[^a-z0-9\s-]", ""); // Remove invalid chars
        str = Regex.Replace(str, @"\s+", "-").Trim(); // Replace spaces with hyphens
        str = Regex.Replace(str, @"-+", "-"); // Replace multiple hyphens with single
        return str;
    }

    // Helper method to generate a unique slug within a given company, optionally excluding a specific ID
    private async Task<string> GenerateUniqueSlug(string baseSlug, int companyId, int? excludeId = null)
    {
        string slug = baseSlug;
        int counter = 0;
        IQueryable<CompanyTaskStatus> query = _context.CompanyTaskStatuses.Where(cts => cts.CompanyId == companyId);

        // Exclude the current item when checking for uniqueness during an update
        if (excludeId.HasValue)
        {
            query = query.Where(cts => cts.Id != excludeId.Value);
        }

        while (await query.AnyAsync(cts => cts.Slug == slug))
        {
            counter++;
            slug = $"{baseSlug}-{counter}";
        }
        return slug;
    }

    // --- CompanyTaskStatus Specific Endpoints ---

    // GET: api/Taskstatuses?companyId={companyId}
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CompanyTaskstatusDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<List<CompanyTaskstatusDto>>>> GetAllCompanyTaskstatuses([FromQuery] int? companyId)
    {
        try
        {
            var resolvedCompanyId = GetCompanyId(companyId);
            if (!resolvedCompanyId.HasValue)
            {
                return Error<List<CompanyTaskstatusDto>>("Company ID is required.", StatusCodes.Status400BadRequest, "bad_request");
            }

            _logger.LogInformation($"Attempting to retrieve all task statuses for Company ID: {resolvedCompanyId.Value}");

            var statuses = await _context.CompanyTaskStatuses
                .Where(cts => cts.CompanyId == resolvedCompanyId.Value)
                .OrderBy(cts => cts.Order) // Order by the 'Order' field
                .ToListAsync();

            var statusDtos = statuses.Select(s => new CompanyTaskstatusDto
            {
                Id = s.Id,
                CompanyId = s.CompanyId,
                DefaultTaskStatusId = s.DefaultTaskStatusId,
                Name = s.Name,
                StatusColor = s.StatusColor,
                Slug = s.Slug,
                Order = s.Order,
                IsCustom = s.IsCustom,
                IsEditableName = s.IsEditableName,
                IsDeletable = s.IsDeletable
            }).ToList();

            _logger.LogInformation($"Successfully retrieved {statusDtos.Count} task statuses for Company ID: {resolvedCompanyId.Value}.");
            return Success("Company task statuses retrieved successfully.", statusDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while retrieving company task statuses.");
            return Error<List<CompanyTaskstatusDto>>("An error occurred while retrieving company task statuses.", StatusCodes.Status500InternalServerError);
        }
    }

    // GET: api/Taskstatuses/5?companyId={companyId}
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyTaskstatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<CompanyTaskstatusDto>>> GetCompanyTaskstatus(int id, [FromQuery] int? companyId)
    {
        try
        {
            var resolvedCompanyId = GetCompanyId(companyId);
            if (!resolvedCompanyId.HasValue)
            {
                return Error<CompanyTaskstatusDto>("Company ID is required.", StatusCodes.Status400BadRequest, "bad_request");
            }

            _logger.LogInformation($"Attempting to retrieve task status with ID: {id} for Company ID: {resolvedCompanyId.Value}");

            // Fetch the CompanyTaskStatus by ID and CompanyId to ensure it belongs to the correct company
            var companyTaskstatus = await _context.CompanyTaskStatuses
                .FirstOrDefaultAsync(cts => cts.Id == id && cts.CompanyId == resolvedCompanyId.Value);

            if (companyTaskstatus == null)
            {
                _logger.LogWarning($"Task status with ID {id} not found for Company ID {resolvedCompanyId.Value}.");
                return Error<CompanyTaskstatusDto>($"Task status with ID {id} not found for the specified company.", StatusCodes.Status404NotFound, "not_found");
            }

            _logger.LogInformation($"Successfully retrieved task status with ID: {id} for Company ID: {resolvedCompanyId.Value}.");
            var companyTaskstatusDto = new CompanyTaskstatusDto
            {
                Id = companyTaskstatus.Id,
                CompanyId = companyTaskstatus.CompanyId,
                DefaultTaskStatusId = companyTaskstatus.DefaultTaskStatusId,
                Name = companyTaskstatus.Name,
                StatusColor = companyTaskstatus.StatusColor,
                Slug = companyTaskstatus.Slug,
                Order = companyTaskstatus.Order,
                IsCustom = companyTaskstatus.IsCustom,
                IsEditableName = companyTaskstatus.IsEditableName,
                IsDeletable = companyTaskstatus.IsDeletable
            };
            return Success("Company task status retrieved successfully.", companyTaskstatusDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error occurred while retrieving task status with ID: {id} for Company ID: {companyId}.");
            return Error<CompanyTaskstatusDto>($"An error occurred while retrieving task status with ID {id}.", StatusCodes.Status500InternalServerError);
        }
    }

    // POST: api/Taskstatuses
    [HttpPost]
    [Authorize(Policy = "CanTaskStatusCreate")] // Added permission
    [ProducesResponseType(typeof(ApiResponse<CompanyTaskstatusDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<CompanyTaskstatusDto>>> CreateCompanyTaskstatus([FromBody] CreateCompanyTaskstatusDto createCompanyTaskstatusDto)
    {
        try
        {
            var resolvedCompanyId = GetCompanyId(createCompanyTaskstatusDto.CompanyId);
            if (!resolvedCompanyId.HasValue || resolvedCompanyId.Value <= 0) // Ensure company ID is valid
            {
                return Error<CompanyTaskstatusDto>("Company ID is required and must be valid.", StatusCodes.Status400BadRequest, "bad_request");
            }

            // Further validation for CompanyId matching authenticated user's context (if applicable)
            // if (resolvedCompanyId.Value != createCompanyTaskstatusDto.CompanyId)
            // {
            //     return Error<CompanyTaskstatusDto>("Company ID in body does not match authenticated user's company.", StatusCodes.Status403Forbidden, "forbidden");
            // }


            _logger.LogInformation($"Attempting to create a new company task status '{createCompanyTaskstatusDto.Name}' for Company ID: {resolvedCompanyId.Value}");

            // Check for duplicate name within the same company
            if (await _context.CompanyTaskStatuses.AnyAsync(cts => cts.CompanyId == resolvedCompanyId.Value && cts.Name == createCompanyTaskstatusDto.Name))
            {
                _logger.LogWarning($"Attempted to create duplicate task status name '{createCompanyTaskstatusDto.Name}' for Company ID: {resolvedCompanyId.Value}.");
                return Error<CompanyTaskstatusDto>($"A task status with the name '{createCompanyTaskstatusDto.Name}' already exists for this company.", StatusCodes.Status409Conflict, "conflict");
            }

            // Determine the next order for the new status within this company
            var maxOrder = (await _context.CompanyTaskStatuses
                                            .Where(cts => cts.CompanyId == resolvedCompanyId.Value)
                                            .Select(cts => cts.Order)
                                            .ToListAsync()) // Materialize to a list first
                                            .DefaultIfEmpty(0) // Then apply DefaultIfEmpty on the in-memory list
                                            .Max(); // Then find the Max on the in-memory list

            // --- SLUG GENERATION AND ASSIGNMENT (Corrected for Create) ---
            string generatedSlug;
            if (!string.IsNullOrWhiteSpace(createCompanyTaskstatusDto.Slug))
            {
                // If client provided a slug, use it as base and make it unique
                generatedSlug = await GenerateUniqueSlug(GenerateSlug(createCompanyTaskstatusDto.Slug), resolvedCompanyId.Value);
            }
            else
            {
                // If client did not provide a slug, generate one from the name and make it unique
                generatedSlug = await GenerateUniqueSlug(GenerateSlug(createCompanyTaskstatusDto.Name), resolvedCompanyId.Value);
            }
            // --- END SLUG GENERATION ---

            var companyTaskstatus = new CompanyTaskStatus
            {
                CompanyId = resolvedCompanyId.Value,
                Name = createCompanyTaskstatusDto.Name,
                StatusColor = createCompanyTaskstatusDto.StatusColor,
                Slug = generatedSlug, // Assign the determined unique slug
                Order = maxOrder + 1,
                IsCustom = true, // Newly created statuses are custom
                IsEditableName = true, // Custom statuses are editable by default
                IsDeletable = true, // Custom statuses are deletable by default
                CreatedAt = DateTime.UtcNow,
                CreatedBy = GetCurrentUserId(),
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = GetCurrentUserId(),
            };

            _context.CompanyTaskStatuses.Add(companyTaskstatus);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Company task status '{companyTaskstatus.Name}' created successfully with ID: {companyTaskstatus.Id} for Company ID: {companyTaskstatus.CompanyId}");

            var responseDto = new CompanyTaskstatusDto
            {
                Id = companyTaskstatus.Id,
                CompanyId = companyTaskstatus.CompanyId,
                DefaultTaskStatusId = companyTaskstatus.DefaultTaskStatusId,
                Name = companyTaskstatus.Name,
                StatusColor = companyTaskstatus.StatusColor,
                Slug = companyTaskstatus.Slug, // Ensure Slug is in the DTO for response
                Order = companyTaskstatus.Order,
                IsCustom = companyTaskstatus.IsCustom,
                IsEditableName = companyTaskstatus.IsEditableName,
                IsDeletable = companyTaskstatus.IsDeletable
            };
            return Success("Company task status created successfully.", responseDto, StatusCodes.Status201Created);
        }
        catch (DbUpdateException ex)
        {
            var innerException = ex.InnerException as Npgsql.PostgresException; // Assuming PostgreSQL
            if (innerException != null)
            {
                // Check for unique constraint violation
                if (innerException.SqlState == "23505")
                {
                    _logger.LogError(ex, $"Database conflict while creating company task status '{createCompanyTaskstatusDto.Name}'. Details: {innerException.Message}");
                    return Error<CompanyTaskstatusDto>($"A status with this name or slug already exists for the company. Details: {innerException.Detail ?? innerException.Message}", StatusCodes.Status409Conflict, "conflict");
                }
                // Check for not-null constraint violation
                if (innerException.SqlState == "23502")
                {
                    _logger.LogError(ex, $"Database Not Null Violation while creating company task status '{createCompanyTaskstatusDto.Name}'. Missing required field: {innerException.Message}");
                    return Error<CompanyTaskstatusDto>($"A required database field was missing. Details: {innerException.Detail ?? innerException.Message}", StatusCodes.Status400BadRequest, "bad_request");
                }
                // Check for foreign key constraint violation
                if (innerException.SqlState == "23503")
                {
                    _logger.LogError(ex, $"Database Foreign Key Violation while creating company task status '{createCompanyTaskstatusDto.Name}'. Invalid reference: {innerException.Message}");
                    return Error<CompanyTaskstatusDto>($"An invalid company ID or other reference was provided. Details: {innerException.Detail ?? innerException.Message}", StatusCodes.Status400BadRequest, "bad_request");
                }
            }
            _logger.LogError(ex, $"A general database error occurred while creating company task status '{createCompanyTaskstatusDto.Name}'.");
            return Error<CompanyTaskstatusDto>("A database error occurred while creating the company task status.", StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An unexpected error occurred while creating company task status '{createCompanyTaskstatusDto.Name}'.");
            return Error<CompanyTaskstatusDto>("An unexpected error occurred while creating the company task status.", StatusCodes.Status500InternalServerError);
        }
    }


    // PUT: api/Taskstatuses/5
    [HttpPut("{id}")]
    [Authorize(Policy = "CanTaskStatusUpdate")] // Added permission
    [ProducesResponseType(typeof(ApiResponse<CompanyTaskstatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<CompanyTaskstatusDto>>> UpdateCompanyTaskstatus(int id, [FromBody] UpdateCompanyTaskstatusDto updateCompanyTaskstatusDto)
    {
        try
        {
            var resolvedCompanyId = GetCompanyId(updateCompanyTaskstatusDto.CompanyId);
            if (!resolvedCompanyId.HasValue || resolvedCompanyId.Value != updateCompanyTaskstatusDto.CompanyId)
            {
                return Error<CompanyTaskstatusDto>("Company ID in body is invalid or does not match authenticated user's company.", StatusCodes.Status400BadRequest, "bad_request");
            }

            _logger.LogInformation($"Attempting to update company task status with ID: {id} for Company ID: {resolvedCompanyId.Value}");

            if (id != updateCompanyTaskstatusDto.Id)
            {
                _logger.LogWarning($"Mismatch between URL ID ({id}) and body ID ({updateCompanyTaskstatusDto.Id}) for company task status update.");
                return Error<CompanyTaskstatusDto>("Status ID in URL does not match ID in body.", StatusCodes.Status400BadRequest, "bad_request");
            }

            var companyTaskstatus = await _context.CompanyTaskStatuses
                                            .FirstOrDefaultAsync(cts => cts.Id == id && cts.CompanyId == resolvedCompanyId.Value);

            if (companyTaskstatus == null)
            {
                _logger.LogWarning($"Company task status with ID {id} not found for Company ID {resolvedCompanyId.Value} for update.");
                return Error<CompanyTaskstatusDto>($"Company task status with ID {id} not found for the specified company.", StatusCodes.Status404NotFound, "not_found");
            }

            // Prevent editing name if IsEditableName is false
            if (!companyTaskstatus.IsEditableName && companyTaskstatus.Name != updateCompanyTaskstatusDto.Name)
            {
                _logger.LogWarning($"Attempted to modify non-editable name for company task status ID {id}.");
                return Error<CompanyTaskstatusDto>("This task status name cannot be edited.", StatusCodes.Status400BadRequest, "bad_request");
            }

            // Check if the name has changed and if the new name is a duplicate within the same company
            if (companyTaskstatus.Name != updateCompanyTaskstatusDto.Name)
            {
                // Check for duplicate name (excluding the current entity being updated)
                if (await _context.CompanyTaskStatuses.AnyAsync(cts =>
                    cts.CompanyId == resolvedCompanyId.Value &&
                    cts.Name == updateCompanyTaskstatusDto.Name &&
                    cts.Id != id))
                {
                    _logger.LogWarning($"Attempted to update company task status ID {id} to duplicate name: '{updateCompanyTaskstatusDto.Name}' for Company ID: {resolvedCompanyId.Value}.");
                    return Error<CompanyTaskstatusDto>($"A task status with the name '{updateCompanyTaskstatusDto.Name}' already exists for this company.", StatusCodes.Status409Conflict, "conflict");
                }

                companyTaskstatus.Name = updateCompanyTaskstatusDto.Name;

                // --- REGENERATE SLUG IF NAME HAS CHANGED ---
                // We pass 'id' to GenerateUniqueSlug to exclude the current entity from uniqueness check
                companyTaskstatus.Slug = await GenerateUniqueSlug(GenerateSlug(updateCompanyTaskstatusDto.Name), resolvedCompanyId.Value, id);
                _logger.LogInformation($"Task status name changed to '{updateCompanyTaskstatusDto.Name}'. Regenerated slug: '{companyTaskstatus.Slug}'.");
            }
            // --- END SLUG HANDLING ON UPDATE ---
            // If the name hasn't changed, the slug remains as is.
            // If the client explicitly sends a slug and the name hasn't changed,
            // but you still want to allow updating the slug, you would add logic here
            // e.g., if (string.IsNullOrWhiteSpace(updateCompanyTaskstatusDto.Name) && companyTaskstatus.Slug != updateCompanyTaskstatusDto.Slug) { /* update slug based on dto */ }
            // However, it's generally recommended to derive slug from name for consistency.


            companyTaskstatus.StatusColor = updateCompanyTaskstatusDto.StatusColor;
            companyTaskstatus.Order = updateCompanyTaskstatusDto.Order; // Allow updating order

            companyTaskstatus.UpdatedAt = DateTime.UtcNow;
            companyTaskstatus.UpdatedBy = GetCurrentUserId();

            _context.Entry(companyTaskstatus).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Company task status with ID: {id} updated successfully for Company ID: {resolvedCompanyId.Value}.");
            var responseDto = new CompanyTaskstatusDto
            {
                Id = companyTaskstatus.Id,
                CompanyId = companyTaskstatus.CompanyId,
                DefaultTaskStatusId = companyTaskstatus.DefaultTaskStatusId,
                Name = companyTaskstatus.Name,
                StatusColor = companyTaskstatus.StatusColor,
                Slug = companyTaskstatus.Slug, // Ensure Slug is in the DTO for response
                Order = companyTaskstatus.Order,
                IsCustom = companyTaskstatus.IsCustom,
                IsEditableName = companyTaskstatus.IsEditableName,
                IsDeletable = companyTaskstatus.IsDeletable
            };
            return Success("Company task status updated successfully.", responseDto);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (!CompanyTaskstatusExists(id, updateCompanyTaskstatusDto.CompanyId))
            {
                _logger.LogError(ex, $"Company task status with ID {id} not found during concurrency check for Company ID: {updateCompanyTaskstatusDto.CompanyId}.");
                return Error<CompanyTaskstatusDto>($"Company task status with ID {id} not found during concurrency check.", StatusCodes.Status404NotFound, "not_found");
            }
            else
            {
                _logger.LogError(ex, $"Concurrency conflict while updating company task status with ID: {id} for Company ID: {updateCompanyTaskstatusDto.CompanyId}.");
                return Error<CompanyTaskstatusDto>("Concurrency error: The company task status was modified by another user.", StatusCodes.Status409Conflict, "concurrency_conflict");
            }
        }
        catch (DbUpdateException ex)
        {
            var innerException = ex.InnerException as Npgsql.PostgresException;
            if (innerException != null)
            {
                if (innerException.SqlState == "23505") // Unique violation
                {
                    _logger.LogError(ex, $"Database conflict while updating company task status with ID: {id}. Details: {innerException.Message}");
                    return Error<CompanyTaskstatusDto>($"A status with this name or slug already exists for the company or ID conflicts. Details: {innerException.Detail ?? innerException.Message}", StatusCodes.Status409Conflict, "conflict");
                }
                if (innerException.SqlState == "23502") // Not-null constraint violation
                {
                    _logger.LogError(ex, $"Database Not Null Violation while updating company task status '{updateCompanyTaskstatusDto.Name}'. Missing required field: {innerException.Message}");
                    return Error<CompanyTaskstatusDto>($"A required database field was missing. Details: {innerException.Detail ?? innerException.Message}", StatusCodes.Status400BadRequest, "bad_request");
                }
                if (innerException.SqlState == "23503") // Foreign key constraint violation
                {
                    _logger.LogError(ex, $"Database Foreign Key Violation while updating company task status '{updateCompanyTaskstatusDto.Name}'. Invalid reference: {innerException.Message}");
                    return Error<CompanyTaskstatusDto>($"An invalid company ID or other reference was provided. Details: {innerException.Detail ?? innerException.Message}", StatusCodes.Status400BadRequest, "bad_request");
                }
            }
            _logger.LogError(ex, $"A general database error occurred while updating company task status with ID: {id}.");
            return Error<CompanyTaskstatusDto>("A database error occurred while updating the company task status.", StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An unexpected error occurred while updating company task status with ID: {id}.");
            return Error<CompanyTaskstatusDto>("An unexpected error occurred while updating the company task status.", StatusCodes.Status500InternalServerError);
        }
    }

    // DELETE: api/Taskstatuses/5?companyId={companyId}
    [HttpDelete("{id}")]
    [Authorize(Policy = "CanTaskStatusDelete")] // Added permission
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCompanyTaskstatus(int id, [FromQuery] int? companyId)
    {
        try
        {
            var resolvedCompanyId = GetCompanyId(companyId);
            if (!resolvedCompanyId.HasValue)
            {
                return Error<object>("Company ID is required.", StatusCodes.Status400BadRequest, "bad_request");
            }

            _logger.LogInformation($"Attempting to delete company task status with ID: {id} for Company ID: {resolvedCompanyId.Value}");

            var companyTaskstatus = await _context.CompanyTaskStatuses
                                            .FirstOrDefaultAsync(cts => cts.Id == id && cts.CompanyId == resolvedCompanyId.Value);

            if (companyTaskstatus == null)
            {
                _logger.LogWarning($"Company task status with ID {id} not found for Company ID {resolvedCompanyId.Value} for deletion.");
                return Error<object>($"Company task status with ID {id} not found for the specified company.", StatusCodes.Status404NotFound, "not_found");
            }

            // Prevent deletion if IsDeletable is false
            if (!companyTaskstatus.IsDeletable)
            {
                _logger.LogWarning($"Attempted to delete non-deletable company task status ID {id}.");
                return Error<object>("This task status cannot be deleted.", StatusCodes.Status400BadRequest, "bad_request");
            }

            // IMPORTANT: Check if status is in use by tasks before deleting (scoped to the company)
            // You need to update your Task model to link to CompanyTaskStatus.Id instead of Taskstatus.StatusId
            // Ensure t.StatusNavigation is correctly loaded (e.g., using .Include() if needed in the query fetching tasks)
            var tasksUsingStatus = await _context.Tasks.AnyAsync(t => t.StatusNavigation.Id == id && t.CompanyId == resolvedCompanyId.Value);
            if (tasksUsingStatus)
            {
                _logger.LogWarning($"Attempted to delete company task status ID {id} which is in use by tasks for Company ID: {resolvedCompanyId.Value}.");
                return Error<object>("Cannot delete status as it is currently used by one or more tasks for this company. Consider deactivating it instead.", StatusCodes.Status400BadRequest, "bad_request");
            }

            // IMPORTANT: Also check if status is in use by Projects before deleting (scoped to the company)
            // You need to update your Project model to link to CompanyTaskStatus.Id instead of Taskstatus.StatusId
            // Ensure p.StatusNavigation is correctly loaded
            var projectsUsingStatus = await _context.Projects.AnyAsync(p => p.StatusNavigation.Id == id && p.CompanyId == resolvedCompanyId.Value);
            if (projectsUsingStatus)
            {
                _logger.LogWarning($"Attempted to delete company task status ID {id} which is in use by projects for Company ID: {resolvedCompanyId.Value}.");
                return Error<object>("Cannot delete status as it is currently used by one or more projects for this company. Consider deactivating it instead.", StatusCodes.Status400BadRequest, "bad_request");
            }

            _context.CompanyTaskStatuses.Remove(companyTaskstatus);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Company task status with ID: {id} deleted successfully for Company ID: {resolvedCompanyId.Value}.");
            return Success<object>("Company task status deleted successfully.", null, StatusCodes.Status200OK);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, $"A database error occurred while deleting company task status with ID: {id}.");
            return Error<object>("A database error occurred while deleting the company task status.", StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An unexpected error occurred while deleting company task status with ID: {id}.");
            return Error<object>("An unexpected error occurred while deleting the company task status.", StatusCodes.Status500InternalServerError);
        }
    }

    private bool CompanyTaskstatusExists(int id, int companyId)
    {
        return _context.CompanyTaskStatuses.Any(e => e.Id == id && e.CompanyId == companyId);
    }
}
