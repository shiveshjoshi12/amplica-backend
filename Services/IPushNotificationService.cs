using FirebaseAdmin.Messaging;

namespace BizfreeApp.Services
{
    public interface IPushNotificationService
    {
        Task<string> SendNotificationAsync(string fcmToken, string title, string body, Dictionary<string, string>? data = null);
        Task<BatchResponse> SendNotificationToMultipleTokensAsync(List<string> fcmTokens, string title, string body, Dictionary<string, string>? data = null);
        Task<string> SendTaskDueNotificationAsync(string fcmToken, string taskTitle, DateOnly dueDate, int taskId);
        Task<string> SendTaskOverdueNotificationAsync(string fcmToken, string taskTitle, DateOnly dueDate, int taskId);
        Task SendTaskNotificationToUserAsync(int userId, string title, string body, Dictionary<string, string>? data = null);
    }
}
