namespace BizfreeApp.Services
{
    public interface ITaskEmailService
    {
        Task SendTaskCreatedEmailAsync(int taskId, int createdByUserId);
        Task SendTaskAssignedEmailAsync(int taskId, int assignedUserId, int assignedByUserId);
    }
}