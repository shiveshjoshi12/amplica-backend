// Services/TaskEmailService.cs
using BizfreeApp.Data;
using BizfreeApp.Constants;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;
using User = BizfreeApp.Models.User;

namespace BizfreeApp.Services
{
    public class TaskEmailService : ITaskEmailService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly ILogger<TaskEmailService> _logger;

        public TaskEmailService(ApplicationDbContext context, IEmailSender emailSender, IEmailTemplateService emailTemplateService, ILogger<TaskEmailService> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _emailTemplateService = emailTemplateService;
            _logger = logger;
        }

        public async Task SendTaskCreatedEmailAsync(int taskId, int createdByUserId)
        {
            var task = await _context.Tasks.Include(t => t.Priority).FirstOrDefaultAsync(t => t.TaskId == taskId);
            var creator = await _context.Users.Include(u => u.CompanyUserUsers).FirstOrDefaultAsync(u => u.UserId == createdByUserId);

            if (task == null || creator == null) return;

            var creatorName = creator.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == task.CompanyId)?.FirstName ?? creator.Email;
            var body = _emailTemplateService.GetTaskCreatedEmailBody(task.Title, creatorName, task.Priority?.Name, task.EndDate);

            var admins = await GetCompanyUsersWithPermissionAsync(task.CompanyId, Permissions.TaskReadAll);
            var emailTasks = admins.Select(admin => _emailSender.SendEmailAsync(admin.Email, $"New Task Created: {task.Title}", body));
            await Task.WhenAll(emailTasks);
        }

        public async Task SendTaskAssignedEmailAsync(int taskId, int assignedUserId, int assignedByUserId)
        {
            var task = await _context.Tasks.Include(t => t.Priority).FirstOrDefaultAsync(t => t.TaskId == taskId);
            var assignee = await _context.Users.FirstOrDefaultAsync(u => u.UserId == assignedUserId);
            var assigner = await _context.Users.Include(u => u.CompanyUserUsers).FirstOrDefaultAsync(u => u.UserId == assignedByUserId);

            if (task == null || assignee == null || assigner == null) return;

            var assignerName = assigner.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == task.CompanyId)?.FirstName ?? assigner.Email;
            var body = _emailTemplateService.GetTaskAssignedEmailBody(task.Title, assignerName, task.Priority?.Name, task.EndDate, task.Description);

            await _emailSender.SendEmailAsync(assignee.Email, $"New Task Assigned: {task.Title}", body);
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
