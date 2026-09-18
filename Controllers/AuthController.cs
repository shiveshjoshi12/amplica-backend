using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs;
using BizfreeApp.Data;
using BizfreeApp.Services; // Add this using directive to access IEmailSender
using System.Collections.Generic; // Make sure this is included for List<string>
using System.Text.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Microsoft.AspNetCore.Identity;

[ApiExplorerSettings(IgnoreApi = false)]
[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly BizfreeApp.Data.ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateService _emailTemplateService;

    // Permissions caching (moved here for use across relevant methods)
    private readonly Dictionary<string, (List<string> permissions, DateTime cachedAt)> _permissionsCache = new();
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5); // Cache for 5 minutes

    public AuthController(BizfreeApp.Data.ApplicationDbContext context, IConfiguration configuration, ILogger<AuthController> logger, IEmailSender emailSender, IEmailTemplateService emailTemplateService)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _emailSender = emailSender;
        _emailTemplateService = emailTemplateService;
    }

    private string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    // MODIFIED: This method now accepts a list of permissions to include in the token
    private JwtSecurityToken GenerateAccessToken(User user, List<string> userPermissions)
    {
        var claims = new List<Claim>
        {
            new Claim("UserId", user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "User"), // Standard role claim
            new Claim("RoleId", user.RoleId?.ToString() ?? "0"),      // Custom Role ID claim
            new Claim("CompanyId", user.CompanyId?.ToString() ?? "0") // Custom Company ID claim
        };

        // --- IMPORTANT: ADD GRANULAR PERMISSIONS AS CLAIMS ---
        foreach (var permission in userPermissions)
        {
            claims.Add(new Claim("permission", permission)); // Add each permission with "permission" type
        }
        // --- END IMPORTANT SECTION ---

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        return new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(360), // Token valid for 24 hours
            signingCredentials: creds
        );
    }

    // Generates a short-lived impersonation token for the Super Admin.
    // The UserId claim is kept as the Super Admin's ID to preserve the audit trail.
    private string GenerateImpersonationToken(User superAdminUser, int targetCompanyId, List<string> companyAdminPermissions)
    {
        var claims = new List<Claim>
        {
            new Claim("UserId", superAdminUser.UserId.ToString()),           // SA's ID � audit trail (CreatedBy/UpdatedBy)
            new Claim(JwtRegisteredClaimNames.Email, superAdminUser.Email ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, superAdminUser.Role?.RoleName ?? "SuperAdmin"),
            new Claim("RoleId", superAdminUser.RoleId?.ToString() ?? "0"),
            new Claim("CompanyId", targetCompanyId.ToString()),              // Target company scope
            new Claim("IsImpersonating", "true"),
            new Claim("ImpersonatorId", superAdminUser.UserId.ToString()),
            new Claim("ImpersonatedCompanyId", targetCompanyId.ToString()),
        };

        foreach (var permission in companyAdminPermissions)
            claims.Add(new Claim("permission", permission));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),   // Short-lived: 1 hour
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation($"Login attempt for email: {request.Email} at {DateTime.UtcNow}.");

        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            _logger.LogWarning("Login failed: Email or password not provided.");
            return BadRequest(new { message = "Email and password are required.", status = "error", status_code = 400 });
        }

        // Query for user and their role
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive && !u.IsDeleted);

        if (user == null)
        {
            _logger.LogWarning($"Login failed for email: {request.Email} - User not found or inactive.");
            return Unauthorized(new { message = "Invalid credentials.", status = "error", status_code = 401 });
        }
        // :white_check_mark: Verify password using PasswordHasher
        var passwordHasher = new PasswordHasher<User>();
        var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning($"Login failed for email: {request.Email} - Invalid password.");
            return Unauthorized(new { message = "Invalid credentials.", status = "error", status_code = 401 });
        }

        // Get CompanyUser
        var companyUser = await _context.CompanyUsers
            .FirstOrDefaultAsync(cu => cu.UserId == user.UserId);

        if (companyUser == null)
        {
            _logger.LogWarning($"CompanyUser not found for UserId: {user.UserId}");
            return BadRequest(new { message = "User company information not found.", status = "error", status_code = 400 });
        }

        // *** FCM TOKEN MIGRATION LOGIC ***
        bool fcmTokenSaved = false;
        string fcmTokenMigrationInfo = "";

        if (!string.IsNullOrEmpty(request.FcmToken))
        {
            try
            {
                // Check if FCM token already exists for another user
                var existingUserWithToken = await _context.Users
                    .FirstOrDefaultAsync(u => u.FcmToken == request.FcmToken &&
                                             u.UserId != user.UserId &&
                                             !u.IsDeleted);

                if (existingUserWithToken != null)
                {
                    // Remove FCM token from previous user
                    var previousUserEmail = existingUserWithToken.Email;
                    existingUserWithToken.FcmToken = null;
                    existingUserWithToken.FcmTokenUpdatedAt = DateTime.UtcNow;
                    existingUserWithToken.DeviceId = null;
                    existingUserWithToken.UpdatedAt = DateTime.UtcNow;

                    _logger.LogInformation($"FCM token migrated from user {existingUserWithToken.UserId} ({previousUserEmail}) to user {user.UserId} ({user.Email})");
                    fcmTokenMigrationInfo = $"Token migrated from {previousUserEmail}";
                }

                // Assign FCM token to current user
                user.FcmToken = request.FcmToken;
                user.FcmTokenUpdatedAt = DateTime.UtcNow;
                user.DeviceId = request.DeviceId;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                fcmTokenSaved = true;

                _logger.LogInformation($"FCM token saved successfully for user {user.UserId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save FCM token for user {UserId}", user.UserId);
            }
        }

        // *** REST OF YOUR EXISTING LOGIN LOGIC ***
        var permissions = await GetUserPermissionsAsync(companyUser.RoleId, user.CompanyId);

        string? fullName = $"{companyUser.FirstName} {companyUser.LastName}".Trim();
        if (string.IsNullOrEmpty(fullName))
        {
            fullName = null;
        }

        var accessToken = GenerateAccessToken(user, permissions);
        var encodedAccessToken = new JwtSecurityTokenHandler().WriteToken(accessToken);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation($"User {user.UserId} logged in successfully at {DateTime.UtcNow}.");

        return Ok(new
        {
            message = "Login successful.",
            status = "success",
            status_code = 200,
            token = encodedAccessToken,
            refreshToken = refreshToken,
            fcmTokenSaved = fcmTokenSaved,
            fcmTokenMigrated = !string.IsNullOrEmpty(fcmTokenMigrationInfo), // New field
            fcmMigrationInfo = fcmTokenMigrationInfo, // New field
            user = new
            {
                id = user.UserId,
                email = user.Email,
                role = user.Role?.RoleName,
                roleId = user.RoleId,
                companyId = user.CompanyId,
                companyRoleId = companyUser.RoleId,
                fullName = fullName
            },
            permissions = permissions
        });
    }

    // Updated method to fetch permissions from company_roles table
    private async Task<List<string>> GetUserPermissionsAsync(int companyRoleId, int? companyId)
    {
        _logger.LogInformation($"GetUserPermissionsAsync called: CompanyRoleId={companyRoleId}, CompanyId={companyId}");

        if (!companyId.HasValue)
        {
            _logger.LogWarning("CompanyId is null - returning empty permissions");
            return new List<string>();
        }
        // Handle super admin case (companyId = 0)
        var actualCompanyId = companyId.Value;
        var cacheKey = $"companyrole_{companyRoleId}_{actualCompanyId}";

        _logger.LogInformation($"Processing user with CompanyId={actualCompanyId} {(actualCompanyId == 0 ? "(Super Admin)" : "")}");

        // Check cache first
        if (_permissionsCache.TryGetValue(cacheKey, out var cached) &&
            DateTime.UtcNow - cached.cachedAt < _cacheTimeout)
        {
            _logger.LogInformation($"Permissions retrieved from cache: {cached.permissions.Count} permissions");
            return cached.permissions;
        }

        // Fetch from database if not in cache or expired
        _logger.LogInformation($"About to query CompanyRoles with: CompanyRoleId={companyRoleId}, CompanyId={actualCompanyId}");

        var companyRole = await _context.CompanyRoles
            .Where(cr => cr.CompanyRoleId == companyRoleId &&
                         cr.CompanyId == actualCompanyId &&
                         cr.IsActive == true &&
                         (cr.IsDeleted == false || cr.IsDeleted == null))
            .FirstOrDefaultAsync();

        _logger.LogInformation($"Query result: {(companyRole != null ? "Found" : "Not Found")}");

        if (companyRole == null)
        {
            _logger.LogWarning($"No CompanyRole found for CompanyRoleId={companyRoleId}, CompanyId={actualCompanyId}");

            // Additional debugging: Let's see what records actually exist
            var debugRoles = await _context.CompanyRoles
                .Where(cr => cr.CompanyId == actualCompanyId)
                .Select(cr => new { cr.CompanyRoleId, cr.RoleName, cr.IsActive, cr.IsDeleted })
                .ToListAsync();

            _logger.LogInformation($"Available CompanyRoles for CompanyId {actualCompanyId}: {System.Text.Json.JsonSerializer.Serialize(debugRoles)}");

            return new List<string>();
        }

        _logger.LogInformation($"CompanyRole found: {companyRole.RoleName}, DefaultPermission length: {companyRole.DefaultPermission?.Length ?? 0}");

        var permissions = new List<string>();

        if (!string.IsNullOrEmpty(companyRole.DefaultPermission))
        {
            try
            {
                _logger.LogInformation($"Raw DefaultPermission: {companyRole.DefaultPermission}");

                // Parse the JSON array of permission names
                var permissionNames = System.Text.Json.JsonSerializer.Deserialize<string[]>(companyRole.DefaultPermission);

                if (permissionNames != null && permissionNames.Length > 0)
                {
                    permissions = permissionNames.ToList();
                    _logger.LogInformation($"Successfully parsed {permissions.Count} permissions: [{string.Join(", ", permissions.Take(5))}...]");
                }
                else
                {
                    _logger.LogWarning("Parsed permissions array is null or empty");
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing failed for CompanyRoleId {CompanyRoleId}. Raw data: {RawData}",
                    companyRoleId, companyRole.DefaultPermission);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error parsing permissions for CompanyRoleId {CompanyRoleId}", companyRoleId);
            }
        }
        else
        {
            _logger.LogWarning($"DefaultPermission is null or empty for CompanyRoleId={companyRoleId}");
        }

        // Store in cache
        _permissionsCache[cacheKey] = (permissions, DateTime.UtcNow);
        _logger.LogInformation($"Cached {permissions.Count} permissions with key: {cacheKey}");
        return permissions;
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        _logger.LogInformation($"Refresh token request received at {DateTime.UtcNow}.");

        if (string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.RefreshToken))
        {
            _logger.LogWarning("Refresh token failed: Invalid client request (missing token or refresh token).");
            return BadRequest(new { message = "Invalid client request (token or refresh token missing).", status = "error", status_code = 400 });
        }

        var principal = GetPrincipalFromExpiredToken(request.Token);
        if (principal == null)
        {
            _logger.LogWarning("Refresh token failed: Invalid access token (principal is null).");
            return BadRequest(new { message = "Invalid access token.", status = "error", status_code = 400 });
        }

        var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            _logger.LogWarning("Refresh token failed: Token claims missing UserId or invalid format.");
            return BadRequest(new { message = "Invalid token claims (UserId missing).", status = "error", status_code = 400 });
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            _logger.LogWarning($"Refresh token failed for user {userId}: Invalid, used, or expired refresh token.");
            return Unauthorized(new { message = "Invalid or expired refresh token.", status = "error", status_code = 401 });
        }

        // Get CompanyUser to fetch RoleId (which now references CompanyRole)
        var companyUser = await _context.CompanyUsers
            .Include(cu => cu.CompanyRole) // Include CompanyRole navigation
            .FirstOrDefaultAsync(cu => cu.UserId == user.UserId);

        if (companyUser == null)
        {
            _logger.LogWarning($"CompanyUser not found for UserId: {user.UserId} during refresh token");
            return BadRequest(new { message = "User company information not found.", status = "error", status_code = 400 });
        }

        // UPDATED: Use RoleId instead of CompanyRoleId (RoleId now references CompanyRole)
        var userPermissions = await GetUserPermissionsAsync(companyUser.RoleId, user.CompanyId);

        _logger.LogInformation($"Refresh token - Permissions fetched: Count={userPermissions.Count}");

        var newAccessToken = GenerateAccessToken(user, userPermissions);
        var newEncodedAccessToken = new JwtSecurityTokenHandler().WriteToken(newAccessToken);
        var newRefreshToken = GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation($"Access token refreshed successfully for user {userId} at {DateTime.UtcNow}.");

        return Ok(new
        {
            message = "Token refreshed successfully.",
            status = "success",
            status_code = 200,
            token = newEncodedAccessToken,
            refreshToken = newRefreshToken,
            permissions = userPermissions
        });
    }


    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = _configuration["Jwt:Audience"],
            ValidateIssuer = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!)),
            ValidateLifetime = false // IMPORTANT: Set to false to allow validating expired tokens for refresh flow
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogWarning("GetPrincipalFromExpiredToken failed: Invalid security token or algorithm mismatch.");
                return null;
            }

            return principal;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogError(ex, "SecurityTokenException occurred while validating expired token. Token: {Token}", token);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred in GetPrincipalFromExpiredToken. Token: {Token}", token);
            return null;
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
        {
            _logger.LogWarning("Logout failed: Invalid user session (UserId claim missing or invalid).");
            return Unauthorized(new { message = "Invalid user session.", status = "error", status_code = 401 });
        }

        _logger.LogInformation($"Logout attempt for user ID: {userId} at {DateTime.UtcNow}.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive && !u.IsDeleted);
        if (user == null)
        {
            _logger.LogWarning($"Logout failed: User {userId} not found or inactive/deleted.");
            return NotFound(new { message = "User not found or already logged out.", status = "error", status_code = 404 });
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation($"User {userId} logged out successfully at {DateTime.UtcNow}.");
        return Ok(new { message = "Logged out successfully.", status = "success", status_code = 200 });
    }
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost("impersonate/{companyId:int}")]
    public async Task<IActionResult> Impersonate(int companyId)
    {
        // 1. Get Super Admin's UserId from current token
        var superAdminIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (superAdminIdClaim == null || !int.TryParse(superAdminIdClaim.Value, out int superAdminUserId))
            return Unauthorized(new { message = "Invalid session.", status = "error", status_code = 401 });

        var superAdminUser = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == superAdminUserId && u.IsActive && !u.IsDeleted);

        if (superAdminUser == null)
            return Unauthorized(new { message = "Super Admin not found.", status = "error", status_code = 401 });

        // 2. Validate target company exists
        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.CompanyId == companyId && !c.IsDeleted);
        if (company == null)
            return NotFound(new { message = "Company not found.", status = "error", status_code = 404 });

        // 3. Find the admin CompanyRole for this company (first created = admin role)
        var adminCompanyRole = await _context.CompanyRoles
            .Where(cr => cr.CompanyId == companyId
                      && cr.IsActive == true
                      && cr.IsDeleted == false)
            .OrderBy(cr => cr.CompanyRoleId)
            .FirstOrDefaultAsync();

        if (adminCompanyRole == null)
            return NotFound(new {
                message = "No active role found for this company.",
                status = "error", status_code = 404
            });

        // 4. Load permissions using existing method (reads default_permission JSON from company_roles)
        var permissions = await GetUserPermissionsAsync(adminCompanyRole.CompanyRoleId, companyId);

        // 5. Generate impersonation token � SA's UserId preserved for audit trail
        var impersonationToken = GenerateImpersonationToken(superAdminUser, companyId, permissions);

        _logger.LogInformation(
            "SuperAdmin {SAId} started impersonating CompanyId {CompanyId} at {Time}",
            superAdminUserId, companyId, DateTime.UtcNow);

        return Ok(new
        {
            message = "Impersonation started.",
            status = "success",
            status_code = 200,
            token = impersonationToken,
            isImpersonating = true,
            impersonatedCompanyId = companyId,
            companyName = company.CompanyName,
            impersonatedRoleName = adminCompanyRole.RoleName,
            permissions,
            expiresInMinutes = 60
        });
    }


    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
        {
            return Unauthorized(new { message = "Invalid user session.", status = "error", status_code = 401 });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive && !u.IsDeleted);
        if (user == null)
        {
            return NotFound(new { message = "User not found.", status = "error", status_code = 404 });
        }

        var passwordHasher = new PasswordHasher<User>();
        bool isPasswordValid = false;

        try
        {
            var verifyResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
            if (verifyResult == PasswordVerificationResult.Success || verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                isPasswordValid = true;
            }
        }
        catch (FormatException)
        {
            if (user.PasswordHash == request.CurrentPassword)
            {
                isPasswordValid = true;

                user.PasswordHash = passwordHasher.HashPassword(user, request.CurrentPassword);
                await _context.SaveChangesAsync();
            }
        }

        if (!isPasswordValid)
        {
            return BadRequest(new { message = "Current password is incorrect.", status = "error", status_code = 400 });
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Password changed successfully.", status = "success", status_code = 200 });
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        _logger.LogInformation($"Forgot password request for email: {request.Email} at {DateTime.UtcNow}.");

        if (string.IsNullOrEmpty(request.Email))
        {
            return BadRequest(new { message = "Email is required.", status = "error", status_code = 400 });
        }

        if (!IsValidEmail(request.Email))
        {
            return BadRequest(new { message = "Provided email is not in a valid format.", status = "error", status_code = 400 });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted);

        // Security best practice: Avoid revealing whether an email exists or not.
        // Always return a generic success message, regardless of whether the email exists.
        // This prevents email enumeration attacks.
        if (user == null)
        {
            // Return generic success (avoid user enumeration)
            return Ok(new { message = "If an account exists for this email, a password reset link has been sent.", status = "success", status_code = 200 });
        }

        // Generate reset token (short-lived)
        var token = GenerateRefreshToken();
        user.RefreshToken = token;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddHours(1);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        string frontendUrl = _configuration["AppSettings:FrontendUrl"] ?? "https://amplica.in";
        string resetLink = $"{frontendUrl}/reset-password?email={WebUtility.UrlEncode(user.Email!)}&token={WebUtility.UrlEncode(token)}";

        // Replace the problematic line with proper email service usage
        try
        {
            string userNameForEmail = (await _context.CompanyUsers.FirstOrDefaultAsync(cu => cu.UserId == user.UserId))?.FirstName ?? "User";


            string emailBody = _emailTemplateService.GetPasswordResetEmailBody(userNameForEmail, resetLink);
            await _emailSender.SendEmailAsync(user.Email!, "Reset Your BizfreeApp Password", emailBody);
            _logger.LogInformation($"Password reset email sent successfully to {user.Email}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}.", user.Email);
            // It's a security best practice to still return success here to prevent email enumeration.
        }

        return Ok(new { message = "Password reset link sent to your email.", status = "success", status_code = 200 });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        _logger.LogInformation($"Reset password attempt for email: {request.Email}");

        // --- 1. Validation ---
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.NewPassword) || string.IsNullOrEmpty(request.ConfirmPassword))
        {
            return BadRequest(new { message = "Email, token, new password, and confirm password are required.", status = "error", status_code = 400 });
        }
        if (request.NewPassword != request.ConfirmPassword)
        {
            return BadRequest(new { message = "New password and confirm password do not match.", status = "error", status_code = 400 });
        }

        // --- 2. Find User and Validate Token ---
        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Email == request.Email &&
            u.RefreshToken == request.Token &&
            u.RefreshTokenExpiryTime > DateTime.UtcNow &&
            !u.IsDeleted);

        if (user == null)
        {
            _logger.LogWarning($"Reset password failed for email {request.Email}: Invalid or expired reset token.");
            return BadRequest(new { message = "Invalid or expired reset token. Please try requesting a new reset link.", status = "error", status_code = 400 });
        }

        // --- 3. Update Password in Database ---
        user.PasswordHash = request.NewPassword;
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation($"Password was successfully changed in the database for email: {request.Email}.");

        // --- 4. Send Confirmation Email (Best Practice) ---
        try
        {
            string userName = user.CompanyUserUsers.FirstOrDefault()?.FirstName ?? "User";
            string emailBody = _emailTemplateService.GetPasswordChangedConfirmationEmailBody(userName);
            await _emailSender.SendEmailAsync(user.Email!, "Your BizfreeApp Password Has Been Changed", emailBody);
            _logger.LogInformation($"Password change confirmation email sent successfully to {user.Email}.");
        }
        catch (Exception ex)
        {
            // The password was already reset. Do not fail the API call if the email fails. Just log it.
            _logger.LogWarning(ex, "Password was reset successfully, but failed to send the confirmation email to {Email}.", user.Email);
        }

        // --- 5. Return Success Response ---
        return Ok(new { message = "Password has been reset successfully.", status = "success", status_code = 200 });
    }

    // Helper method to validate email format
    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address.Equals(email, StringComparison.InvariantCultureIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}