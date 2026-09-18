namespace BizfreeApp.Services
{
    public interface ITaskNotificationService
    {
        Task CheckAndSendDueTaskNotificationsAsync();
        Task CheckAndSendOverdueTaskNotificationsAsync();
        Task SendTaskAssignmentNotificationAsync(int taskId, int assignedUserId);
        Task SendTaskStatusChangeNotificationAsync(int taskId, string newStatus);
        Task SendTodoStatusChangeNotificationAsync(int todoId, string newStatus, int userId);
        Task SendProjectCreatedNotificationAsync(int projectId, int createdByUserId);
        Task SendProjectMemberAddedNotificationAsync(int projectId, int addedUserId, int addedByUserId);
        Task SendProjectMemberRemovedNotificationAsync(int projectId, int removedUserId, int removedByUserId);
        Task SendProjectDeletedNotificationAsync(int projectId, List<int> memberUserIds, int deletedByUserId);
    }
}
