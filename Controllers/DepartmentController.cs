using BizfreeApp.Data;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BizfreeApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DepartmentController : ControllerBase
    {
        private readonly Data.ApplicationDbContext _context;
        private readonly ILogger<DepartmentController> _logger;

        public DepartmentController(Data.ApplicationDbContext context, ILogger<DepartmentController> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Helper Methods

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : null;
        }

        private int? GetCurrentUserCompanyId()
        {
            var companyIdClaim = User.FindFirst("CompanyId")?.Value;
            return int.TryParse(companyIdClaim, out var companyId) ? companyId : null;
        }

        private int? GetCurrentUserRoleId()
        {
            var roleIdClaim = User.FindFirst("RoleId")?.Value;
            return int.TryParse(roleIdClaim, out var roleId) ? roleId : null;
        }

        private ActionResult<ApiResponse<T>> Success<T>(string message, T? data, int statusCode = StatusCodes.Status200OK)
        {
            return StatusCode(statusCode, new ApiResponse<T>(message, "Success", statusCode, data));
        }

        private ActionResult<ApiResponse<T>> Error<T>(string message, int statusCode, string status = "Error")
        {
            return StatusCode(statusCode, new ApiResponse<T>(message, status, statusCode, default(T)));
        }

        private IActionResult Error(string message, int statusCode, string status = "Error")
        {
            return StatusCode(statusCode, new ApiResponse<object>(message, status, statusCode, null));
        }

        #endregion

        // GET: api/Department
        [HttpGet]
        [Authorize(Policy = "CanDepartmentRead")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<DepartmentDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<IEnumerable<DepartmentDto>>>> GetDepartments([FromQuery] int? companyId = null)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserCompanyId = GetCurrentUserCompanyId();
                var currentUserRoleId = GetCurrentUserRoleId();

                if (!currentUserId.HasValue || !currentUserCompanyId.HasValue || !currentUserRoleId.HasValue)
                {
                    return Error<IEnumerable<DepartmentDto>>("Authentication information is missing.", StatusCodes.Status401Unauthorized);
                }

                int targetCompanyId;

                if (companyId.HasValue)
                {
                    if (!User.HasClaim("permission", "department:read:all"))
                    {
                        return Error<IEnumerable<DepartmentDto>>("You don't have permission to view departments from other companies.", StatusCodes.Status403Forbidden);
                    }
                    targetCompanyId = companyId.Value;
                }
                else
                {
                    targetCompanyId = currentUserCompanyId.Value;
                }

                var companyExists = await _context.Companies
                    .AnyAsync(c => c.CompanyId == targetCompanyId && c.IsActive == true && c.IsDeleted == false);

                if (!companyExists)
                {
                    return Error<IEnumerable<DepartmentDto>>("Company not found.", StatusCodes.Status404NotFound);
                }

                var departments = await _context.Departments
                    .Where(d => d.CompanyId == targetCompanyId && (d.IsDeleted == false || d.IsDeleted == null))
                    .Include(d => d.Company)
                    .Include(d => d.DepartmentHead) // Resolves by EmployeeId through foreign key
                    .Include(d => d.CreatedByNavigation)
                        .ThenInclude(u => u.CompanyUserUsers)
                    .Include(d => d.UpdatedByNavigation)
                        .ThenInclude(u => u.CompanyUserUsers)
                    .OrderBy(d => d.DepartmentName)
                    .Select(d => new DepartmentDto
                    {
                        DeptId = d.DeptId,
                        CompanyId = d.CompanyId,
                        DepartmentName = d.DepartmentName,
                        Description = d.Description,
                        DepartmentHeadUserId = d.DepartmentHeadUserId, // Contains EmployeeId value
                        DepartmentHeadName = d.DepartmentHead != null
                            ? $"{d.DepartmentHead.FirstName} {d.DepartmentHead.LastName}".Trim()
                            : null,
                        CreatedAt = d.CreatedAt,
                        CreatedBy = d.CreatedBy,
                        UpdatedAt = d.UpdatedAt,
                        UpdatedBy = d.UpdatedBy,
                        IsDeleted = d.IsDeleted,
                        CompanyName = d.Company.CompanyName,
                        CreatedByName = d.CreatedByNavigation != null && d.CreatedByNavigation.CompanyUserUsers.Any()
                            ? $"{d.CreatedByNavigation.CompanyUserUsers.First().FirstName} {d.CreatedByNavigation.CompanyUserUsers.First().LastName}".Trim()
                            : d.CreatedByNavigation != null ? d.CreatedByNavigation.Email : null,
                        UpdatedByName = d.UpdatedByNavigation != null && d.UpdatedByNavigation.CompanyUserUsers.Any()
                            ? $"{d.UpdatedByNavigation.CompanyUserUsers.First().FirstName} {d.UpdatedByNavigation.CompanyUserUsers.First().LastName}".Trim()
                            : d.UpdatedByNavigation != null ? d.UpdatedByNavigation.Email : null
                    })
                    .ToListAsync();

                _logger.LogInformation($"Retrieved {departments.Count} departments for company ID {targetCompanyId}");

                return Success($"Departments retrieved successfully for company ID {targetCompanyId}.", (IEnumerable<DepartmentDto>)departments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving departments for company ID {CompanyId}", companyId);
                return Error<IEnumerable<DepartmentDto>>("An error occurred while retrieving departments.", StatusCodes.Status500InternalServerError);
            }
        }

        // GET: api/Department/5
        [HttpGet("{id}")]
        [Authorize(Policy = "CanDepartmentRead")]
        [ProducesResponseType(typeof(ApiResponse<DepartmentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<DepartmentDto>>> GetDepartment(int id)
        {
            try
            {
                var currentUserCompanyId = GetCurrentUserCompanyId();
                var currentUserRoleId = GetCurrentUserRoleId();

                if (!currentUserCompanyId.HasValue || !currentUserRoleId.HasValue)
                {
                    return Error<DepartmentDto>("Authentication information is missing.", StatusCodes.Status401Unauthorized);
                }

                var department = await _context.Departments
                    .Where(d => d.DeptId == id && (d.IsDeleted == false || d.IsDeleted == null))
                    .Include(d => d.Company)
                    .Include(d => d.DepartmentHead) // Resolves by EmployeeId through foreign key
                    .Include(d => d.CreatedByNavigation)
                        .ThenInclude(u => u.CompanyUserUsers)
                    .Include(d => d.UpdatedByNavigation)
                        .ThenInclude(u => u.CompanyUserUsers)
                    .Select(d => new DepartmentDto
                    {
                        DeptId = d.DeptId,
                        CompanyId = d.CompanyId,
                        DepartmentName = d.DepartmentName,
                        Description = d.Description,
                        DepartmentHeadUserId = d.DepartmentHeadUserId, // Contains EmployeeId value
                        DepartmentHeadName = d.DepartmentHead != null
                            ? $"{d.DepartmentHead.FirstName} {d.DepartmentHead.LastName}".Trim()
                            : null,
                        CreatedAt = d.CreatedAt,
                        CreatedBy = d.CreatedBy,
                        UpdatedAt = d.UpdatedAt,
                        UpdatedBy = d.UpdatedBy,
                        IsDeleted = d.IsDeleted,
                        CompanyName = d.Company.CompanyName,
                        CreatedByName = d.CreatedByNavigation != null && d.CreatedByNavigation.CompanyUserUsers.Any()
                            ? $"{d.CreatedByNavigation.CompanyUserUsers.First().FirstName} {d.CreatedByNavigation.CompanyUserUsers.First().LastName}".Trim()
                            : d.CreatedByNavigation != null ? d.CreatedByNavigation.Email : null,
                        UpdatedByName = d.UpdatedByNavigation != null && d.UpdatedByNavigation.CompanyUserUsers.Any()
                            ? $"{d.UpdatedByNavigation.CompanyUserUsers.First().FirstName} {d.UpdatedByNavigation.CompanyUserUsers.First().LastName}".Trim()
                            : d.UpdatedByNavigation != null ? d.UpdatedByNavigation.Email : null
                    })
                    .FirstOrDefaultAsync();

                if (department == null)
                {
                    return Error<DepartmentDto>("Department not found.", StatusCodes.Status404NotFound);
                }

                if (department.CompanyId != currentUserCompanyId.Value && !User.HasClaim("permission", "department:read:all"))
                {
                    return Error<DepartmentDto>("You don't have permission to view this department.", StatusCodes.Status403Forbidden);
                }

                return Success("Department retrieved successfully.", department);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving department with ID: {Id}", id);
                return Error<DepartmentDto>("An error occurred while retrieving the department.", StatusCodes.Status500InternalServerError);
            }
        }

        // POST: api/Department
        [HttpPost]
        [Authorize(Policy = "CanDepartmentCreate")]
        [ProducesResponseType(typeof(ApiResponse<DepartmentDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<DepartmentDto>>> CreateDepartment(DepartmentCreateDto createDto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserCompanyId = GetCurrentUserCompanyId();
                var currentUserRoleId = GetCurrentUserRoleId();

                if (!currentUserId.HasValue || !currentUserCompanyId.HasValue || !currentUserRoleId.HasValue)
                {
                    return Error<DepartmentDto>("Authentication information is missing.", StatusCodes.Status401Unauthorized);
                }

                var targetCompanyId = currentUserCompanyId.Value;

                var companyExists = await _context.Companies
                    .AnyAsync(c => c.CompanyId == targetCompanyId && c.IsActive == true && c.IsDeleted == false);

                if (!companyExists)
                {
                    return Error<DepartmentDto>($"Company with ID {targetCompanyId} does not exist.", StatusCodes.Status400BadRequest);
                }

                // Frontend sends UserId, convert to EmployeeId for database
                CompanyUser? departmentHead = null;
                int? employeeIdToStore = null;

                if (createDto.DepartmentHeadUserId.HasValue)
                {
                    // Query by UserId (from frontend)
                    departmentHead = await _context.CompanyUsers
                        .FirstOrDefaultAsync(cu => cu.UserId == createDto.DepartmentHeadUserId.Value &&
                                                  cu.CompanyId == targetCompanyId &&
                                                  !(cu.IsDeleted ?? false));

                    if (departmentHead == null)
                    {
                        return Error<DepartmentDto>($"The specified department head (UserId: {createDto.DepartmentHeadUserId.Value}) does not exist or does not belong to this company.", StatusCodes.Status400BadRequest);
                    }

                    // Check if this employee is already managing another department
                    if (departmentHead.ManagedDepartmentId.HasValue)
                    {
                        var existingDept = await _context.Departments
                            .Where(d => d.DeptId == departmentHead.ManagedDepartmentId.Value && !(d.IsDeleted ?? false))
                            .Select(d => d.DepartmentName)
                            .FirstOrDefaultAsync();

                        if (existingDept != null)
                        {
                            return Error<DepartmentDto>($"This employee is already managing the department '{existingDept}'. An employee can only manage one department at a time.", StatusCodes.Status409Conflict);
                        }
                    }

                    // Store EmployeeId for database (convert UserId → EmployeeId)
                    employeeIdToStore = departmentHead.EmployeeId;
                }

                var departmentExists = await _context.Departments
                    .AnyAsync(d => d.CompanyId == targetCompanyId &&
                                  d.DepartmentName == createDto.DepartmentName &&
                                  (d.IsDeleted == false || d.IsDeleted == null));

                if (departmentExists)
                {
                    return Error<DepartmentDto>($"A department with the name '{createDto.DepartmentName}' already exists in this company.", StatusCodes.Status409Conflict);
                }

                // Create department with EmployeeId
                var department = new Department
                {
                    CompanyId = targetCompanyId,
                    DepartmentName = createDto.DepartmentName,
                    Description = createDto.Description,
                    DepartmentHeadUserId = employeeIdToStore, // Store EmployeeId (not UserId)
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = currentUserId.Value,
                    IsDeleted = false
                };

                _context.Departments.Add(department);
                await _context.SaveChangesAsync();

                // Update the CompanyUser's ManagedDepartmentId
                if (departmentHead != null)
                {
                    departmentHead.ManagedDepartmentId = department.DeptId;
                    departmentHead.UpdatedAt = DateTime.UtcNow;
                    departmentHead.UpdatedBy = currentUserId.Value;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Updated CompanyUser (UserId: {departmentHead.UserId}, EmployeeId: {departmentHead.EmployeeId}) ManagedDepartmentId to {department.DeptId}");
                }

                var createdDepartment = await _context.Departments
                    .Where(d => d.DeptId == department.DeptId)
                    .Include(d => d.Company)
                    .Include(d => d.DepartmentHead)
                    .Include(d => d.CreatedByNavigation)
                        .ThenInclude(u => u.CompanyUserUsers)
                    .Select(d => new DepartmentDto
                    {
                        DeptId = d.DeptId,
                        CompanyId = d.CompanyId,
                        DepartmentName = d.DepartmentName,
                        Description = d.Description,
                        DepartmentHeadUserId = d.DepartmentHead != null ? d.DepartmentHead.UserId : (int?)null, // Return UserId to frontend
                        DepartmentHeadName = d.DepartmentHead != null
                            ? $"{d.DepartmentHead.FirstName} {d.DepartmentHead.LastName}".Trim()
                            : null,
                        CreatedAt = d.CreatedAt,
                        CreatedBy = d.CreatedBy,
                        UpdatedAt = d.UpdatedAt,
                        UpdatedBy = d.UpdatedBy,
                        IsDeleted = d.IsDeleted,
                        CompanyName = d.Company.CompanyName,
                        CreatedByName = d.CreatedByNavigation != null && d.CreatedByNavigation.CompanyUserUsers.Any()
                            ? $"{d.CreatedByNavigation.CompanyUserUsers.First().FirstName} {d.CreatedByNavigation.CompanyUserUsers.First().LastName}".Trim()
                            : d.CreatedByNavigation != null ? d.CreatedByNavigation.Email : null
                    })
                    .FirstOrDefaultAsync();

                _logger.LogInformation($"Department '{department.DepartmentName}' created with ID {department.DeptId} for company {targetCompanyId}. Department head UserId: {createDto.DepartmentHeadUserId}");

                return Success("Department created successfully.", createdDepartment, StatusCodes.Status201Created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating department.");
                return Error<DepartmentDto>("An error occurred while creating the department.", StatusCodes.Status500InternalServerError);
            }
        }

        // PUT: api/Department/5
        [HttpPut("{id}")]
        [Authorize(Policy = "CanDepartmentUpdate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateDepartment(int id, DepartmentUpdateDto updateDto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserCompanyId = GetCurrentUserCompanyId();
                var currentUserRoleId = GetCurrentUserRoleId();

                if (!currentUserId.HasValue || !currentUserCompanyId.HasValue || !currentUserRoleId.HasValue)
                {
                    return Error("Authentication information is missing.", StatusCodes.Status401Unauthorized);
                }

                var department = await _context.Departments
                    .Include(d => d.DepartmentHead)
                    .FirstOrDefaultAsync(d => d.DeptId == id && (d.IsDeleted == false || d.IsDeleted == null));

                if (department == null)
                {
                    return Error("Department not found.", StatusCodes.Status404NotFound);
                }

                if (department.CompanyId != currentUserCompanyId.Value && !User.HasClaim("permission", "department:read:all"))
                {
                    return Error("You can only update departments for your own company.", StatusCodes.Status403Forbidden);
                }

                // Check for duplicate department name
                if (department.DepartmentName != updateDto.DepartmentName)
                {
                    var duplicateExists = await _context.Departments
                        .AnyAsync(d => d.CompanyId == department.CompanyId &&
                                      d.DepartmentName == updateDto.DepartmentName &&
                                      d.DeptId != id &&
                                      (d.IsDeleted == false || d.IsDeleted == null));

                    if (duplicateExists)
                    {
                        return Error($"A department with the name '{updateDto.DepartmentName}' already exists in this company.", StatusCodes.Status409Conflict);
                    }
                }

                // Frontend sends UserId, convert to EmployeeId for comparison and storage
                int? newEmployeeId = null;
                CompanyUser? newHead = null;

                if (updateDto.DepartmentHeadUserId.HasValue)
                {
                    // Query by UserId (from frontend)
                    newHead = await _context.CompanyUsers
                        .FirstOrDefaultAsync(cu => cu.UserId == updateDto.DepartmentHeadUserId.Value &&
                                                  cu.CompanyId == department.CompanyId &&
                                                  !(cu.IsDeleted ?? false));

                    if (newHead == null)
                    {
                        return Error($"The specified department head (UserId: {updateDto.DepartmentHeadUserId.Value}) does not exist or does not belong to this company.", StatusCodes.Status400BadRequest);
                    }

                    newEmployeeId = newHead.EmployeeId; // Convert UserId → EmployeeId

                    // Check if new head is already managing another department
                    if (newHead.ManagedDepartmentId.HasValue && newHead.ManagedDepartmentId != id)
                    {
                        var existingDept = await _context.Departments
                            .Where(d => d.DeptId == newHead.ManagedDepartmentId.Value && !(d.IsDeleted ?? false))
                            .Select(d => d.DepartmentName)
                            .FirstOrDefaultAsync();

                        if (existingDept != null)
                        {
                            return Error($"This employee is already managing the department '{existingDept}'. An employee can only manage one department at a time.", StatusCodes.Status409Conflict);
                        }
                    }
                }

                // Handle department head change (compare EmployeeIds)
                if (newEmployeeId != department.DepartmentHeadUserId)
                {
                    // Clear ManagedDepartmentId from old department head
                    if (department.DepartmentHeadUserId.HasValue)
                    {
                        var oldHead = await _context.CompanyUsers
                            .FirstOrDefaultAsync(cu => cu.EmployeeId == department.DepartmentHeadUserId.Value);

                        if (oldHead != null && oldHead.ManagedDepartmentId == id)
                        {
                            oldHead.ManagedDepartmentId = null;
                            oldHead.UpdatedAt = DateTime.UtcNow;
                            oldHead.UpdatedBy = currentUserId.Value;

                            _logger.LogInformation($"Cleared ManagedDepartmentId for old department head (UserId: {oldHead.UserId}, EmployeeId: {oldHead.EmployeeId})");
                        }
                    }

                    // Set ManagedDepartmentId for new department head
                    if (newHead != null)
                    {
                        newHead.ManagedDepartmentId = id;
                        newHead.UpdatedAt = DateTime.UtcNow;
                        newHead.UpdatedBy = currentUserId.Value;

                        _logger.LogInformation($"Set ManagedDepartmentId to {id} for new department head (UserId: {newHead.UserId}, EmployeeId: {newHead.EmployeeId})");
                    }
                }

                // Update department properties (store EmployeeId)
                department.DepartmentName = updateDto.DepartmentName;
                department.Description = updateDto.Description;
                department.DepartmentHeadUserId = newEmployeeId; // Store EmployeeId (not UserId)
                department.UpdatedAt = DateTime.UtcNow;
                department.UpdatedBy = currentUserId.Value;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Department {id} ('{department.DepartmentName}') updated successfully. Department head UserId: {updateDto.DepartmentHeadUserId?.ToString() ?? "None"}");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating department with ID: {Id}", id);
                return Error("An error occurred while updating the department.", StatusCodes.Status500InternalServerError);
            }
        }

        // DELETE: api/Department/5
        [HttpDelete("{id}")]
        [Authorize(Policy = "CanDepartmentDelete")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteDepartment(int id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserCompanyId = GetCurrentUserCompanyId();
                var currentUserRoleId = GetCurrentUserRoleId();

                if (!currentUserId.HasValue || !currentUserCompanyId.HasValue || !currentUserRoleId.HasValue)
                {
                    return Error("Authentication information is missing.", StatusCodes.Status401Unauthorized);
                }

                var department = await _context.Departments
                    .FirstOrDefaultAsync(d => d.DeptId == id && (d.IsDeleted == false || d.IsDeleted == null));

                if (department == null)
                {
                    return Error("Department not found.", StatusCodes.Status404NotFound);
                }

                if (department.CompanyId != currentUserCompanyId.Value && !User.HasClaim("permission", "department:read:all"))
                {
                    return Error("You can only delete departments for your own company.", StatusCodes.Status403Forbidden);
                }

                var hasActiveUsers = await _context.CompanyUsers
                    .AnyAsync(cu => cu.DepartmentId == id && (cu.IsDeleted == false || cu.IsDeleted == null));

                if (hasActiveUsers)
                {
                    return Error("Cannot delete department. It has active users assigned to it.", StatusCodes.Status409Conflict);
                }

                department.IsDeleted = true;
                department.UpdatedAt = DateTime.UtcNow;
                department.UpdatedBy = currentUserId.Value;

                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting department with ID: {Id}", id);
                return Error("An error occurred while deleting the department.", StatusCodes.Status500InternalServerError);
            }
        }
    }
}
