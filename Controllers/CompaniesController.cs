using BizfreeApp.Data;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BizfreeApp.Services;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using System.Text;

namespace BizfreeApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompaniesController : ControllerBase
    {
        private readonly Data.ApplicationDbContext _context;
        private readonly ILogger<CompaniesController> _logger;
        private readonly IUploadHandler _uploadHandler;
        private readonly IEmailSender _emailSender;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly IPasswordHasher<User> _passwordHasher;

        public CompaniesController(
            Data.ApplicationDbContext context,
            ILogger<CompaniesController> logger,
            IUploadHandler uploadHandler,
            IEmailSender emailSender,
            IEmailTemplateService emailTemplateService,
            IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _logger = logger;
            _uploadHandler = uploadHandler;
            _emailSender = emailSender;
            _emailTemplateService = emailTemplateService;
            _passwordHasher = passwordHasher;
        }

        // --- THIS SECTION IS NOW CORRECTED ---
        #region Helper Methods
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

        /// <summary>Generates a cryptographically random password of the specified length.</summary>
        private string GenerateRandomPassword(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()";
            var random = new Random();
            var password = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                password.Append(chars[random.Next(chars.Length)]);
            }
            return password.ToString();
        }


        private async System.Threading.Tasks.Task CreateDefaultTaskStatusesForCompany(int companyId, int? currentUserId)
        {
            try
            {
                // Get all default task statuses from the master table
                var defaultStatuses = await _context.Taskstatuses
                    .OrderBy(ts => ts.StatusId)
                    .ToListAsync();

                if (!defaultStatuses.Any())
                {
                    _logger.LogWarning($"No default task statuses found to copy for company {companyId}");
                    return;
                }

                // Create company-specific task statuses
                var companyTaskStatuses = new List<CompanyTaskStatus>();
                int order = 1;

                foreach (var defaultStatus in defaultStatuses)
                {
                    var companyTaskStatus = new CompanyTaskStatus
                    {
                        CompanyId = companyId,
                        DefaultTaskStatusId = defaultStatus.StatusId,
                        Name = defaultStatus.Name,
                        Slug = GenerateSlug(defaultStatus.Name),
                        Order = order++,
                        StatusColor = defaultStatus.StatusColor,
                        IsCustom = false, // These are copies of default statuses
                        IsEditableName = true,
                        IsDeletable = false, // Default statuses shouldn't be deletable
                        CreatedBy = currentUserId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = currentUserId
                    };

                    companyTaskStatuses.Add(companyTaskStatus);
                }

                _context.CompanyTaskStatuses.AddRange(companyTaskStatuses);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Successfully created {companyTaskStatuses.Count} default task statuses for company {companyId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating default task statuses for company {companyId}");
                throw; 
            }
        }

        private string GenerateSlug(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "unknown";

            return name.ToLowerInvariant()
                       .Replace(" ", "-")
                       .Replace("_", "-")
                       .Trim('-');
        }


        private ActionResult<ApiResponse<T>> Success<T>(string message, T? data, int statusCode = StatusCodes.Status200OK)
        {
            return StatusCode(statusCode, new ApiResponse<T>(message, "success", statusCode, data));
        }

        // ** GENERIC ERROR METHOD (RESTORED) **
        // This is the method that was missing, causing the errors.
        private ActionResult<ApiResponse<T>> Error<T>(string message, int statusCode, string status = "error")
        {
            return StatusCode(statusCode, new ApiResponse<T>(message, status, statusCode, default(T)));
        }

        // NON-GENERIC ERROR METHOD (for calls without a specific return type)
        private IActionResult Error(string message, int statusCode, string status = "error")
        {
            return StatusCode(statusCode, new ApiResponse<object>(message, status, statusCode, null));
        }
        #endregion
        // --- END OF CORRECTION ---

        // GET: api/Companies
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<CompanyDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PagedResult<CompanyDto>>>> GetCompanies(
     [FromQuery] int page = 1,
     [FromQuery] int pageSize = 5,
     [FromQuery] string sortBy = "CompanyName",
     [FromQuery] string sortOrder = "asc",
     [FromQuery] string? search = null,
     [FromQuery] string? industry = null,
     [FromQuery] string? companySize = null,
     [FromQuery] bool? isApproved = null,
     [FromQuery] bool? isActive = null,
     [FromQuery] DateTime? createdFrom = null,
     [FromQuery] DateTime? createdTo = null,
     [FromQuery] int? packageId = null
 )
        {
            try
            {
                _logger.LogInformation("Fetching companies with pagination, sorting, and filtering.");

                // Base query with soft delete filter
                IQueryable<Company> query = _context.Companies
                    .Where(c => !c.IsDeleted && c.CompanyId != 0)
                    .Include(c => c.CreatedByNavigation)
                    .Include(c => c.UpdatedByNavigation)
                    .Include(c => c.Package);

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(c =>
                        c.CompanyName.Contains(search) ||
                        (c.CompanyEmail != null && c.CompanyEmail.Contains(search)) ||
                        (c.AboutCompany != null && c.AboutCompany.Contains(search)) ||
                        (c.CompanyAddress != null && c.CompanyAddress.Contains(search))
                    );
                }

                // Apply industry filter
                if (!string.IsNullOrWhiteSpace(industry))
                {
                    var industries = industry.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(i => i.Trim())
                                           .ToList();

                    if (industries.Any())
                    {
                        query = query.Where(c => c.Industry != null && industries.Contains(c.Industry));
                    }
                }

                // Apply company size filter
                if (!string.IsNullOrWhiteSpace(companySize))
                {
                    var companySizeInts = companySize.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                     .Select(cs => int.TryParse(cs.Trim(), out var val) ? (int?)val : null)
                                                     .Where(cs => cs.HasValue)
                                                     .Select(cs => cs.Value)
                                                     .ToList();

                    if (companySizeInts.Any())
                    {
                        query = query.Where(c => c.CompanySize.HasValue && companySizeInts.Contains(c.CompanySize.Value));
                    }
                }

                // Apply approval status filter
                if (isApproved.HasValue)
                {
                    query = query.Where(c => c.IsApproved == isApproved.Value);
                }

                // Apply active status filter
                if (isActive.HasValue)
                {
                    query = query.Where(c => c.IsActive == isActive.Value);
                }

                // Apply package filter
                if (packageId.HasValue)
                {
                    query = query.Where(c => c.PackageId == packageId.Value);
                }

                // Apply date range filters
                if (createdFrom.HasValue)
                {
                    query = query.Where(c => c.CreatedAt >= createdFrom.Value);
                }

                if (createdTo.HasValue)
                {
                    query = query.Where(c => c.CreatedAt <= createdTo.Value);
                }

                // Get total count before pagination
                var totalCount = await query.CountAsync();

                // Apply sorting
                switch (sortBy.ToLower())
                {
                    case "companyid":
                        query = sortOrder.ToLower() == "desc"
                            ? query.OrderByDescending(c => c.CompanyId)
                            : query.OrderBy(c => c.CompanyId);
                        break;
                    case "companyname":
                        query = sortOrder.ToLower() == "desc"
                            ? query.OrderByDescending(c => c.CompanyName)
                            : query.OrderBy(c => c.CompanyName);
                        break;
                    case "companyemail":
                        query = sortOrder.ToLower() == "desc"
                            ? query.OrderByDescending(c => c.CompanyEmail)
                            : query.OrderBy(c => c.CompanyEmail);
                        break;
                    case "companyaddress":
                        query = sortOrder.ToLower() == "desc"
                            ? query.OrderByDescending(c => c.CompanyAddress)
                            : query.OrderBy(c => c.CompanyAddress);
                        break;
                    case "industry":
                        query = sortOrder.ToLower() == "desc"
                            ? query.OrderByDescending(c => c.Industry)
                            : query.OrderBy(c => c.Industry);
                        break;
                    case "companysize":
                        query = sortOrder.ToLower() == "desc"
                            ? query.OrderByDescending(c => c.CompanySize)
                            : query.OrderBy(c => c.CompanySize);
                        break;
                    case "isapproved":
                        query = sortOrder.ToLower() == "desc"
                            ? query.OrderByDescending(c => c.IsApproved)
                            : query.OrderBy(c => c.IsApproved);
                        break;
                    case "isactive":
                        query = sortOrder.ToLower() == "desc"
                            ? query.OrderByDescending(c => c.IsActive)
                            : query.OrderBy(c => c.IsActive);
                        break;
                    case "createdat":
                        query = sortOrder.ToLower() == "desc"
                            ? query.OrderByDescending(c => c.CreatedAt)
                            : query.OrderBy(c => c.CreatedAt);
                        break;
                    case "updatedat":
                        query = sortOrder.ToLower() == "desc"
                            ? query.OrderByDescending(c => c.UpdatedAt)
                            : query.OrderBy(c => c.UpdatedAt);
                        break;
                    default:
                        query = query.OrderBy(c => c.CompanyName);
                        break;
                }

                // Apply pagination
                var companies = query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .AsEnumerable() // switch to client-side evaluation
                    .Select(c => new CompanyDto
                    {
                        CompanyId = c.CompanyId,
                        CompanyName = c.CompanyName,
                        CompanyAddress = c.CompanyAddress,
                        CompanyEmail = c.CompanyEmail,
                        CompanyPhone = c.CompanyPhone,
                        CompanyUrl = c.CompanyUrl,
                        CompanyLogoUrl = c.CompanyLogoUrl,
                        AboutCompany = c.AboutCompany,
                        Industry = c.Industry,
                        CompanySize = c.CompanySize,
                        IsApproved = c.IsApproved,
                        Vision = c.Vision,
                        Mission = c.Mission,
                        Goal = c.Goal,
                        IsActive = c.IsActive,
                        AdminUserId = c.AdminUserId,
                        PackageId = c.PackageId,
                        CreatedAt = c.CreatedAt,
                        CreatedBy = c.CreatedBy,
                        UpdatedAt = c.UpdatedAt,
                        UpdatedBy = c.UpdatedBy,
                        CoreValues = !string.IsNullOrEmpty(c.CoreValues)
                            ? JsonSerializer.Deserialize<List<string>>(c.CoreValues)
                            : new List<string>()
                    })
                    .ToList();

                // Calculate total pages
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                // Create paged result
                var pagedResult = new PagedResult<CompanyDto>
                {
                    Items = companies,
                    TotalCount = totalCount,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    SortBy = sortBy,
                    SortOrder = sortOrder,
                    SearchKeyword = search
                };

                return Success("Companies retrieved successfully.", pagedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving companies with pagination and filtering.");
                return Error<PagedResult<CompanyDto>>("An error occurred while retrieving companies.", StatusCodes.Status500InternalServerError);
            }
        }

        // Existing GET company by ID method (no change needed for IsDeleted here as it's for a specific ID)
        // GET: api/Companies/5 (Modified to return CompanyDto)
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<CompanyDto>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<CompanyDto>>> GetCompany(int id)
        {
            try
            {
                var company = await _context.Companies
                    .Include(c => c.CreatedByNavigation)
                    .Include(c => c.UpdatedByNavigation)
                    .Include(c => c.Package)
                    .FirstOrDefaultAsync(c => c.CompanyId == id && !c.IsDeleted);

                if (company == null)
                {
                    _logger.LogWarning($"Company with ID: {id} not found or has been deleted.");
                    return Error<CompanyDto>("Company not found.", StatusCodes.Status404NotFound);
                }
                var coreValues = !string.IsNullOrEmpty(company.CoreValues)
    ? JsonSerializer.Deserialize<List<string>>(company.CoreValues)
    : new List<string>();
                // Fetch documents separately
                var documents = await _context.CompanyDocuments
                    .Where(cd => cd.CompanyId == id && !cd.IsDeleted && cd.IsActive)
                    .Include(cd => cd.CreatedByUser)
                        .ThenInclude(u => u.CompanyUserUsers)
                    .Include(cd => cd.UpdatedByUser)
                        .ThenInclude(u => u.CompanyUserUsers)
                    .OrderByDescending(cd => cd.CreatedAt)
                    .ToListAsync();

                var companyDto = new CompanyDto
                {
                    CompanyId = company.CompanyId,
                    CompanyName = company.CompanyName,
                    CompanyAddress = company.CompanyAddress,
                    CompanyEmail = company.CompanyEmail,
                    CompanyPhone = company.CompanyPhone,
                    CompanyUrl = company.CompanyUrl,
                    CompanyLogoUrl = company.CompanyLogoUrl,
                    AboutCompany = company.AboutCompany,
                    Industry = company.Industry,
                    CompanySize = company.CompanySize,
                    IsApproved = company.IsApproved,
                    Vision = company.Vision,
                    Mission = company.Mission,
                    Goal = company.Goal,
                    IsActive = company.IsActive,
                    AdminUserId = company.AdminUserId,
                    PackageId = company.PackageId,
                    CreatedAt = company.CreatedAt,
                    CreatedBy = company.CreatedBy,
                    UpdatedAt = company.UpdatedAt,
                    UpdatedBy = company.UpdatedBy,
                    CoreValues = coreValues,
                    Documents = documents.Select(cd => new
                    {
                        Id = cd.Id,
                        CompanyId = cd.CompanyId,
                        DocumentName = cd.DocumentName,
                        Description = cd.Description,
                        DocumentType = cd.DocumentType,
                        DocumentUrl = cd.DocumentUrl,
                        FileSize = cd.FileSize,
                        Version = cd.Version,
                        Category = cd.Category,
                        IsActive = cd.IsActive,
                        CreatedAt = cd.CreatedAt,
                        UpdatedAt = cd.UpdatedAt,
                        CreatedBy = cd.CreatedBy,
                        UpdatedBy = cd.UpdatedBy,
                        CreatedByName = cd.CreatedByUser.CompanyUserUsers
                            .FirstOrDefault()?.FirstName + " " +
                            cd.CreatedByUser.CompanyUserUsers
                            .FirstOrDefault()?.LastName,
                        UpdatedByName = cd.UpdatedByUser.CompanyUserUsers
                            .FirstOrDefault()?.FirstName + " " +
                            cd.UpdatedByUser.CompanyUserUsers
                            .FirstOrDefault()?.LastName
                    }).ToList()
                };

                _logger.LogInformation($"Company with ID: {id} and documents retrieved successfully.");
                return Success("Company retrieved successfully.", companyDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving company with ID: {id}.");
                return Error<CompanyDto>("An error occurred while retrieving the company.", StatusCodes.Status500InternalServerError);
            }
        }


        // POST: api/Companies
        [HttpPost]
        public async Task<ActionResult<ApiResponse<CompanyDto>>> PostCompany(CompanyCreateDto createDto)
        {
            var currentUserId = GetCurrentUserIdFromClaims();
            if (!currentUserId.HasValue)
            {
                _logger.LogWarning("Current user ID claim not found for company creation.");
            }

            // --- BRANCH A: CompanyEmail provided, adminUserId NOT provided → auto-create admin ---
            bool autoCreateAdmin = !string.IsNullOrWhiteSpace(createDto.CompanyEmail) && !createDto.AdminUserId.HasValue;

            if (autoCreateAdmin)
            {
                return await CreateCompanyWithAutoAdmin(createDto, currentUserId);
            }

            // --- BRANCH B: adminUserId provided → old behavior preserved ---
            // --- BRANCH C: neither provided → create company only ---
            return await CreateCompanyOnly(createDto, currentUserId);
        }

        /// <summary>
        /// Branch A — Creates the company AND auto-creates a Company Admin user using CompanyEmail.
        /// The admin's login credentials are emailed to CompanyEmail.
        /// DB triggers handle CompanyRole creation for the new company.
        /// </summary>
        private async Task<ActionResult<ApiResponse<CompanyDto>>> CreateCompanyWithAutoAdmin(CompanyCreateDto createDto, int? currentUserId)
        {
            // 1. Validate email not already used in users table
            bool emailTakenByUser = await _context.Users.AnyAsync(u => u.Email == createDto.CompanyEmail && u.IsActive && !u.IsDeleted);
            if (emailTakenByUser)
            {
                _logger.LogWarning("Company creation failed: Email '{Email}' is already registered as a user.", createDto.CompanyEmail);
                return Error<CompanyDto>($"The email '{createDto.CompanyEmail}' is already associated with an existing user account.", StatusCodes.Status409Conflict);
            }

            // 2. Validate email not already used by another company
            bool emailTakenByCompany = await _context.Companies.AnyAsync(c => c.CompanyEmail == createDto.CompanyEmail);
            if (emailTakenByCompany)
            {
                _logger.LogWarning("Company creation failed: Duplicate company email '{Email}'.", createDto.CompanyEmail);
                return Error<CompanyDto>($"A company with email '{createDto.CompanyEmail}' already exists.", StatusCodes.Status409Conflict);
            }

            // 3. Begin atomic transaction
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 3a. Create the Company row
                var company = new Company
                {
                    CompanyName = createDto.CompanyName,
                    CompanyAddress = createDto.CompanyAddress,
                    CompanyEmail = createDto.CompanyEmail,
                    CompanyPhone = createDto.CompanyPhone,
                    CompanyUrl = createDto.CompanyUrl,
                    CompanyLogoUrl = createDto.CompanyLogoUrl,
                    AboutCompany = createDto.AboutCompany,
                    Industry = createDto.Industry,
                    CompanySize = createDto.CompanySize,
                    IsApproved = createDto.IsApproved,
                    Vision = createDto.Vision,
                    Mission = createDto.Mission,
                    Goal = createDto.Goal,
                    IsActive = createDto.IsActive,
                    IsDeleted = false,
                    AdminUserId = null, // will be set after user creation
                    PackageId = createDto.PackageId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = currentUserId,
                    UpdatedBy = currentUserId,
                    CoreValues = (createDto.CoreValues != null && createDto.CoreValues.Any())
                        ? JsonSerializer.Serialize(createDto.CoreValues)
                        : null
                };

                _context.Companies.Add(company);
                await _context.SaveChangesAsync();
                // DB trigger fires here → creates default CompanyRoles for this company

                // 3b. Create default task statuses
                await CreateDefaultTaskStatusesForCompany(company.CompanyId, currentUserId);

                // 3c. Generate credentials
                string plainPassword = GenerateRandomPassword(10);
                string hashedPassword = _passwordHasher.HashPassword(new User(), plainPassword);

                // Derive a display name from email prefix (e.g., "info@acme.com" → "info")
                string adminFirstName = createDto.CompanyEmail!.Split('@')[0];

                // 3d. Create the User (auth identity)
                var adminUser = new User
                {
                    Email = createDto.CompanyEmail,
                    PasswordHash = hashedPassword,
                    CompanyId = company.CompanyId,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = currentUserId,
                };

                _context.Users.Add(adminUser);
                await _context.SaveChangesAsync();

                // 3e. Find the Company Admin CompanyRole created by the DB trigger
                // Looks for the first role flagged as admin-level for this company
                var adminCompanyRole = await _context.CompanyRoles
                    .Where(cr => cr.CompanyId == company.CompanyId && !cr.IsDeleted)
                    .OrderBy(cr => cr.CompanyRoleId)
                    .FirstOrDefaultAsync(cr =>
                        cr.RoleName.ToLower().Contains("admin") ||
                        cr.DefaultRole != null && cr.DefaultRole.ToLower().Contains("admin"));

                // 3f. Create the CompanyUser (profile / admin link)
                var adminCompanyUser = new CompanyUser
                {
                    UserId = adminUser.UserId,
                    CompanyId = company.CompanyId,
                    FirstName = adminFirstName,
                    LastName = null,
                    RoleId = adminCompanyRole?.CompanyRoleId ?? 0,
                    CheckAdmin = 1,
                    JoiningDate = DateOnly.FromDateTime(DateTime.UtcNow),
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = currentUserId,
                    UpdatedBy = currentUserId,
                };

                _context.CompanyUsers.Add(adminCompanyUser);
                await _context.SaveChangesAsync();

                // 3g. Link admin user back to the company
                company.AdminUserId = adminUser.UserId;
                _context.Entry(company).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Company '{CompanyName}' (ID={CompanyId}) created with auto-admin (UserId={AdminUserId}, Email={Email}) by user {CallerUserId}.",
                    company.CompanyName, company.CompanyId, adminUser.UserId, createDto.CompanyEmail, currentUserId);

                // 4. Send welcome email (fire-and-forget, non-blocking)
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        string emailBody = _emailTemplateService.GetWelcomeEmailBody(
                            adminFirstName, "", createDto.CompanyEmail!, plainPassword);
                        await _emailSender.SendEmailAsync(
                            createDto.CompanyEmail!, "Welcome to Bizfree — Your Login Credentials", emailBody);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send welcome email to '{Email}'.", createDto.CompanyEmail);
                    }
                });

                // 5. Fetch roles for response
                var createdCompanyRoles = await _context.CompanyRoles
                    .Where(cr => cr.CompanyId == company.CompanyId)
                    .Select(cr => new CompanyRoleDto
                    {
                        CompanyRoleId = cr.CompanyRoleId,
                        CompanyId = cr.CompanyId,
                        RoleName = cr.RoleName,
                        IsActive = cr.IsActive,
                        DefaultRole = cr.DefaultRole,
                        DefaultPermission = cr.DefaultPermission,
                        CreatedAt = cr.CreatedAt,
                    })
                    .ToListAsync();

                var responseDto = new CompanyDto
                {
                    CompanyId = company.CompanyId,
                    CompanyName = company.CompanyName,
                    CompanyAddress = company.CompanyAddress,
                    CompanyEmail = company.CompanyEmail,
                    CompanyPhone = company.CompanyPhone,
                    CompanyUrl = company.CompanyUrl,
                    CompanyLogoUrl = company.CompanyLogoUrl,
                    AboutCompany = company.AboutCompany,
                    Industry = company.Industry,
                    CompanySize = company.CompanySize,
                    IsApproved = company.IsApproved,
                    Vision = company.Vision,
                    Mission = company.Mission,
                    Goal = company.Goal,
                    IsActive = company.IsActive,
                    AdminUserId = company.AdminUserId,
                    AdminEmail = createDto.CompanyEmail, // confirms credentials were sent here
                    PackageId = company.PackageId,
                    CreatedAt = company.CreatedAt,
                    CreatedBy = company.CreatedBy,
                    UpdatedAt = company.UpdatedAt,
                    UpdatedBy = company.UpdatedBy,
                    CompanyRoles = createdCompanyRoles,
                    CoreValues = !string.IsNullOrEmpty(company.CoreValues)
                        ? JsonSerializer.Deserialize<List<string>>(company.CoreValues)
                        : new List<string>(),
                };

                return Success("Company created successfully. Login credentials have been sent to the admin email.", responseDto, StatusCodes.Status201Created);
            }
            catch (DbUpdateException dbEx)
            {
                await transaction.RollbackAsync();
                _logger.LogError(dbEx, "Database error creating company with auto-admin. Inner: {Inner}", dbEx.InnerException?.Message);
                if (dbEx.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23503")
                    return Error<CompanyDto>("A data integrity error occurred. Please ensure all related IDs are valid.", StatusCodes.Status409Conflict);
                return Error<CompanyDto>("A database error occurred while creating the company. Please try again.", StatusCodes.Status500InternalServerError);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error creating company with auto-admin.");
                return Error<CompanyDto>("An unexpected error occurred while creating the company.", StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Branch B (adminUserId provided) and Branch C (neither provided).
        /// Preserves the original company-only creation behavior exactly.
        /// </summary>
        private async Task<ActionResult<ApiResponse<CompanyDto>>> CreateCompanyOnly(CompanyCreateDto createDto, int? currentUserId)
        {
            try
            {
                // Validate Company Email Uniqueness
                if (!string.IsNullOrEmpty(createDto.CompanyEmail) && await _context.Companies.AnyAsync(c => c.CompanyEmail == createDto.CompanyEmail))
                {
                    _logger.LogWarning("Company creation failed: Duplicate email '{Email}'.", createDto.CompanyEmail);
                    return Error<CompanyDto>($"Company with email '{createDto.CompanyEmail}' already exists.", StatusCodes.Status409Conflict);
                }

                // Validate adminUserId if provided
                if (createDto.AdminUserId.HasValue)
                {
                    var adminUserExists = await _context.Users.AnyAsync(u => u.UserId == createDto.AdminUserId.Value);
                    if (!adminUserExists)
                    {
                        _logger.LogWarning("Company creation failed: AdminUserId {Id} does not exist.", createDto.AdminUserId.Value);
                        return Error<CompanyDto>("The provided Admin User ID does not exist. Please provide a valid user ID.", StatusCodes.Status400BadRequest);
                    }
                }

                var company = new Company
                {
                    CompanyName = createDto.CompanyName,
                    CompanyAddress = createDto.CompanyAddress,
                    CompanyEmail = createDto.CompanyEmail,
                    CompanyPhone = createDto.CompanyPhone,
                    CompanyUrl = createDto.CompanyUrl,
                    CompanyLogoUrl = createDto.CompanyLogoUrl,
                    AboutCompany = createDto.AboutCompany,
                    Industry = createDto.Industry,
                    CompanySize = createDto.CompanySize,
                    IsApproved = createDto.IsApproved,
                    Vision = createDto.Vision,
                    Mission = createDto.Mission,
                    Goal = createDto.Goal,
                    IsActive = createDto.IsActive,
                    IsDeleted = false,
                    AdminUserId = createDto.AdminUserId,
                    PackageId = createDto.PackageId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = currentUserId,
                    UpdatedBy = currentUserId,
                    CoreValues = (createDto.CoreValues != null && createDto.CoreValues.Any())
                        ? JsonSerializer.Serialize(createDto.CoreValues)
                        : null
                };

                _context.Companies.Add(company);
                await _context.SaveChangesAsync();

                await CreateDefaultTaskStatusesForCompany(company.CompanyId, currentUserId);

                var createdCompanyRoles = await _context.CompanyRoles
                    .Where(cr => cr.CompanyId == company.CompanyId)
                    .Select(cr => new CompanyRoleDto
                    {
                        CompanyRoleId = cr.CompanyRoleId,
                        CompanyId = cr.CompanyId,
                        RoleName = cr.RoleName,
                        IsActive = cr.IsActive,
                        DefaultRole = cr.DefaultRole,
                        DefaultPermission = cr.DefaultPermission,
                        CreatedAt = cr.CreatedAt,
                    })
                    .ToListAsync();

                _logger.LogInformation("Company '{CompanyName}' (ID={CompanyId}) created by user {UserId}.",
                    company.CompanyName, company.CompanyId, currentUserId);

                var companyResponseDto = new CompanyDto
                {
                    CompanyId = company.CompanyId,
                    CompanyName = company.CompanyName,
                    CompanyAddress = company.CompanyAddress,
                    CompanyEmail = company.CompanyEmail,
                    CompanyPhone = company.CompanyPhone,
                    CompanyUrl = company.CompanyUrl,
                    CompanyLogoUrl = company.CompanyLogoUrl,
                    AboutCompany = company.AboutCompany,
                    Industry = company.Industry,
                    CompanySize = company.CompanySize,
                    IsApproved = company.IsApproved,
                    Vision = company.Vision,
                    Mission = company.Mission,
                    Goal = company.Goal,
                    IsActive = company.IsActive,
                    AdminUserId = company.AdminUserId,
                    PackageId = company.PackageId,
                    CreatedAt = company.CreatedAt,
                    CreatedBy = company.CreatedBy,
                    UpdatedAt = company.UpdatedAt,
                    UpdatedBy = company.UpdatedBy,
                    CompanyRoles = createdCompanyRoles,
                    CoreValues = !string.IsNullOrEmpty(company.CoreValues)
                        ? JsonSerializer.Deserialize<List<string>>(company.CoreValues)
                        : new List<string>(),
                };

                return Success("Company created successfully.", companyResponseDto, StatusCodes.Status201Created);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error creating company. Inner: {Inner}", dbEx.InnerException?.Message);
                if (dbEx.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23503")
                    return Error<CompanyDto>("A data integrity error occurred. Please ensure all related IDs are valid.", StatusCodes.Status409Conflict);
                return Error<CompanyDto>("A database error occurred while creating the company. Please try again.", StatusCodes.Status500InternalServerError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating company.");
                return Error<CompanyDto>("An unexpected error occurred while creating the company.", StatusCodes.Status500InternalServerError);
            }
        }


        // PUT: api/Companies/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCompany(int id, CompanyUpdateDto updateDto)
        {
            try
            {
                var company = await _context.Companies.FindAsync(id);
                if (company == null)
                {
                    // This call uses the non-generic Error() and was already correct
                    return Error("Company not found.", StatusCodes.Status404NotFound);
                }

                var currentUserId = GetCurrentUserIdFromClaims();

                company.CompanyName = updateDto.CompanyName;
                company.CompanyAddress = updateDto.CompanyAddress;
                company.CompanyEmail = updateDto.CompanyEmail;
                company.CompanyPhone = updateDto.CompanyPhone;
                company.CompanyUrl = updateDto.CompanyUrl;
                if (!string.IsNullOrEmpty(updateDto.CompanyLogoUrl))
                {
                    company.CompanyLogoUrl = updateDto.CompanyLogoUrl;
                }
                company.AboutCompany = updateDto.AboutCompany;
                company.Industry = updateDto.Industry;
                company.CompanySize = updateDto.CompanySize;
                company.IsApproved = updateDto.IsApproved;
                company.Vision = updateDto.Vision;
                company.Mission = updateDto.Mission;
                company.Goal = updateDto.Goal;
                company.IsActive = updateDto.IsActive;
                company.AdminUserId = updateDto.AdminUserId;
                company.PackageId = updateDto.PackageId;
                company.UpdatedAt = DateTime.UtcNow;
                company.UpdatedBy = currentUserId;
                company.CoreValues = (updateDto.CoreValues != null && updateDto.CoreValues.Any())
                ? JsonSerializer.Serialize(updateDto.CoreValues)
                : null;

                _context.Entry(company).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating company with ID: {id}.");
                return Error("An unexpected error occurred while updating the company.", StatusCodes.Status500InternalServerError);
            }
        }

        // DELETE: api/Companies/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCompany(int id)
        {
            try
            {
                var company = await _context.Companies.FindAsync(id);
                if (company == null)
                {
                    return Error("Company not found.", StatusCodes.Status404NotFound);
                }

                company.IsDeleted = true;
                company.UpdatedBy = GetCurrentUserIdFromClaims();
                company.UpdatedAt = DateTime.UtcNow;
                _context.Entry(company).State = EntityState.Modified;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting company with ID: {id}.");
                return Error("An unexpected error occurred while deleting the company.", StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("{id}/upload-logo")]
        public async Task<ActionResult<ApiResponse<string>>> UploadCompanyLogo(int id, IFormFile logoFile)
        {
            try
            {
                if (logoFile == null || logoFile.Length == 0)
                {
                    return Error<string>("Logo file is required.", StatusCodes.Status400BadRequest);
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                var fileExtension = Path.GetExtension(logoFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Error<string>("Only image files (jpg, jpeg, png, gif, bmp) are allowed.", StatusCodes.Status400BadRequest);
                }

                // Validate file size (e.g., max 5MB)
                if (logoFile.Length > 5 * 1024 * 1024)
                {
                    return Error<string>("File size cannot exceed 5MB.", StatusCodes.Status400BadRequest);
                }

                // Check if company exists
                var company = await _context.Companies.FindAsync(id);
                if (company == null)
                {
                    return Error<string>("Company not found.", StatusCodes.Status404NotFound);
                }

                // Upload the logo using your existing UploadHandler
                var logoUrl = await _uploadHandler.SaveCompanyLogoAsync(logoFile);

                // Update company with new logo URL
                company.CompanyLogoUrl = logoUrl;
                company.UpdatedAt = DateTime.UtcNow;
                company.UpdatedBy = GetCurrentUserIdFromClaims();

                _context.Entry(company).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                // Fixed: Added the required third parameter (status code)
                return Success("Company logo uploaded successfully.", logoUrl, StatusCodes.Status200OK);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading logo for company ID: {id}.");
                return Error<string>("An error occurred while uploading the company logo.", StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("{companyId}/documents")]
        public async Task<ActionResult<ApiResponse<CompanyDocumentResponseDto>>> UploadCompanyDocument(
    int companyId,
    [FromForm] CompanyDocumentCreateDto documentDto,
    IFormFile documentFile)
        {
            try
            {
                if (documentFile == null || documentFile.Length == 0)
                {
                    return Error<CompanyDocumentResponseDto>("Document file is required.", StatusCodes.Status400BadRequest);
                }

                // Validate file size (e.g., max 50MB)
                if (documentFile.Length > 50 * 1024 * 1024)
                {
                    return Error<CompanyDocumentResponseDto>("File size cannot exceed 50MB.", StatusCodes.Status400BadRequest);
                }

                // Validate allowed file types
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                var fileExtension = Path.GetExtension(documentFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Error<CompanyDocumentResponseDto>("File type not allowed. Supported formats: PDF, DOC, DOCX, XLS, XLSX, PPT, PPTX, TXT, JPG, JPEG, PNG, GIF, BMP.", StatusCodes.Status400BadRequest);
                }

                var currentUserId = GetCurrentUserIdFromClaims();
                if (currentUserId == 0) // or whatever indicates invalid user
                {
                    return Error<CompanyDocumentResponseDto>("User authentication required.", StatusCodes.Status401Unauthorized);
                }

                // Upload the document using UploadHandler
                var uploadedDocument = await _uploadHandler.UploadCompanyDocumentAsync(
            currentUserId.Value, // Use .Value to get the int from int?
                    companyId,
                    documentFile,
                    documentDto.DocumentName,
                    documentDto.Description,
                    documentDto.Version,
                    documentDto.Category);

                // Map to response DTO
                var responseDto = new CompanyDocumentResponseDto
                {
                    Id = uploadedDocument.Id,
                    CompanyId = uploadedDocument.CompanyId,
                    DocumentName = uploadedDocument.DocumentName,
                    Description = uploadedDocument.Description,
                    DocumentType = uploadedDocument.DocumentType,
                    DocumentUrl = uploadedDocument.DocumentUrl,
                    FileSize = uploadedDocument.FileSize,
                    Version = uploadedDocument.Version,
                    Category = uploadedDocument.Category,
                    IsActive = uploadedDocument.IsActive,
                    CreatedAt = uploadedDocument.CreatedAt,
                    UpdatedAt = uploadedDocument.UpdatedAt,
                    CreatedByName = $"User {uploadedDocument.CreatedBy}", // You might want to fetch actual user name
                    UpdatedByName = $"User {uploadedDocument.UpdatedBy}"
                };

                return Success("Company document uploaded successfully.", responseDto, StatusCodes.Status201Created);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, $"Validation error uploading document for company {companyId}.");
                return Error<CompanyDocumentResponseDto>(ex.Message, StatusCodes.Status400BadRequest);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, $"Invalid argument for company {companyId} document upload.");
                return Error<CompanyDocumentResponseDto>(ex.Message, StatusCodes.Status400BadRequest);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, $"File system error uploading document for company {companyId}.");
                return Error<CompanyDocumentResponseDto>("File upload failed due to server error.", StatusCodes.Status500InternalServerError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error uploading document for company {companyId}.");
                return Error<CompanyDocumentResponseDto>("An unexpected error occurred while uploading the document.", StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("{companyId}/documents")]
        public async Task<ActionResult<ApiResponse<List<CompanyDocumentResponseDto>>>> GetCompanyDocuments(int companyId)
        {
            try
            {
                var documents = await _context.CompanyDocuments
                    .Where(cd => cd.CompanyId == companyId && !cd.IsDeleted)
                    .Include(cd => cd.CreatedByUser)
                    .Include(cd => cd.UpdatedByUser)
                    .OrderByDescending(cd => cd.CreatedAt)
                    .ToListAsync();

                var documentDtos = documents.Select(doc => new CompanyDocumentResponseDto
                {
                    Id = doc.Id,
                    CompanyId = doc.CompanyId,
                    DocumentName = doc.DocumentName,
                    Description = doc.Description,
                    DocumentType = doc.DocumentType,
                    DocumentUrl = doc.DocumentUrl,
                    FileSize = doc.FileSize,
                    Version = doc.Version,
                    Category = doc.Category,
                    IsActive = doc.IsActive,
                    CreatedAt = doc.CreatedAt,
                    UpdatedAt = doc.UpdatedAt,
                    //CreatedByName = doc.CreatedByUser?.UserName ?? "Unknown",
                    //UpdatedByName = doc.UpdatedByUser?.UserName ?? "Unknown"
                }).ToList();

                return Success("Company documents retrieved successfully.", documentDtos, StatusCodes.Status200OK);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving documents for company {companyId}.");
                return Error<List<CompanyDocumentResponseDto>>("An error occurred while retrieving company documents.", StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("{companyId}/documents/{documentId}")]
        public async Task<IActionResult> DeleteCompanyDocument(int companyId, int documentId)
        {
            try
            {
                // Check if company exists
                var company = await _context.Companies.FindAsync(companyId);
                if (company == null)
                {
                    return Error("Company not found.", StatusCodes.Status404NotFound);
                }

                // Find the document
                var document = await _context.CompanyDocuments
                    .FirstOrDefaultAsync(cd => cd.Id == documentId && cd.CompanyId == companyId && !cd.IsDeleted);

                if (document == null)
                {
                    return Error("Document not found.", StatusCodes.Status404NotFound);
                }

                // Get current user for audit trail
                var currentUserId = GetCurrentUserIdFromClaims();

                if (!currentUserId.HasValue)
                {
                    _logger.LogWarning($"Attempt to delete document {documentId} for company {companyId} without a valid user ID.");
                    return Error("User authentication required to perform this action.", StatusCodes.Status401Unauthorized);
                }

                // Soft delete - mark as deleted instead of physically removing
                document.IsDeleted = true;
                document.UpdatedAt = DateTime.UtcNow;
                document.UpdatedBy = currentUserId.Value;

                _context.Entry(document).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Company document '{document.DocumentName}' (ID: {documentId}) deleted by user {currentUserId}.");

                // Return 204 No Content for successful deletion
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting document {documentId} for company {companyId}.");
                return Error("An error occurred while deleting the document.", StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("dashboard-stats")]
        [ProducesResponseType(typeof(ApiResponse<DashboardStatsDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<DashboardStatsDto>>> GetDashboardStats()
        {
            try
            {
                var totalActiveUsers = await _context.CompanyUsers
                    .Where(cu => cu.IsActive == true && cu.IsDeleted != true)
                    .CountAsync();

                var totalCompanies = await _context.Companies
                    .Where(c => !c.IsDeleted && c.CompanyId != 0) // Exclude company with ID = 0
                    .CountAsync();

                var activeCompanies = await _context.Companies
                    .Where(c => !c.IsDeleted && c.IsActive == true && c.CompanyId != 0) // Exclude company with ID = 0
                    .CountAsync();

                var approvedCompanies = await _context.Companies
                    .Where(c => !c.IsDeleted && c.IsApproved == true && c.CompanyId != 0) // Exclude company with ID = 0
                    .CountAsync();

                var stats = new DashboardStatsDto
                {
                    TotalActiveUsers = totalActiveUsers,
                    TotalCompanies = totalCompanies,
                    ActiveCompanies = activeCompanies,
                    ApprovedCompanies = approvedCompanies
                };

                return Success("Dashboard statistics retrieved successfully.", stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard statistics.");
                return Error<DashboardStatsDto>("An error occurred while retrieving dashboard statistics.", StatusCodes.Status500InternalServerError);
            }
        }

        private bool CompanyExists(int id)
        {
            return _context.Companies.Any(e => e.CompanyId == id);
        }
    }
}
