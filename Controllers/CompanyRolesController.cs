using BizfreeApp.Data;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BizfreeApp.Constants;

namespace BizfreeApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompanyRolesController : ControllerBase
    {
        private readonly Data.ApplicationDbContext _context;
        private readonly ILogger<CompanyRolesController> _logger;

        public CompanyRolesController(Data.ApplicationDbContext context, ILogger<CompanyRolesController> logger)
        {
            _context = context;
            _logger = logger;
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

        // Helper method to generate a standardized success response
        private ActionResult<ApiResponse<T>> Success<T>(string message, T? data, int statusCode = StatusCodes.Status200OK)
        {
            return StatusCode(statusCode, new ApiResponse<T>(message, "success", statusCode, data));
        }

        // Helper method to generate a standardized error response
        private ActionResult<ApiResponse<T>> Error<T>(string message, int statusCode, string status = "error")
        {
            return StatusCode(statusCode, new ApiResponse<T>(message, status, statusCode, default(T)));
        }

        // Overloaded Helper method for plain IActionResult
        private IActionResult Error(string message, int statusCode, string status = "error")
        {
            return StatusCode(statusCode, new ApiResponse<object>(message, status, statusCode, null));
        }

        // GET: api/CompanyRoles
        [HttpGet]
        [Authorize(Policy = "CanCompanyRoleRead")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<CompanyRoleDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<IEnumerable<CompanyRoleDto>>>> GetCompanyRoles([FromQuery] int? companyId = null)
        {
            try
            {
                var currentUserId = GetCurrentUserIdFromClaims();
                var currentUserCompanyId = GetCompanyIdFromClaims();

                if (!currentUserId.HasValue || !currentUserCompanyId.HasValue)
                {
                    return Error<IEnumerable<CompanyRoleDto>>("Authentication information is missing.", StatusCodes.Status401Unauthorized);
                }
                // Determine which company's roles to fetch
                int targetCompanyId;

                if (companyId.HasValue)
                {
                    if (companyId.Value != currentUserCompanyId.Value)
                    {
                        return Error<IEnumerable<CompanyRoleDto>>("You don't have permission to view roles from other companies.", StatusCodes.Status403Forbidden);
                    }
                    targetCompanyId = companyId.Value;
                }
                else
                {
                    // If no companyId provided, use current user's company
                    targetCompanyId = currentUserCompanyId.Value;
                }

                // Validate that the target company exists
                var companyExists = await _context.Companies
    .AnyAsync(c => c.CompanyId == targetCompanyId &&
                   c.IsActive.HasValue && c.IsActive.Value &&
                   !c.IsDeleted);


                if (!companyExists)
                {
                    return Error<IEnumerable<CompanyRoleDto>>("Company not found.", StatusCodes.Status404NotFound);
                }

                // Fetch company roles for the target company
                var companyRoles = await _context.CompanyRoles
                    .Where(cr => cr.CompanyId == targetCompanyId &&
                                //cr.IsActive == true &&
                                !cr.IsDeleted)
                    .OrderBy(cr => cr.RoleName)
                    .Select(cr => new CompanyRoleDto
                    {
                        CompanyRoleId = cr.CompanyRoleId,
                        CompanyId = cr.CompanyId,
                        RoleName = cr.RoleName,
                        IsActive = cr.IsActive,
                        DefaultRole = cr.DefaultRole,
                        DefaultPermission = cr.DefaultPermission,
                        CreatedAt = cr.CreatedAt
                    })
                    .ToListAsync();

                _logger.LogInformation($"Retrieved {companyRoles.Count} company roles for company ID {targetCompanyId}");

                return Success($"Company roles retrieved successfully for company ID {targetCompanyId}.", (IEnumerable<CompanyRoleDto>)companyRoles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving company roles for company ID {CompanyId}", companyId);
                return Error<IEnumerable<CompanyRoleDto>>("An error occurred while retrieving company roles.", StatusCodes.Status500InternalServerError);
            }
        }

        // GET: api/CompanyRoles/5
        [HttpGet("{id}")]
        [Authorize(Policy = "CanCompanyRoleRead")] // Added permission
        [ProducesResponseType(typeof(ApiResponse<CompanyRoleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CompanyRoleDto>>> GetCompanyRole(int id)
        {
            try
            {
                var currentUserCompanyId = GetCompanyIdFromClaims();

                if (!currentUserCompanyId.HasValue)
                {
                    return Error<CompanyRoleDto>("Authentication information is missing.", StatusCodes.Status401Unauthorized);
                }

                var companyRole = await _context.CompanyRoles
                    .Where(cr => cr.CompanyRoleId == id && !cr.IsDeleted)
                    .Select(cr => new CompanyRoleDto
                    {
                        CompanyRoleId = cr.CompanyRoleId,
                        CompanyId = cr.CompanyId,
                        RoleName = cr.RoleName,
                        IsActive = cr.IsActive,
                        IsDeleted = cr.IsDeleted,
                        CreatedAt = cr.CreatedAt,
                        CreatedBy = cr.CreatedBy,
                        UpdatedAt = cr.UpdatedAt,
                        UpdatedBy = cr.UpdatedBy,
                        DefaultRole = cr.DefaultRole,
                        DefaultPermission = cr.DefaultPermission
                    })
                    .FirstOrDefaultAsync();

                if (companyRole == null)
                {
                    return Error<CompanyRoleDto>("Company role not found.", StatusCodes.Status404NotFound);
                }

                if (companyRole.CompanyId != currentUserCompanyId.Value)
                {
                    return Error<CompanyRoleDto>("You don't have permission to view this company role.", StatusCodes.Status403Forbidden);
                }

                return Success("Company role retrieved successfully.", companyRole);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving company role with ID: {id}.");
                return Error<CompanyRoleDto>("An error occurred while retrieving the company role.", StatusCodes.Status500InternalServerError);
            }
        }

        // POST: api/CompanyRoles
        [HttpPost]
        [Authorize(Policy = "CanCompanyRoleCreate")]
        [ProducesResponseType(typeof(ApiResponse<CompanyRole>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CompanyRole>>> PostCompanyRole(CompanyRoleCreateDto createDto)
        {
            try
            {
                var currentUserId = GetCurrentUserIdFromClaims();
                var currentUserCompanyId = GetCompanyIdFromClaims();

                if (!currentUserId.HasValue || !currentUserCompanyId.HasValue)
                    return Error<CompanyRole>("Authentication information is missing.", StatusCodes.Status401Unauthorized);

                if (createDto.CompanyId != currentUserCompanyId.Value)
                    return Error<CompanyRole>("You can only create roles for your own company.", StatusCodes.Status403Forbidden);

                var companyExists = await _context.Companies
                    .AnyAsync(c => c.CompanyId == createDto.CompanyId && c.IsActive == true && c.IsDeleted == false);

                if (!companyExists)
                    return Error<CompanyRole>($"Company with ID {createDto.CompanyId} does not exist.", StatusCodes.Status400BadRequest);

                var validPermissions = await _context.Permissions
                    .Where(p => createDto.PermissionIds.Contains(p.PermissionId))
                    .Select(p => p.PermissionName)
                    .ToListAsync();

                if (validPermissions.Count != createDto.PermissionIds.Count)
                    return Error<CompanyRole>("Some provided permissions are invalid.", StatusCodes.Status400BadRequest);

                // Always ensure every new role can view its own assigned tasks
                if (!validPermissions.Contains(Permissions.TaskReadAssigned))
                    validPermissions.Add(Permissions.TaskReadAssigned);

                var serializedPermissions = System.Text.Json.JsonSerializer.Serialize(validPermissions);

                var activeDuplicate = await _context.CompanyRoles.FirstOrDefaultAsync(cr =>
                    cr.CompanyId == createDto.CompanyId &&
                    cr.RoleName == createDto.RoleName &&
                    (cr.IsDeleted == false || cr.IsDeleted == null));

                if (activeDuplicate != null)
                    return Error<CompanyRole>($"A role with the name '{createDto.RoleName}' already exists for Company ID {createDto.CompanyId}.", StatusCodes.Status409Conflict);

                var softDeleted = await _context.CompanyRoles.FirstOrDefaultAsync(cr =>
                    cr.CompanyId == createDto.CompanyId &&
                    cr.RoleName == createDto.RoleName &&
                    cr.IsDeleted == true);

                if (softDeleted != null)
                {
                    softDeleted.IsDeleted = false;
                    softDeleted.IsActive = createDto.IsActive;
                    softDeleted.DefaultRole = createDto.DefaultRole;
                    softDeleted.DefaultPermission = serializedPermissions;
                    softDeleted.UpdatedAt = DateTime.UtcNow;
                    softDeleted.UpdatedBy = currentUserId;

                    await _context.SaveChangesAsync();
                    return Success("Company role restored and updated successfully.", softDeleted, StatusCodes.Status201Created);
                }

                var companyRole = new CompanyRole
                {
                    CompanyId = createDto.CompanyId,
                    RoleName = createDto.RoleName,
                    IsActive = createDto.IsActive,
                    DefaultRole = createDto.DefaultRole,
                    DefaultPermission = serializedPermissions,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = currentUserId
                };

                _context.CompanyRoles.Add(companyRole);
                await _context.SaveChangesAsync();

                return Success("Company role created successfully.", companyRole, StatusCodes.Status201Created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating company role.");
                return Error<CompanyRole>("An error occurred while creating the company role.", StatusCodes.Status500InternalServerError);
            }
        }

        // PUT: api/CompanyRoles/5
        [HttpPut("{id}")]
        [Authorize(Policy = "CanCompanyRoleUpdate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PutCompanyRole(int id, CompanyRoleUpdateDto updateDto)
        {
            try
            {
                var currentUserId = GetCurrentUserIdFromClaims();
                var currentUserCompanyId = GetCompanyIdFromClaims();

                if (!currentUserId.HasValue || !currentUserCompanyId.HasValue)
                {
                    return Error("Authentication information is missing.", StatusCodes.Status401Unauthorized);
                }

                var companyRole = await _context.CompanyRoles.FindAsync(id);
                if (companyRole == null)
                {
                    return Error("Company role not found.", StatusCodes.Status404NotFound);
                }

                if (companyRole.CompanyId != currentUserCompanyId.Value)
                {
                    return Error("You can only update roles for your own company.", StatusCodes.Status403Forbidden);
                }

                // Check for uniqueness of RoleName within the same Company (if RoleName is being changed)
                if (companyRole.RoleName != updateDto.RoleName)
                {
                    var nameConflict = await _context.CompanyRoles
                        .AnyAsync(cr => cr.CompanyId == companyRole.CompanyId &&
                                        cr.RoleName == updateDto.RoleName &&
                                        cr.CompanyRoleId != id &&
                                        !cr.IsDeleted);

                    if (nameConflict)
                    {
                        return Error($"A role with the name '{updateDto.RoleName}' already exists for this company.", StatusCodes.Status409Conflict);
                    }
                }
                var validPermissions = await _context.Permissions
                    .Where(p => updateDto.PermissionIds.Contains(p.PermissionId))
                    .Select(p => p.PermissionName)
                    .ToListAsync();

                if (validPermissions.Count != updateDto.PermissionIds.Count)
                {
                    return Error("Some provided permissions are invalid.", StatusCodes.Status400BadRequest);
                }

                // Always ensure task:read-assigned is preserved — employees must always see their own tasks
                if (!validPermissions.Contains(Permissions.TaskReadAssigned))
                    validPermissions.Add(Permissions.TaskReadAssigned);

                var serializedPermissions = System.Text.Json.JsonSerializer.Serialize(validPermissions);

                companyRole.RoleName = updateDto.RoleName;
                companyRole.IsActive = updateDto.IsActive;
                companyRole.DefaultRole = updateDto.DefaultRole;
                companyRole.DefaultPermission = serializedPermissions;
                companyRole.UpdatedAt = DateTime.UtcNow;
                companyRole.UpdatedBy = currentUserId;

                _context.Entry(companyRole).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (DbUpdateConcurrencyException dbEx)
            {
                if (!CompanyRoleExists(id))
                {
                    return Error("Company role not found (concurrency issue).", StatusCodes.Status404NotFound);
                }
                else
                {
                    _logger.LogError(dbEx, $"Concurrency error updating company role with ID: {id}.");
                    return Error("A concurrency error occurred while updating the company role.", StatusCodes.Status409Conflict);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating company role with ID: {id}.");
                return Error("An unexpected error occurred while updating the company role.", StatusCodes.Status500InternalServerError);
            }
        }

        // DELETE: api/CompanyRoles/5
        [HttpDelete("{id}")]
        [Authorize(Policy = "CanCompanyRoleDelete")] // Added permission
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteCompanyRole(int id)
        {
            try
            {
                var currentUserId = GetCurrentUserIdFromClaims();
                var currentUserCompanyId = GetCompanyIdFromClaims();

                if (!currentUserId.HasValue || !currentUserCompanyId.HasValue)
                {
                    return Error("Authentication information is missing.", StatusCodes.Status401Unauthorized);
                }

                var companyRole = await _context.CompanyRoles.FindAsync(id);
                if (companyRole == null || companyRole.IsDeleted)
                {
                    return Error("Company role not found.", StatusCodes.Status404NotFound);
                }

                if (companyRole.CompanyId != currentUserCompanyId.Value)
                {
                    return Error("You can only delete roles for your own company.", StatusCodes.Status403Forbidden);
                }

                // Soft delete
                companyRole.IsDeleted = true;
                companyRole.UpdatedAt = DateTime.UtcNow;
                companyRole.UpdatedBy = currentUserId;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting company role with ID: {id}.");
                return Error("An error occurred while deleting the company role.", StatusCodes.Status500InternalServerError);
            }
        }

        private bool CompanyRoleExists(int id)
        {
            return _context.CompanyRoles.Any(e => e.CompanyRoleId == id && !e.IsDeleted);
        }
    }
}
