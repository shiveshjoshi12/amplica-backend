using Microsoft.EntityFrameworkCore;
using BizfreeApp.Data;

namespace BizfreeApp.Services
{
    public class TaskNotificationService : ITaskNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly ILogger<TaskNotificationService> _logger;
        private readonly IConfiguration _configuration;

        public TaskNotificationService(
            ApplicationDbContext context,
            IPushNotificationService pushNotificationService,
            ILogger<TaskNotificationService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task CheckAndSendDueTaskNotificationsAsync()
        {
            try
            {
                var dueSoonDays = _configuration.GetValue<int>("TaskNotifications:DueSoonDays", 1);
                var enableDueNotifications = _configuration.GetValue<bool>("TaskNotifications:EnableDueNotifications", true);

                if (!enableDueNotifications)
                {
                    _logger.LogInformation("Due task notifications are disabled");
                    return;
                }

                // Convert to DateOnly for proper comparison
                var targetDate = DateOnly.FromDateTime(DateTime.Now).AddDays(dueSoonDays);

                // Get tasks that are due soon
                var dueTasks = await _context.Tasks
                    .Where(t => t.EndDate.HasValue &&
                               t.EndDate.Value == targetDate && // Now comparing DateOnly with DateOnly
                               t.AssignedTo.HasValue &&
                               !t.IsDeleted)
                    .Join(_context.Users,
                          task => task.AssignedTo,
                          user => user.UserId,
                          (task, user) => new { Task = task, User = user })
                    .Where(tu => !string.IsNullOrEmpty(tu.User.FcmToken))
                    .ToListAsync();

                _logger.LogInformation($"Found {dueTasks.Count} due tasks");

                // Send notifications
                foreach (var taskUser in dueTasks)
                {
                    try
                    {
                        await _pushNotificationService.SendTaskDueNotificationAsync(
                            taskUser.User.FcmToken!,
                            taskUser.Task.Title,
                            taskUser.Task.EndDate!.Value, // Convert DateOnly to DateTime
                            taskUser.Task.TaskId
                        );

                        _logger.LogInformation($"Sent due notification for task {taskUser.Task.TaskId} to user {taskUser.Task.AssignedTo}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send due notification for task {taskUser.Task.TaskId}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CheckAndSendDueTaskNotificationsAsync");
            }
        }
        public async Task CheckAndSendOverdueTaskNotificationsAsync()
        {
            try
            {
                var enableOverdueNotifications = _configuration.GetValue<bool>("TaskNotifications:EnableOverdueNotifications", true);

                if (!enableOverdueNotifications)
                {
                    _logger.LogInformation("Overdue task notifications are disabled");
                    return;
                }

                // Convert to DateOnly for proper comparison
                var today = DateOnly.FromDateTime(DateTime.Now);

                // Get overdue tasks
                var overdueTasks = await _context.Tasks
                    .Where(t => t.EndDate.HasValue &&
                               t.EndDate.Value < today && // Now comparing DateOnly with DateOnly
                               t.AssignedTo.HasValue &&
                               !t.IsDeleted)
                    .Join(_context.Users,
                          task => task.AssignedTo,
                          user => user.UserId,
                          (task, user) => new { Task = task, User = user })
                    .Where(tu => !string.IsNullOrEmpty(tu.User.FcmToken))
                    .ToListAsync();

                _logger.LogInformation($"Found {overdueTasks.Count} overdue tasks");

                // Send notifications
                foreach (var taskUser in overdueTasks)
                {
                    try
                    {
                        await _pushNotificationService.SendTaskOverdueNotificationAsync(
                            taskUser.User.FcmToken!,
                            taskUser.Task.Title,
                            taskUser.Task.EndDate!.Value, // Convert DateOnly to DateTime
                            taskUser.Task.TaskId
                        );

                        _logger.LogInformation($"Sent overdue notification for task {taskUser.Task.TaskId} to user {taskUser.Task.AssignedTo}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send overdue notification for task {taskUser.Task.TaskId}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CheckAndSendOverdueTaskNotificationsAsync");
            }
        }

        public async Task SendTaskAssignmentNotificationAsync(int taskId, int assignedUserId)
        {
            try
            {
                var task = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
                if (task == null) return;

                var data = new Dictionary<string, string>
                 {
                     { "type", "task_assigned" },
                     { "task_id", taskId.ToString() }
                 };

                await _pushNotificationService.SendTaskNotificationToUserAsync(
                    assignedUserId,
                    "📋 New Task Assigned",
                    $"You've been assigned: '{task.Title}'",
                    data
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending task assignment notification for task {taskId}");
            }
        }

        public async Task SendTaskStatusChangeNotificationAsync(int taskId, string newStatus)
        {
            try
            {
                var task = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
                if (task?.AssignedTo == null) return;

                var data = new Dictionary<string, string>
                 {
                     { "type", "task_status_change" },
                     { "task_id", taskId.ToString() },
                     { "new_status", newStatus }
                 };

                await _pushNotificationService.SendTaskNotificationToUserAsync(
                    task.AssignedTo.Value,
                    "🔄 Task Status Updated",
                    $"'{task.Title}' status changed to: {newStatus}",
                    data
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending task status change notification for task {taskId}");
            }
        }
        public async Task SendTodoStatusChangeNotificationAsync(int todoId, string newStatus, int userId)
        {
            try
            {
                var todo = await _context.TaskTodos.FirstOrDefaultAsync(t => t.TaskTodoId == todoId);
                if (todo == null) return;

                var data = new Dictionary<string, string>
         {
             { "type", "todo_status_change" },
             { "todo_id", todoId.ToString() },
             { "new_status", newStatus }
         };

                await _pushNotificationService.SendTaskNotificationToUserAsync(
                    userId,
                    "✅ Todo Status Updated",
                    $"Your todo '{todo.Title}' status changed to: {newStatus}",
                    data
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending todo status change notification for todo {todoId}");
            }
        }

        public async Task SendProjectCreatedNotificationAsync(int projectId, int createdByUserId)
        {
            try
            {
                var project = await _context.Projects
     .Include(p => p.ProjectMembers.Where(pm => !(pm.IsDeleted ?? false)))
     .FirstOrDefaultAsync(p => p.ProjectId == projectId);

                if (project == null) return;

                // Get all project members except the creator
                var memberUserIds = project.ProjectMembers
                    .Where(pm => pm.UserId != createdByUserId)
                    .Select(pm => pm.UserId)
                    .ToList();

                var data = new Dictionary<string, string>
         {
             { "type", "project_created" },
             { "project_id", projectId.ToString() },
             { "action", "open_project" }
         };

                // Send notification to all project members
                foreach (var userId in memberUserIds)
                {
                    try
                    {
                        await _pushNotificationService.SendTaskNotificationToUserAsync(
                            userId,
                            "🚀 New Project Created",
                            $"New project '{project.Name}' has been created",
                            data
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send project created notification to user {UserId}", userId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending project created notification for project {ProjectId}", projectId);
            }
        }

        public async Task SendProjectMemberAddedNotificationAsync(int projectId, int addedUserId, int addedByUserId)
        {
            try
            {
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId);
                if (project == null) return;

                var data = new Dictionary<string, string>
         {
             { "type", "project_member_added" },
             { "project_id", projectId.ToString() },
             { "action", "open_project" }
         };

                // Notify the added user
                await _pushNotificationService.SendTaskNotificationToUserAsync(
                    addedUserId,
                    "👥 Added to Project",
                    $"You've been added to project '{project.Name}'",
                    data
                );

                // Notify other project members (except the one who added and the newly added user)
                var otherMembers = await _context.ProjectMembers
                    .Where(pm => pm.ProjectId == projectId &&
                                 pm.UserId != addedUserId &&
                                 pm.UserId != addedByUserId &&
                                 !pm.IsDeleted.GetValueOrDefault())
                    .Select(pm => pm.UserId)
                    .ToListAsync();

                foreach (var userId in otherMembers)
                {
                    try
                    {
                        await _pushNotificationService.SendTaskNotificationToUserAsync(
                            userId,
                            "👥 New Team Member",
                            $"A new member has been added to project '{project.Name}'",
                            data
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send member added notification to user {UserId}", userId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending project member added notification");
            }
        }

        public async Task SendProjectMemberRemovedNotificationAsync(int projectId, int removedUserId, int removedByUserId)
        {
            try
            {
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId);
                if (project == null) return;

                var data = new Dictionary<string, string>
         {
             { "type", "project_member_removed" },
             { "project_id", projectId.ToString() }
         };

                // Notify the removed user
                await _pushNotificationService.SendTaskNotificationToUserAsync(
                    removedUserId,
                    "👥 Removed from Project",
                    $"You've been removed from project '{project.Name}'",
                    data
                );

                // Notify remaining project members
                var remainingMembers = await _context.ProjectMembers
                    .Where(pm => pm.ProjectId == projectId &&
                                 pm.UserId != removedUserId &&
                                 pm.UserId != removedByUserId &&
                                 !pm.IsDeleted.GetValueOrDefault())
                    .Select(pm => pm.UserId)
                    .ToListAsync();

                foreach (var userId in remainingMembers)
                {
                    try
                    {
                        await _pushNotificationService.SendTaskNotificationToUserAsync(
                            userId,
                            "👥 Team Member Removed",
                            $"A member has been removed from project '{project.Name}'",
                            data
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send member removed notification to user {UserId}", userId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending project member removed notification");
            }
        }

        public async Task SendProjectDeletedNotificationAsync(int projectId, List<int> memberUserIds, int deletedByUserId)
        {
            try
            {
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId);
                if (project == null) return;

                var data = new Dictionary<string, string>
         {
             { "type", "project_deleted" },
             { "project_id", projectId.ToString() }
         };

                // Notify all project members except the one who deleted it
                foreach (var userId in memberUserIds.Where(id => id != deletedByUserId))
                {
                    try
                    {
                        await _pushNotificationService.SendTaskNotificationToUserAsync(
                            userId,
                            "🗑️ Project Deleted",
                            $"Project '{project.Name}' has been deleted",
                            data
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send project deleted notification to user {UserId}", userId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending project deleted notification");
            }
        }

    }
}
