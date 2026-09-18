// Services/IProjectEmailService.cs
namespace BizfreeApp.Services
{
    public interface IProjectEmailService
    {
        Task SendProjectCreatedNotificationAsync(int projectId, int createdByUserId);
        Task SendProjectDeletedNotificationAsync(int projectId, string projectName, int deletedByUserId, string deletionReason = "");
        Task SendProjectMemberAddedEmailAsync(int projectId, int addedUserId, int addedByUserId);
    }
}
