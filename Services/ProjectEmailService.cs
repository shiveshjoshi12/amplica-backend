// Services/ProjectEmailService.cs
using BizfreeApp.Data;
using BizfreeApp.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using User = BizfreeApp.Models.User;

namespace BizfreeApp.Services
{
    public class ProjectEmailService : IProjectEmailService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly ILogger<ProjectEmailService> _logger;

        public ProjectEmailService(
            ApplicationDbContext context,
            IEmailSender emailSender,
            IEmailTemplateService emailTemplateService,
            ILogger<ProjectEmailService> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _emailTemplateService = emailTemplateService;
            _logger = logger;
        }

        public async Task SendProjectCreatedNotificationAsync(int projectId, int createdByUserId)
        {
            try
            {
                var projectInfo = await _context.Projects
                    .Include(p => p.CreatedByNavigation)
                        .ThenInclude(u => u.CompanyUserUsers)
                    .Include(p => p.Company)
                    .Where(p => p.ProjectId == projectId && !(p.IsDeleted ?? false))
                    .FirstOrDefaultAsync();

                if (projectInfo == null)
                {
                    _logger.LogWarning($"Project with ID {projectId} not found");
                    return;
                }

                var creatorCompanyUser = projectInfo.CreatedByNavigation?.CompanyUserUsers
                    .FirstOrDefault(cu => cu.CompanyId == projectInfo.CompanyId);

                var createdByName = creatorCompanyUser != null
                    ? $"{creatorCompanyUser.FirstName} {creatorCompanyUser.LastName}".Trim()
                    : projectInfo.CreatedByNavigation?.Email ?? "Unknown User";

                var creator = await _context.Users
                    .Where(u => u.UserId == createdByUserId &&
                                u.IsActive &&
                                !u.IsDeleted)
                    .Select(u => new { u.Email })
                    .FirstOrDefaultAsync();

                if (creator == null || string.IsNullOrEmpty(creator.Email))
                {
                    _logger.LogWarning($"Creator email not found for user {createdByUserId}");
                    return;
                }

                var emailBody = _emailTemplateService.GetProjectCreatedEmailBody(
                    projectInfo.Name ?? "Untitled Project",
                    createdByName,
                    projectInfo.Description ?? "",
                    projectInfo.StartDate?.ToDateTime(TimeOnly.MinValue),
                    projectInfo.EndDate?.ToDateTime(TimeOnly.MinValue)
                );

                var subject = $"Project Created Successfully: {projectInfo.Name}";

                await _emailSender.SendEmailAsync(creator.Email, subject, emailBody);

                _logger.LogInformation(
                    $"Project creation notification sent to creator {creator.Email} for project {projectId}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending project created notification for project {projectId}");
            }
        }

        public async Task SendProjectDeletedNotificationAsync(int projectId, string projectName, int deletedByUserId, string deletionReason = "")
        {
            try
            {
                // Get project details and company info
                var projectInfo = await _context.Projects
                    .Include(p => p.Company)
                    .Where(p => p.ProjectId == projectId)
                    .FirstOrDefaultAsync();

                if (projectInfo == null)
                {
                    _logger.LogWarning($"Project with ID {projectId} not found for deletion email notification");
                    return;
                }

                // Get deleter's info
                var deletedByUser = await _context.Users
                    .Include(u => u.CompanyUserUsers)
                    .FirstOrDefaultAsync(u => u.UserId == deletedByUserId);

                var deletedByCompanyUser = deletedByUser?.CompanyUserUsers
                    .FirstOrDefault(cu => cu.CompanyId == projectInfo.CompanyId);
                var deletedByName = deletedByCompanyUser != null
                    ? $"{deletedByCompanyUser.FirstName} {deletedByCompanyUser.LastName}".Trim()
                    : deletedByUser?.Email ?? "Unknown User";

                var notificationRecipients = await GetCompanyUsersWithPermissionAsync(projectInfo.CompanyId, Permissions.ProjectRead);

                // Generate email body
                var emailBody = _emailTemplateService.GetProjectDeletedEmailBody(
                    projectName,
                    deletedByName,
                    deletionReason
                );

                var subject = $"Project Deleted: {projectName}";

                // Send emails to all recipients
                var emailTasks = notificationRecipients.Select(async user =>
                {
                    try
                    {
                        await _emailSender.SendEmailAsync(user.Email, subject, emailBody);
                        _logger.LogInformation($"Project deletion notification sent to {user.Email} for project {projectName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send project deletion notification to {user.Email} for project {projectName}");
                    }
                });

                await Task.WhenAll(emailTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending project deleted notifications for project {projectId}");
            }
        }
        // Services/ProjectEmailService.cs
        public async Task SendProjectMemberAddedEmailAsync(int projectId, int addedUserId, int addedByUserId)
        {
            try
            {
                var project = await _context.Projects.FindAsync(projectId);
                var addedUser = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == addedUserId);
                var assigner = await _context.Users.Include(u => u.CompanyUserUsers).FirstOrDefaultAsync(u => u.UserId == addedByUserId);

                if (project == null || addedUser == null || assigner == null) return;

                var assignerName = assigner.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId)?.FirstName ?? assigner.Email;

                var body = _emailTemplateService.GetProjectMemberAddedEmailBody(project.Name, assignerName, addedUser.Role?.RoleName);

                await _emailSender.SendEmailAsync(addedUser.Email, $"Added to Project: {project.Name}", body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send project member added email for user {addedUserId}");
            }
        }

        private async Task<List<User>> GetCompanyUsersWithPermissionAsync(int? companyId, string permission)
        {
            if (!companyId.HasValue)
            {
                return new List<User>();
            }

            var companyUsers = await _context.CompanyUsers
                .Where(cu => cu.CompanyId == companyId.Value &&
                             (cu.IsDeleted == false || cu.IsDeleted == null) &&
                             cu.User.IsActive &&
                             !cu.User.IsDeleted)
                .Include(cu => cu.User)
                .Include(cu => cu.CompanyRole)
                .ToListAsync();

            return companyUsers
                .Where(cu => CompanyRoleHasPermission(cu.CompanyRole?.DefaultPermission, permission))
                .Select(cu => cu.User)
                .DistinctBy(u => u.UserId)
                .ToList();
        }

        private static bool CompanyRoleHasPermission(string? serializedPermissions, string permission)
        {
            if (string.IsNullOrWhiteSpace(serializedPermissions))
            {
                return false;
            }

            try
            {
                var permissions = JsonSerializer.Deserialize<string[]>(serializedPermissions);
                return permissions?.Contains(permission, StringComparer.OrdinalIgnoreCase) == true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
