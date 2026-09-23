using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs;
using BizfreeApp.Services;
using System;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using System.Text;
using Microsoft.Extensions.Logging;
using BizfreeApp.DTOs;
using Microsoft.AspNetCore.Http; // For IFormFile and StatusCodes
using BizfreeApp.Constants;

namespace BizfreeApp.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize] // Uncomment this line to enforce authentication for all actions in this controller
public class CompanyUsersController : ControllerBase
{
    private readonly BizfreeApp.Data.ApplicationDbContext _context;
    private readonly IUploadHandler _uploadHandler;
    private readonly ILogger<CompanyUsersController> _logger;
    private readonly IEmailSender _emailSender;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IEmailTemplateService _emailTemplateService;

    public CompanyUsersController(BizfreeApp.Data.ApplicationDbContext context,
                                  IUploadHandler uploadHandler,
                                  ILogger<CompanyUsersController> logger,
                                  IEmailSender emailSender,
                                  IPasswordHasher<User> passwordHasher,
                                  IEmailTemplateService emailTemplateService)
    {
        _context = context;
        _uploadHandler = uploadHandler;
        _logger = logger;
        _emailSender = emailSender;
        _passwordHasher = passwordHasher;
        _emailTemplateService = emailTemplateService;
    }

    // --- Helper methods to extract claims ---

    private int? GetCompanyIdFromClaims()
    {
        var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
        if (companyIdClaim == null)
        {
            companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.GroupSid);
        }

        if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out int companyId))
        {
            _logger.LogInformation("GetCompanyIdFromClaims: Company ID found: {CompanyId}", companyId);
            return companyId;
        }
        _logger.LogWarning("GetCompanyIdFromClaims: Company ID claim not found or invalid.");
        return null;
    }

    private int? GetCurrentUserIdFromClaims()
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdClaim == null)
        {
            userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        }

        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            _logger.LogInformation("GetCurrentUserIdFromClaims: User ID found: {UserId}", userId);
            return userId;
        }
        _logger.LogWarning("GetCurrentUserIdFromClaims: User ID claim not found or invalid.");
        return null;
    }

    private ActionResult<ApiResponse<T>> Error<T>(string message, int statusCode, object? errorDetails = null)
    {
        return StatusCode(statusCode, new ApiResponse<T>(message, "error", statusCode, default, errorDetails));
    }

    private bool HasPermission(string permission)
    {
        return User.HasClaim("permission", permission);
    }

    private async Task<int?> ResolveBaseRoleIdAsync(string? roleName)
    {
        var roles = await _context.Roles.ToListAsync();
        var normalizedRoleName = NormalizeRoleName(roleName);
        var matchedRole = roles.FirstOrDefault(r => NormalizeRoleName(r.RoleName) == normalizedRoleName);

        if (matchedRole != null)
        {
            return matchedRole.RoleId;
        }

        return roles.FirstOrDefault(r => NormalizeRoleName(r.RoleName) == "employee")?.RoleId;
    }

    private static string NormalizeRoleName(string? roleName)
    {
        return new string((roleName ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    [HttpGet("details")]
    [Authorize(Policy = "CanEmployeeRead")] // Added permission
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CompanyUserDetailsDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<ActionResult<ApiResponse<PagedResult<CompanyUserDetailsDto>>>> GetCompanyUsers(
       [FromQuery] int page = 1,
       [FromQuery] int pageSize = 5,
       [FromQuery] string sortBy = "CreatedAt",
       [FromQuery] string sortOrder = "desc",
       [FromQuery] string? search = null,
       [FromQuery] bool? isActiveFilter = null,
       [FromQuery] int? roleIdFilter = null, // Existing: filters by CompanyRole.CompanyRoleId
       [FromQuery] int? departmentIdFilter = null, // Existing: filters by Department.DepartmentId
       [FromQuery] int? companyIdQuery = null,
       // NEW FILTERS:
       [FromQuery] string? departmentNames = null, // NEW: Filter by department names (comma-separated)
       [FromQuery] string? roleNames = null, // NEW: Filter by role names (comma-separated)
       [FromQuery] bool? isDeletedFilter = null // NEW: Filter for deleted users
       )
    {
        try
        {
            _logger.LogInformation("GetCompanyUsers: Fetching company users with filters: Page:{Page}, Size:{PageSize}, SortBy:{SortBy}, SortOrder:{SortOrder}, Search:{Search}, Active:{IsActive}, CompanyRoleId:{RoleId}, Dept:{DeptId}, CompanyQuery:{CompanyQuery}, DeptNames:{DeptNames}, RoleNames:{RoleNames}, IsDeleted:{IsDeleted}",
                                    page, pageSize, sortBy, sortOrder, search, isActiveFilter, roleIdFilter, departmentIdFilter, companyIdQuery, departmentNames, roleNames, isDeletedFilter);

            var currentUserId = GetCurrentUserIdFromClaims();
            var currentCompanyId = GetCompanyIdFromClaims();

            if (!currentUserId.HasValue || !currentCompanyId.HasValue)
            {
                _logger.LogWarning("GetCompanyUsers: Authentication claims missing. UserId: {UserIdHasValue}, CompanyId: {CompanyIdHasValue}",
                                     currentUserId.HasValue, currentCompanyId.HasValue);
                return Error<PagedResult<CompanyUserDetailsDto>>("Authentication information (UserId, CompanyId) is missing. Ensure your token is valid and contains these claims.", StatusCodes.Status401Unauthorized);
            }

            // Include CompanyRole navigation instead of Role
            IQueryable<CompanyUser> query = _context.CompanyUsers
                .Include(cu => cu.User)
                .Include(cu => cu.Department)
                .Include(cu => cu.CompanyRole);

            var canReadCompanyEmployees = HasPermission(Permissions.EmployeeReadAll);
            var targetCompanyId = companyIdQuery ?? currentCompanyId.Value;

            if (companyIdQuery.HasValue && companyIdQuery.Value != currentCompanyId.Value)
            {
                return Error<PagedResult<CompanyUserDetailsDto>>("You do not have permission to filter users for other companies.", StatusCodes.Status403Forbidden);
            }

            query = query.Where(cu => cu.CompanyId == targetCompanyId);

            if (!canReadCompanyEmployees)
            {
                query = query.Where(cu => cu.UserId == currentUserId.Value);
            }

            // Search filter (unchanged)
            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchLower = search.ToLower();
                query = query.Where(cu =>
                    (cu.FirstName != null && cu.FirstName.ToLower().Contains(searchLower)) ||
                    (cu.LastName != null && cu.LastName.ToLower().Contains(searchLower)) ||
                    (cu.EmployeeCode != null && cu.EmployeeCode.ToLower().Contains(searchLower)) ||
                    (cu.User != null && cu.User.Email != null && cu.User.Email.ToLower().Contains(searchLower)) ||
                    (cu.CompanyRole != null && cu.CompanyRole.RoleName != null && cu.CompanyRole.RoleName.ToLower().Contains(searchLower))
                );
            }

            // Active filter (unchanged)
            if (isActiveFilter.HasValue)
            {
                query = query.Where(cu => cu.IsActive == isActiveFilter.Value);
            }

            // Deleted filter
            if (isDeletedFilter.HasValue)
            {
                query = query.Where(cu => (cu.IsDeleted ?? false) == isDeletedFilter.Value);
            }
            else
            {
                // Default: Hide deleted users
                query = query.Where(cu => !(cu.IsDeleted ?? false));
            }

            // Existing role ID filter (unchanged)
            if (roleIdFilter.HasValue)
            {
                var roleExists = await _context.CompanyRoles
                    .AnyAsync(cr => cr.CompanyRoleId == roleIdFilter.Value &&
                                   cr.CompanyId == targetCompanyId &&
                                   cr.IsActive == true &&
                                   !cr.IsDeleted);

                if (!roleExists)
                {
                    return Error<PagedResult<CompanyUserDetailsDto>>("Invalid role filter: Role does not exist or does not belong to your company.", StatusCodes.Status400BadRequest);
                }

                query = query.Where(cu => cu.RoleId == roleIdFilter.Value);
            }

            // Existing department ID filter (unchanged)
            if (departmentIdFilter.HasValue)
            {
                query = query.Where(cu => cu.DepartmentId == departmentIdFilter.Value);
            }

            // NEW: Department Names Filter
            if (!string.IsNullOrWhiteSpace(departmentNames))
            {
                var departmentNamesList = departmentNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                        .Select(d => d.Trim())
                                                        .ToList();

                if (departmentNamesList.Any())
                {
                    query = query.Where(cu => cu.Department != null &&
                                             departmentNamesList.Contains(cu.Department.DepartmentName));
                }
            }

            // NEW: Role Names Filter
            if (!string.IsNullOrWhiteSpace(roleNames))
            {
                var roleNamesList = roleNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                            .Select(r => r.Trim())
                                            .ToList();

                if (roleNamesList.Any())
                {
                    query = query.Where(cu => cu.CompanyRole != null &&
                                             roleNamesList.Contains(cu.CompanyRole.RoleName));
                }
            }

            var totalUsers = await query.CountAsync();

            // Sorting (unchanged)
            var userPropertyMap = new Dictionary<string, Expression<Func<CompanyUser, object>>>
        {
            { "userid", cu => cu.UserId },
            { "employeeid", cu => cu.EmployeeId },
            { "employeecode", cu => cu.EmployeeCode! },
            { "firstname", cu => cu.FirstName! },
            { "lastname", cu => cu.LastName! },
            { "useremail", cu => cu.User!.Email! },
            { "roleid", cu => cu.RoleId },
            { "rolename", cu => cu.CompanyRole!.RoleName! },
            { "departmentid", cu => cu.DepartmentId! },
            { "departmentname", cu => cu.Department!.DepartmentName! },
            { "isactive", cu => cu.IsActive },
            { "joiningdate", cu => cu.JoiningDate },
            { "createdat", cu => cu.CreatedAt }
        };

            if (userPropertyMap.TryGetValue(sortBy.ToLower(), out var sortExpression))
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
                _logger.LogWarning("GetCompanyUsers: Invalid sortBy parameter '{SortBy}'. Defaulting to CreatedAt descending.", sortBy);
                query = query.OrderByDescending(cu => cu.CreatedAt);
            }

            // Execute query and map to DTO (unchanged)
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(cu => new CompanyUserDetailsDto
                {
                    // ... your existing DTO mapping remains the same
                    UserId = cu.UserId,
                    CompanyEmployeeId = cu.EmployeeId,
                    RoleId = cu.RoleId,
                    CompanyId = cu.CompanyId,
                    DepartmentId = cu.DepartmentId,
                    IsActive = cu.IsActive,
                    EmploymentType = cu.EmploymentType,
                    Address = cu.Address,
                    AddressLine1 = cu.AddressLine1,
                    City = cu.City,
                    State = cu.State,
                    PostalCode = cu.PostalCode,
                    Country = cu.Country,
                    Gender = cu.Gender,
                    BloodGroup = cu.BloodGroup,
                    PhoneNumber = cu.PhoneNumber,
                    DateOfBirth = cu.DateOfBirth,
                    MaritalStatus = cu.MaritalStatus,
                    JoiningDate = cu.JoiningDate,
                    EmployeeCode = cu.EmployeeCode,
                    ProfilePhotoUrl = cu.ProfilePhotoUrl,
                    Description = cu.Description,
                    IsDeleted = cu.IsDeleted,
                    CreatedAt = cu.CreatedAt,
                    CreatedBy = cu.CreatedBy,
                    UpdatedAt = cu.UpdatedAt,
                    UpdatedBy = cu.UpdatedBy,
                    CheckAdmin = cu.CheckAdmin,
                    FirstName = cu.FirstName,
                    LastName = cu.LastName,
                    AnniversaryDate = cu.AnniversaryDate,
                    ReportToUserId = cu.ReportToUserId,
                    EmergencyContactName = cu.EmergencyContactName,
                    EmergencyContactRelation = cu.EmergencyContactRelation,
                    EmergencyContactNumber = cu.EmergencyContactNumber,
                    UserEmail = cu.User != null ? cu.User.Email : string.Empty,
                    UserIsActive = cu.User != null ? cu.User.IsActive : false,
                    UserIsDeleted = cu.User != null ? cu.User.IsDeleted : false,
                    UserCreatedAt = cu.User != null ? cu.User.CreatedAt : (DateTime?)null,
                    UserUpdatedAt = cu.User != null ? cu.User.UpdatedAt : (DateTime?)null,
                    UserUpdatedBy = cu.User != null ? cu.User.UpdatedBy : (int?)null,
                    UserRoleId = cu.User != null ? cu.User.RoleId : (int?)null,
                    UserCompanyId = cu.User != null ? cu.User.CompanyId : (int?)null,
                    DepartmentName = cu.Department != null ? cu.Department.DepartmentName : null,
                    // ADD: Role name to DTO if not already present
                    RoleName = cu.CompanyRole != null ? cu.CompanyRole.RoleName : null,
                    ReportToUserName = _context.CompanyUsers
        .Where(r => r.EmployeeId == cu.ReportToUserId && r.CompanyId == cu.CompanyId) // Changed UserId -> EmployeeId
        .Select(r => r.FirstName + " " + r.LastName)
        .FirstOrDefault()
                })
                .ToListAsync();

            int totalPages = (int)Math.Ceiling((double)totalUsers / pageSize);

            var pagedResult = new PagedResult<CompanyUserDetailsDto>
            {
                Items = items,
                TotalCount = totalUsers,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalTasks = totalUsers,
                SortBy = sortBy,
                SortOrder = sortOrder,
                SearchKeyword = search
            };

            return Ok(new ApiResponse<PagedResult<CompanyUserDetailsDto>>("Company users fetched successfully.", "success", StatusCodes.Status200OK, pagedResult));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching company users.");
            return Error<PagedResult<CompanyUserDetailsDto>>("An error occurred while retrieving company user details.", StatusCodes.Status500InternalServerError, new { ExceptionMessage = ex.Message, InnerExceptionMessage = ex.InnerException?.Message });
        }
    }


    [HttpGet("details/user/{userId}")]
    [Authorize(Policy = "CanEmployeeRead")] // Added permission
    [ProducesResponseType(typeof(ApiResponse<CompanyUserDetailsDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<ActionResult<ApiResponse<CompanyUserDetailsDto>>> GetCompanyUserDetailsByUserId(int userId)
    {
        try
        {
            int? currentCompanyId = GetCompanyIdFromClaims();
            if (!currentCompanyId.HasValue)
            {
                return Unauthorized(new ApiResponse<CompanyUserDetailsDto>("Company ID claim missing or invalid.", "error", StatusCodes.Status401Unauthorized));
            }

            int? currentUserId = GetCurrentUserIdFromClaims();
            if (!currentUserId.HasValue)
            {
                return Unauthorized(new ApiResponse<CompanyUserDetailsDto>("User ID claim missing or invalid.", "error", StatusCodes.Status401Unauthorized));
            }

            if (!HasPermission(Permissions.EmployeeReadAll) && userId != currentUserId.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new ApiResponse<CompanyUserDetailsDto>("You do not have permission to view this employee.", "error", StatusCodes.Status403Forbidden));
            }

            _logger.LogInformation("GetCompanyUserDetailsByUserId: Filtering by CompanyId: {CompanyId} and UserId: {UserId}", currentCompanyId.Value, userId);

            var companyUser = await _context.CompanyUsers
                                            .Include(cu => cu.User)
                                            .Include(cu => cu.Department)
                                            .Include(cu => cu.CompanyRole) // NEW: Include CompanyRole navigation
                                            .Where(cu => cu.UserId == userId && !(cu.IsDeleted ?? false))
                                            .Where(cu => cu.CompanyId == currentCompanyId.Value)
                                            .Select(cu => new CompanyUserDetailsDto
                                            {
                                                UserId = cu.UserId,
                                                CompanyEmployeeId = cu.EmployeeId,
                                                RoleId = cu.RoleId, // Now represents CompanyRole.CompanyRoleId
                                                CompanyId = cu.CompanyId,
                                                DepartmentId = cu.DepartmentId,
                                                IsActive = cu.IsActive,
                                                EmploymentType = cu.EmploymentType,
                                                Address = cu.Address,
                                                AddressLine1 = cu.AddressLine1,
                                                City = cu.City,
                                                State = cu.State,
                                                PostalCode = cu.PostalCode,
                                                Country = cu.Country,
                                                Gender = cu.Gender,
                                                BloodGroup = cu.BloodGroup,
                                                PhoneNumber = cu.PhoneNumber,
                                                DateOfBirth = cu.DateOfBirth,
                                                MaritalStatus = cu.MaritalStatus,
                                                JoiningDate = cu.JoiningDate,
                                                EmployeeCode = cu.EmployeeCode,
                                                ProfilePhotoUrl = cu.ProfilePhotoUrl,
                                                Description = cu.Description,
                                                IsDeleted = cu.IsDeleted,
                                                CreatedAt = cu.CreatedAt,
                                                CreatedBy = cu.CreatedBy,
                                                UpdatedAt = cu.UpdatedAt,
                                                UpdatedBy = cu.UpdatedBy,
                                                CheckAdmin = cu.CheckAdmin,
                                                FirstName = cu.FirstName,
                                                LastName = cu.LastName,
                                                AnniversaryDate = cu.AnniversaryDate,
                                                ReportToUserId = cu.ReportToUserId,
                                                EmergencyContactName = cu.EmergencyContactName,
                                                EmergencyContactRelation = cu.EmergencyContactRelation,
                                                EmergencyContactNumber = cu.EmergencyContactNumber,
                                                UserEmail = cu.User != null ? cu.User.Email : string.Empty,
                                                UserIsActive = cu.User != null ? cu.User.IsActive : false,
                                                UserIsDeleted = cu.User != null ? cu.User.IsDeleted : false,
                                                UserCreatedAt = cu.User != null ? cu.User.CreatedAt : (DateTime?)null,
                                                UserUpdatedAt = cu.User != null ? cu.User.UpdatedAt : (DateTime?)null,
                                                UserUpdatedBy = cu.User != null ? cu.User.UpdatedBy : (int?)null,
                                                UserRoleId = cu.User != null ? cu.User.RoleId : (int?)null,
                                                UserCompanyId = cu.User != null ? cu.User.CompanyId : (int?)null,
                                                DepartmentName = cu.Department != null ? cu.Department.DepartmentName : null,
                                                ReportToUserName = (from cuReportTo in _context.CompanyUsers
                                                                    where cuReportTo.EmployeeId == cu.ReportToUserId &&
                                                                          cuReportTo.CompanyId == cu.CompanyId &&
                                                                          !(cuReportTo.IsDeleted ?? false)
                                                                    select (cuReportTo.FirstName != null && cuReportTo.LastName != null)
                                                                        ? cuReportTo.FirstName + " " + cuReportTo.LastName
                                                                        : cuReportTo.FirstName ?? cuReportTo.LastName)
                                                .FirstOrDefault(),
                                            })
                                            .FirstOrDefaultAsync();

            if (companyUser == null)
            {
                _logger.LogInformation("Company user with UserId {UserId} not found for CompanyId {CompanyId} (or is deleted).", userId, currentCompanyId.Value);
                return NotFound(new ApiResponse<CompanyUserDetailsDto>($"Company user with UserId {userId} not found.", "error", StatusCodes.Status404NotFound));
            }

            return Ok(new ApiResponse<CompanyUserDetailsDto>("Company user details fetched successfully.", "success", StatusCodes.Status200OK, companyUser));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company user with UserId: {UserId}", userId);
            return StatusCode(500, new ApiResponse<CompanyUserDetailsDto>("An error occurred while retrieving company user details.", "error", StatusCodes.Status500InternalServerError, null, new { ExceptionMessage = ex.Message, InnerExceptionMessage = ex.InnerException?.Message }));
        }
    }

    [HttpPost]
    [Authorize(Policy = "CanEmployeeCreate")]
    [ProducesResponseType(typeof(ApiResponse<CompanyUserDetailsDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    [ProducesResponseType(typeof(ApiResponse<object>), 409)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<ActionResult<ApiResponse<CompanyUserDetailsDto>>> CreateCompanyUser([FromBody] CompanyUserCreateDto createDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponse<object>("Validation failed.", "error", StatusCodes.Status400BadRequest, null, ModelState));
        }

        // Get current user info for authorization and audit purposes
        int? createdByUserId = GetCurrentUserIdFromClaims();
        int? currentUserCompanyId = GetCompanyIdFromClaims();

        if (!createdByUserId.HasValue || !currentUserCompanyId.HasValue)
        {
            return Unauthorized(new ApiResponse<object>("User ID or Company ID claim missing.", "error", StatusCodes.Status401Unauthorized));
        }

        if (createDto.CompanyId != currentUserCompanyId.Value && currentUserCompanyId.Value != 0)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiResponse<object>("Company mismatch.", "error", StatusCodes.Status403Forbidden));
        }

        // Validate that the specified company exists
        var companyExists = await _context.Companies
            .AnyAsync(c => c.CompanyId == createDto.CompanyId && c.IsActive == true && c.IsDeleted == false);

        if (!companyExists)
        {
            return BadRequest(new ApiResponse<object>($"Company {createDto.CompanyId} not found.", "error", StatusCodes.Status400BadRequest));
        }

        // VALIDATION: Check if the CompanyRole exists and get the corresponding global role
        var companyRole = await _context.CompanyRoles
            .Where(cr => cr.CompanyRoleId == createDto.companyRoleId &&
                         cr.CompanyId == createDto.CompanyId &&
                         cr.IsActive == true && !cr.IsDeleted)
            .FirstOrDefaultAsync();

        if (companyRole == null)
        {
            return BadRequest(new ApiResponse<object>($"Invalid Company Role ID {createDto.companyRoleId}.", "error", StatusCodes.Status400BadRequest));
        }

        var globalRoleId = await ResolveBaseRoleIdAsync(companyRole.DefaultRole ?? companyRole.RoleName);

        if (createDto.ReportToUserId.HasValue)
        {
            var managerAsEmployee = await _context.CompanyUsers
                .AnyAsync(cu => cu.EmployeeId == createDto.ReportToUserId.Value &&
                                cu.CompanyId == createDto.CompanyId &&
                                !(cu.IsDeleted ?? false));

            if (managerAsEmployee)
            {
                // It is a valid EmployeeId. Do nothing (keep the value as is).
            }
            else
            {
                var managerAsUser = await _context.CompanyUsers
                    .FirstOrDefaultAsync(cu => cu.UserId == createDto.ReportToUserId.Value &&
                                               cu.CompanyId == createDto.CompanyId &&
                                               !(cu.IsDeleted ?? false));

                if (managerAsUser != null)
                {
                    // Found as a User ID! Translate it to the correct EmployeeId.
                    createDto.ReportToUserId = managerAsUser.EmployeeId;
                }
                else
                {
                    // 3. Not found as either
                    return BadRequest(new ApiResponse<object>(
                        $"The 'Reports To' user (ID: {createDto.ReportToUserId.Value}) could not be found as either an Employee ID or User ID in this company.",
                        "error",
                        StatusCodes.Status400BadRequest));
                }
            }
        }

        if (createDto.DepartmentId.HasValue)
        {
            var departmentExists = await _context.Departments
                .AnyAsync(d => d.DeptId == createDto.DepartmentId.Value &&
                              d.CompanyId == createDto.CompanyId &&
                              !(d.IsDeleted ?? false));

            if (!departmentExists)
            {
                return BadRequest(new ApiResponse<object>($"Department {createDto.DepartmentId.Value} not found.", "error", StatusCodes.Status400BadRequest));
            }
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 8. Create User
            if (await _context.Users.AnyAsync(u => u.Email == createDto.UserEmail && !u.IsDeleted))
            {
                return Conflict(new ApiResponse<object>("Email already exists.", "error", StatusCodes.Status409Conflict));
            }

            string plainTextPassword = GenerateRandomPassword(10);
            string hashedPassword = _passwordHasher.HashPassword(null, plainTextPassword);

            // FIX FOR COMPILATION ERRORS: Removed "Reports" logic entirely
            var newUser = new User
            {
                Email = createDto.UserEmail,
                PasswordHash = hashedPassword,
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = createdByUserId,
                RoleId = globalRoleId,
                CompanyId = createDto.CompanyId
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // 9. Create CompanyUser
            string newEmployeeCode = $"EMP_{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
            while (await _context.CompanyUsers.AnyAsync(cu => cu.EmployeeCode == newEmployeeCode))
            {
                newEmployeeCode = $"EMP_{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
            }

            var companyUser = new CompanyUser
            {
                UserId = newUser.UserId,
                RoleId = createDto.companyRoleId,
                CompanyId = createDto.CompanyId,
                DepartmentId = createDto.DepartmentId,
                IsActive = true,
                EmploymentType = createDto.EmploymentType,
                Address = createDto.Address,
                AddressLine1 = createDto.AddressLine1,
                City = createDto.City,
                State = createDto.State,
                PostalCode = createDto.PostalCode,
                Country = createDto.Country,
                Gender = createDto.Gender,
                BloodGroup = createDto.BloodGroup,
                PhoneNumber = createDto.PhoneNumber,
                DateOfBirth = createDto.DateOfBirth,
                MaritalStatus = createDto.MaritalStatus,
                JoiningDate = createDto.JoiningDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                EmployeeCode = newEmployeeCode,
                ProfilePhotoUrl = createDto.ProfilePhotoUrl,
                Description = createDto.Description,
                CheckAdmin = createDto.CheckAdmin,
                FirstName = createDto.FirstName,
                LastName = createDto.LastName,
                AnniversaryDate = createDto.AnniversaryDate,
                ReportToUserId = createDto.ReportToUserId, // This is now guaranteed to be an EmployeeID (e.g., 84)
                EmergencyContactName = createDto.EmergencyContactName,
                EmergencyContactRelation = createDto.EmergencyContactRelation,
                EmergencyContactNumber = createDto.EmergencyContactNumber,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdByUserId.Value
            };

            _context.CompanyUsers.Add(companyUser);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            // 10. Send Email
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    string subject = "Welcome to Amplica!";
                    string body = _emailTemplateService.GetWelcomeEmailBody(
                        createDto.FirstName, createDto.LastName ?? "", createDto.UserEmail, plainTextPassword);
                    await _emailSender.SendEmailAsync(createDto.UserEmail, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send welcome email.");
                }
            });

            // 11. Return Response
            var createdUserDetailDto = await _context.CompanyUsers
                .Include(cu => cu.User)
                .Include(cu => cu.Department)
                .Include(cu => cu.CompanyRole)
                .Where(cu => cu.EmployeeId == companyUser.EmployeeId)
                .Select(cu => new CompanyUserDetailsDto
                {
                    UserId = cu.UserId,
                    CompanyEmployeeId = cu.EmployeeId,
                    RoleId = cu.RoleId,
                    CompanyId = cu.CompanyId,
                    DepartmentId = cu.DepartmentId,
                    IsActive = cu.IsActive,
                    EmploymentType = cu.EmploymentType,
                    FirstName = cu.FirstName,
                    LastName = cu.LastName,
                    UserEmail = cu.User != null ? cu.User.Email : "",
                    DepartmentName = cu.Department != null ? cu.Department.DepartmentName : null,
                    RoleName = cu.CompanyRole != null ? cu.CompanyRole.RoleName : null,
                    // Add any other specific fields you need returned to the frontend
                })
                .FirstOrDefaultAsync();

            return CreatedAtAction(nameof(GetCompanyUserDetailsByUserId), new { userId = createdUserDetailDto.UserId },
                new ApiResponse<CompanyUserDetailsDto>("Created successfully.", "success", StatusCodes.Status201Created, createdUserDetailDto));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating user.");
            return Error<CompanyUserDetailsDto>($"Error: {ex.Message}", StatusCodes.Status500InternalServerError);
        }
    }

    // --- Private Helper for Password Generation ---
    private string GenerateRandomPassword(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()";
        Random random = new Random();
        StringBuilder password = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            password.Append(chars[random.Next(chars.Length)]);
        }
        return password.ToString();
    }

    [HttpPut("user/{userId}")]
    [Authorize(Policy = "CanEmployeeUpdate")] // Added permission
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UpdateCompanyUser(int userId, [FromBody] CompanyUserUpdateDto updateDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ApiResponse<object>("Validation failed.", "error", StatusCodes.Status400BadRequest, null, ModelState));
        }

        try
        {
            int? currentCompanyId = GetCompanyIdFromClaims();
            if (!currentCompanyId.HasValue)
                return Unauthorized(new ApiResponse<object>("Company ID claim missing or invalid.", "error", StatusCodes.Status401Unauthorized));

            _logger.LogInformation("UpdateCompanyUser: Filtering by CompanyId: {CompanyId} and UserId: {UserId}", currentCompanyId.Value, userId);

            var companyUser = await _context.CompanyUsers
                                            .Include(cu => cu.User)
                                            .FirstOrDefaultAsync(cu => cu.UserId == userId && cu.CompanyId == currentCompanyId.Value && !(cu.IsDeleted ?? false));

            if (companyUser == null)
            {
                _logger.LogInformation("Company user with UserId {UserId} not found for CompanyId {CompanyId} (or is deleted).", userId, currentCompanyId.Value);
                return NotFound(new ApiResponse<object>($"Company user with UserId {userId} not found or is deleted within your company.", "error", StatusCodes.Status404NotFound));
            }

            int? updatedByUserId = GetCurrentUserIdFromClaims();
            if (!updatedByUserId.HasValue)
            {
                return Unauthorized(new ApiResponse<object>("User ID claim missing or invalid in token.", "error", StatusCodes.Status401Unauthorized));
            }

            // Validate companyRoleId if provided
            if (updateDto.companyRoleId.HasValue)
            {
                var roleExists = await _context.CompanyRoles
                    .AnyAsync(cr => cr.CompanyRoleId == updateDto.companyRoleId.Value &&
                                   cr.CompanyId == currentCompanyId.Value &&
                                   cr.IsActive == true &&
                                   !cr.IsDeleted);

                if (!roleExists)
                {
                    return BadRequest(new ApiResponse<object>(
                        $"Company role with ID {updateDto.companyRoleId.Value} does not exist or is not active in your company.",
                        "error", StatusCodes.Status400BadRequest));
                }
            }

            // Validate DepartmentId if provided
            if (updateDto.DepartmentId.HasValue)
            {
                var departmentExists = await _context.Departments
                    .AnyAsync(d => d.DeptId == updateDto.DepartmentId.Value &&
                                  d.CompanyId == currentCompanyId.Value &&
                                  (d.IsDeleted == false || d.IsDeleted == null));

                if (!departmentExists)
                {
                    return BadRequest(new ApiResponse<object>(
                        $"Department with ID {updateDto.DepartmentId.Value} does not exist in your company.",
                        "error", StatusCodes.Status400BadRequest));
                }
            }

            // Email update logic
            if (!string.IsNullOrWhiteSpace(updateDto.UserEmail) && companyUser.User != null && companyUser.User.Email != updateDto.UserEmail)
            {
                if (await _context.Users.AnyAsync(u => u.Email == updateDto.UserEmail && u.UserId != userId && !(u.IsDeleted)))
                {
                    return Conflict(new ApiResponse<object>("Another user with this email already exists.", "error", StatusCodes.Status409Conflict));
                }
                companyUser.User.Email = updateDto.UserEmail;
                companyUser.User.UpdatedAt = DateTime.UtcNow;
                companyUser.User.UpdatedBy = updatedByUserId;
            }

            // Update fields - CONDITIONAL UPDATES to preserve existing values
            if (updateDto.companyRoleId.HasValue)
                companyUser.RoleId = updateDto.companyRoleId.Value;

            if (updateDto.DepartmentId.HasValue)
                companyUser.DepartmentId = updateDto.DepartmentId.Value;

            if (updateDto.IsActive.HasValue)
                companyUser.IsActive = updateDto.IsActive.Value;

            if (!string.IsNullOrWhiteSpace(updateDto.EmploymentType))
                companyUser.EmploymentType = updateDto.EmploymentType;

            if (!string.IsNullOrWhiteSpace(updateDto.Address))
                companyUser.Address = updateDto.Address;

            if (!string.IsNullOrWhiteSpace(updateDto.AddressLine1))
                companyUser.AddressLine1 = updateDto.AddressLine1;

            if (!string.IsNullOrWhiteSpace(updateDto.City))
                companyUser.City = updateDto.City;

            if (!string.IsNullOrWhiteSpace(updateDto.State))
                companyUser.State = updateDto.State;

            if (!string.IsNullOrWhiteSpace(updateDto.PostalCode))
                companyUser.PostalCode = updateDto.PostalCode;

            if (!string.IsNullOrWhiteSpace(updateDto.Country))
                companyUser.Country = updateDto.Country;

            if (!string.IsNullOrWhiteSpace(updateDto.Gender))
                companyUser.Gender = updateDto.Gender;

            if (!string.IsNullOrWhiteSpace(updateDto.BloodGroup))
                companyUser.BloodGroup = updateDto.BloodGroup;

            if (!string.IsNullOrWhiteSpace(updateDto.PhoneNumber))
                companyUser.PhoneNumber = updateDto.PhoneNumber;

           
                companyUser.DateOfBirth = updateDto.DateOfBirth;

            if (!string.IsNullOrWhiteSpace(updateDto.MaritalStatus))
                companyUser.MaritalStatus = updateDto.MaritalStatus;

            // PROFILE PHOTO: Only update if explicitly provided
            if (updateDto.ProfilePhotoUrl != null)
            {
                companyUser.ProfilePhotoUrl = updateDto.ProfilePhotoUrl;
                _logger.LogInformation("Profile photo updated for user {UserId}: {ProfilePhotoUrl}", userId, updateDto.ProfilePhotoUrl);
            }

            if (!string.IsNullOrWhiteSpace(updateDto.Description))
                companyUser.Description = updateDto.Description;

            if (updateDto.CheckAdmin.HasValue)
                companyUser.CheckAdmin = updateDto.CheckAdmin;

            if (!string.IsNullOrWhiteSpace(updateDto.FirstName))
                companyUser.FirstName = updateDto.FirstName;

            if (!string.IsNullOrWhiteSpace(updateDto.LastName))
                companyUser.LastName = updateDto.LastName;

        
                companyUser.AnniversaryDate = updateDto.AnniversaryDate;

            if (updateDto.JoiningDate.HasValue)
                companyUser.JoiningDate = updateDto.JoiningDate;

            if (updateDto.ReportToUserId.HasValue)
            {
                // Prevent setting self as manager
                if (updateDto.ReportToUserId == userId)
                {
                    return BadRequest(new ApiResponse<object>("A user cannot report to themselves.", "error", StatusCodes.Status400BadRequest));
                }

                // ReportToUserId accepts either an EmployeeId or a UserId - resolve to EmployeeId
                var reportToAsEmployee = await _context.CompanyUsers
                    .AnyAsync(cu => cu.EmployeeId == updateDto.ReportToUserId.Value &&
                                    cu.CompanyId == currentCompanyId.Value &&
                                    !(cu.IsDeleted ?? false));

                if (reportToAsEmployee)
                {
                    // It's a valid EmployeeId, use as-is
                    companyUser.ReportToUserId = updateDto.ReportToUserId;
                }
                else
                {
                    // Try resolving as a UserId -> translate to EmployeeId
                    var reportToAsUser = await _context.CompanyUsers
                        .FirstOrDefaultAsync(cu => cu.UserId == updateDto.ReportToUserId.Value &&
                                                   cu.CompanyId == currentCompanyId.Value &&
                                                   !(cu.IsDeleted ?? false));

                    if (reportToAsUser != null)
                    {
                        companyUser.ReportToUserId = reportToAsUser.EmployeeId;
                    }
                    else
                    {
                        return BadRequest(new ApiResponse<object>(
                            $"The 'Reports To' user (ID: {updateDto.ReportToUserId.Value}) could not be found as either an Employee ID or User ID in this company.",
                            "error", StatusCodes.Status400BadRequest));
                    }
                }
            }

            // Emergency Contact Details - conditional updates
            if (!string.IsNullOrWhiteSpace(updateDto.EmergencyContactName))
                companyUser.EmergencyContactName = updateDto.EmergencyContactName;

            if (!string.IsNullOrWhiteSpace(updateDto.EmergencyContactRelation))
                companyUser.EmergencyContactRelation = updateDto.EmergencyContactRelation;

            if (!string.IsNullOrWhiteSpace(updateDto.EmergencyContactNumber))
                companyUser.EmergencyContactNumber = updateDto.EmergencyContactNumber;

            companyUser.UpdatedAt = DateTime.UtcNow;
            companyUser.UpdatedBy = updatedByUserId.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Company user with UserId {UserId} updated successfully", userId);

            // Re-fetch the updated record to return in the response
            var updatedUser = await _context.CompanyUsers
                .Include(cu => cu.User)
                .Include(cu => cu.Department)
                .Include(cu => cu.CompanyRole)
                .Where(cu => cu.UserId == userId && cu.CompanyId == currentCompanyId.Value)
                .Select(cu => new CompanyUserDetailsDto
                {
                    UserId = cu.UserId,
                    CompanyEmployeeId = cu.EmployeeId,
                    RoleId = cu.RoleId,
                    CompanyId = cu.CompanyId,
                    DepartmentId = cu.DepartmentId,
                    IsActive = cu.IsActive,
                    EmploymentType = cu.EmploymentType,
                    Address = cu.Address,
                    AddressLine1 = cu.AddressLine1,
                    City = cu.City,
                    State = cu.State,
                    PostalCode = cu.PostalCode,
                    Country = cu.Country,
                    Gender = cu.Gender,
                    BloodGroup = cu.BloodGroup,
                    PhoneNumber = cu.PhoneNumber,
                    DateOfBirth = cu.DateOfBirth,
                    MaritalStatus = cu.MaritalStatus,
                    JoiningDate = cu.JoiningDate,
                    EmployeeCode = cu.EmployeeCode,
                    ProfilePhotoUrl = cu.ProfilePhotoUrl,
                    Description = cu.Description,
                    IsDeleted = cu.IsDeleted,
                    CreatedAt = cu.CreatedAt,
                    CreatedBy = cu.CreatedBy,
                    UpdatedAt = cu.UpdatedAt,
                    UpdatedBy = cu.UpdatedBy,
                    CheckAdmin = cu.CheckAdmin,
                    FirstName = cu.FirstName,
                    LastName = cu.LastName,
                    AnniversaryDate = cu.AnniversaryDate,
                    ReportToUserId = cu.ReportToUserId,
                    EmergencyContactName = cu.EmergencyContactName,
                    EmergencyContactRelation = cu.EmergencyContactRelation,
                    EmergencyContactNumber = cu.EmergencyContactNumber,
                    UserEmail = cu.User != null ? cu.User.Email : string.Empty,
                    UserIsActive = cu.User != null ? cu.User.IsActive : false,
                    DepartmentName = cu.Department != null ? cu.Department.DepartmentName : null,
                    RoleName = cu.CompanyRole != null ? cu.CompanyRole.RoleName : null,
                })
                .FirstOrDefaultAsync();

            return Ok(new ApiResponse<CompanyUserDetailsDto>("Company user updated successfully.", "success", StatusCodes.Status200OK, updatedUser));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating company user with UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ApiResponse<object>("An error occurred while updating the company user.", "error",
                    StatusCodes.Status500InternalServerError, null,
                    new { ExceptionMessage = ex.Message, InnerExceptionMessage = ex.InnerException?.Message }));
        }
    }


    [HttpDelete("user/{userId}")]
    [Authorize(Policy = "CanEmployeeDelete")] // Added permission
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> SoftDeleteCompanyUser(int userId)
    {
        try
        {
            int? currentCompanyId = GetCompanyIdFromClaims();
            if (!currentCompanyId.HasValue)
            {
                return Unauthorized(new ApiResponse<object>("Company ID claim missing or invalid.", "error", StatusCodes.Status401Unauthorized));
            }

            _logger.LogInformation("SoftDeleteCompanyUser: Filtering by CompanyId: {CompanyId} and UserId: {UserId}", currentCompanyId.Value, userId);

            var companyUser = await _context.CompanyUsers
                                            .FirstOrDefaultAsync(cu => cu.UserId == userId && cu.CompanyId == currentCompanyId.Value && !(cu.IsDeleted ?? false));

            if (companyUser == null)
            {
                _logger.LogInformation("Company user with UserId {UserId} not found for CompanyId {CompanyId} (or is already deleted).", userId, currentCompanyId.Value);
                return NotFound(new ApiResponse<object>($"Company user with UserId {userId} not found or already deleted within your company.", "error", StatusCodes.Status404NotFound));
            }

            int? updatedByUserId = GetCurrentUserIdFromClaims();
            if (!updatedByUserId.HasValue)
            {
                return Unauthorized(new ApiResponse<object>("User ID claim missing or invalid in token.", "error", StatusCodes.Status401Unauthorized));
            }

            companyUser.IsDeleted = true;
            companyUser.UpdatedAt = DateTime.UtcNow;
            companyUser.UpdatedBy = updatedByUserId.Value;

            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft deleting company user with UserId: {UserId}", userId);
            return StatusCode(500, new ApiResponse<object>("An error occurred while soft deleting the company user.", "error", StatusCodes.Status500InternalServerError, null, new { ExceptionMessage = ex.Message, InnerExceptionMessage = ex.InnerException?.Message }));
        }
    }

    [HttpPost("upload-profile-photo/{userId}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UploadProfilePhoto(int userId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided.");
        }

        try
        {
            var url = await _uploadHandler.UploadProfilePhotoAsync(userId, file);
            return Ok(new { profilePhotoUrl = url });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading profile photo for UserId: {UserId}", userId);
            return StatusCode(500, "An error occurred while uploading the profile photo.");
        }
    }



// delete upload - profile photo endpoint

[HttpDelete("profile-photo/{userId}")]
[Authorize]
[ProducesResponseType(typeof(object), 200)]
[ProducesResponseType(403)]
[ProducesResponseType(404)]
[ProducesResponseType(500)]
public async Task<IActionResult> DeleteProfilePhoto(int userId)
{
    var currentUserId = GetCurrentUserIdFromClaims();

    // User can delete only their own profile photo
    if (currentUserId != userId)
    {
        return Forbid();
    }

    try
    {
        await _uploadHandler.DeleteProfilePhotoAsync(userId);

        return Ok(new
        {
            message = "Profile photo deleted successfully."
        });
    }
    catch (InvalidOperationException ex)
    {
        return NotFound(ex.Message);
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Error deleting profile photo for UserId: {UserId}",
            userId);

        return StatusCode(
            500,
            "An error occurred while deleting the profile photo.");
    }
}






    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN PASSWORD CHANGE ENDPOINTS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Company Admin changes the password of a company employee (user).
    /// The caller must have the "employee:change-password" permission.
    /// The target user must belong to the same company as the caller.
    /// A Company Admin cannot reset another Company Admin's password — only the Super Admin can do that.
    /// </summary>
    [HttpPut("user/{userId}/change-password")]
    [Authorize(Policy = "CanEmployeeChangePassword")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> ChangeEmployeePassword(int userId, [FromBody] BizfreeApp.Models.DTOs.AdminChangePasswordRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponse<object>("Validation failed.", "error", StatusCodes.Status400BadRequest, null, ModelState));

            // --- 1. Extract caller identity ---
            var callerCompanyId = GetCompanyIdFromClaims();
            var callerUserId = GetCurrentUserIdFromClaims();

            if (!callerCompanyId.HasValue || !callerUserId.HasValue)
                return Unauthorized(new ApiResponse<object>("Authentication information is missing.", "error", StatusCodes.Status401Unauthorized));

            // --- 2. Validate passwords match ---
            if (request.NewPassword != request.ConfirmPassword)
                return BadRequest(new ApiResponse<object>("New password and confirm password do not match.", "error", StatusCodes.Status400BadRequest));

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                return BadRequest(new ApiResponse<object>("Password must be at least 6 characters long.", "error", StatusCodes.Status400BadRequest));

            // --- 3. Load the target user's CompanyUser record ---
            var targetCompanyUser = await _context.CompanyUsers
                .Include(cu => cu.User)
                .FirstOrDefaultAsync(cu =>
                    cu.UserId == userId &&
                    cu.CompanyId == callerCompanyId.Value &&
                    !(cu.IsDeleted ?? false));

            if (targetCompanyUser == null || targetCompanyUser.User == null)
                return NotFound(new ApiResponse<object>($"User with ID {userId} not found in your company.", "error", StatusCodes.Status404NotFound));

            // --- 4. Prevent changing another company admin's password from this endpoint ---
            // CheckAdmin != null means the user has an admin-level role (Company Admin)
            if (targetCompanyUser.CheckAdmin != null && targetCompanyUser.UserId != callerUserId.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new ApiResponse<object>(
                        "You cannot change a Company Admin's password. Only a Super Admin can do that.",
                        "error",
                        StatusCodes.Status403Forbidden));
            }

            // --- 5. Prevent changing own password via this endpoint (use /auth/change-password for that) ---
            if (targetCompanyUser.UserId == callerUserId.Value)
                return BadRequest(new ApiResponse<object>("To change your own password, use the /auth/change-password endpoint.", "error", StatusCodes.Status400BadRequest));

            // --- 6. Hash and save the new password ---
            var targetUser = targetCompanyUser.User;
            targetUser.PasswordHash = _passwordHasher.HashPassword(targetUser, request.NewPassword);
            targetUser.UpdatedAt = DateTime.UtcNow;
            targetUser.UpdatedBy = callerUserId.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Company Admin (UserId={CallerUserId}, CompanyId={CompanyId}) changed password for UserId={TargetUserId}",
                callerUserId.Value, callerCompanyId.Value, userId);

            // --- 7. Send notification email (fire-and-forget, non-blocking) ---
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    string targetName = targetCompanyUser.FirstName ?? "User";
                    string emailBody = _emailTemplateService.GetPasswordChangedConfirmationEmailBody(targetName);
                    await _emailSender.SendEmailAsync(targetUser.Email!, "Your Password Has Been Changed by Admin", emailBody);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send password-changed notification email to UserId={TargetUserId}", userId);
                }
            });

            return Ok(new ApiResponse<object>("Employee password changed successfully.", "success", StatusCodes.Status200OK));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for UserId={TargetUserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ApiResponse<object>("An error occurred while changing the employee's password.", "error", StatusCodes.Status500InternalServerError, null, new { ExceptionMessage = ex.Message }));
        }
    }

    /// <summary>
    /// Super Admin changes the password of a Company Admin.
    /// The caller must have the "company-admin:change-password" permission AND CompanyId == 0 (Super Admin).
    /// This endpoint has no company boundary — the Super Admin can target any user.
    /// </summary>
    [HttpPut("admin/{userId}/change-password")]
    [Authorize(Policy = "CanCompanyAdminChangePassword")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 403)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> ChangeSuperAdminTargetPassword(int userId, [FromBody] BizfreeApp.Models.DTOs.AdminChangePasswordRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new ApiResponse<object>("Validation failed.", "error", StatusCodes.Status400BadRequest, null, ModelState));

            // --- 1. Extract caller identity and enforce Super Admin only ---
            var callerCompanyId = GetCompanyIdFromClaims();
            var callerUserId = GetCurrentUserIdFromClaims();

            if (!callerCompanyId.HasValue || !callerUserId.HasValue)
                return Unauthorized(new ApiResponse<object>("Authentication information is missing.", "error", StatusCodes.Status401Unauthorized));

            // Only Super Admin (CompanyId == 0) can call this endpoint
            if (callerCompanyId.Value != 0)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new ApiResponse<object>(
                        "Access denied. Only a Super Admin can use this endpoint.",
                        "error",
                        StatusCodes.Status403Forbidden));
            }

            // --- 2. Validate passwords match ---
            if (request.NewPassword != request.ConfirmPassword)
                return BadRequest(new ApiResponse<object>("New password and confirm password do not match.", "error", StatusCodes.Status400BadRequest));

            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                return BadRequest(new ApiResponse<object>("Password must be at least 6 characters long.", "error", StatusCodes.Status400BadRequest));

            // --- 3. Load the target user (no company restriction for Super Admin) ---
            var targetUser = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive && !u.IsDeleted);

            if (targetUser == null)
                return NotFound(new ApiResponse<object>($"User with ID {userId} not found or is inactive.", "error", StatusCodes.Status404NotFound));

            // --- 4. Load CompanyUser profile for email / logging (optional, best-effort) ---
            var targetCompanyUser = await _context.CompanyUsers
                .FirstOrDefaultAsync(cu => cu.UserId == userId && !(cu.IsDeleted ?? false));

            // --- 5. Hash and save the new password ---
            targetUser.PasswordHash = _passwordHasher.HashPassword(targetUser, request.NewPassword);
            targetUser.UpdatedAt = DateTime.UtcNow;
            targetUser.UpdatedBy = callerUserId.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Super Admin (UserId={CallerUserId}) changed password for UserId={TargetUserId} (CompanyId={TargetCompanyId})",
                callerUserId.Value, userId, targetUser.CompanyId);

            // --- 6. Send notification email (fire-and-forget, non-blocking) ---
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    string targetName = targetCompanyUser?.FirstName ?? "User";
                    string emailBody = _emailTemplateService.GetPasswordChangedConfirmationEmailBody(targetName);
                    await _emailSender.SendEmailAsync(targetUser.Email!, "Your Password Has Been Reset by Super Admin", emailBody);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send password-changed notification email to UserId={TargetUserId}", userId);
                }
            });

            return Ok(new ApiResponse<object>("Password changed successfully.", "success", StatusCodes.Status200OK));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Super Admin password change for UserId={TargetUserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ApiResponse<object>("An error occurred while changing the user's password.", "error", StatusCodes.Status500InternalServerError, null, new { ExceptionMessage = ex.Message }));
        }
    }
}

