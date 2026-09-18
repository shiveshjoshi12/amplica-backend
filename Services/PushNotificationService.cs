using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Data;

namespace BizfreeApp.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PushNotificationService> _logger;

        public PushNotificationService(ApplicationDbContext context, ILogger<PushNotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<string> SendNotificationAsync(string fcmToken, string title, string body, Dictionary<string, string>? data = null)
        {
            var message = new Message()
            {
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Token = fcmToken,
                Data = data ?? new Dictionary<string, string>(),
                Android = new AndroidConfig()
                {
                    Notification = new AndroidNotification()
                    {
                        ClickAction = "FLUTTER_NOTIFICATION_CLICK",
                        Priority = NotificationPriority.HIGH
                    }
                }
            };

            try
            {
                var response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation($"Successfully sent notification: {response}");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending push notification to token: {Token}", fcmToken);
                throw;
            }
        }

        public async Task<BatchResponse> SendNotificationToMultipleTokensAsync(List<string> fcmTokens, string title, string body, Dictionary<string, string>? data = null)
        {
            var messages = fcmTokens.Select(token => new Message()
            {
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Token = token,
                Data = data ?? new Dictionary<string, string>()
            }).ToList();

            try
            {
                var response = await FirebaseMessaging.DefaultInstance.SendAllAsync(messages);
                _logger.LogInformation($"Successfully sent {response.SuccessCount} notifications out of {fcmTokens.Count}");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending batch push notifications");
                throw;
            }
        }

        public async Task<string> SendTaskDueNotificationAsync(string fcmToken, string taskTitle, DateOnly dueDate, int taskId)
        {
            var data = new Dictionary<string, string>
    {
        { "type", "task_due" },
        { "task_id", taskId.ToString() },
        { "due_date", dueDate.ToString("yyyy-MM-dd") }
    };

            return await SendNotificationAsync(
                fcmToken,
                "📅 Task Due Soon",
                $"'{taskTitle}' is due on {dueDate:MMM dd, yyyy}",
                data
            );
        }

        public async Task<string> SendTaskOverdueNotificationAsync(string fcmToken, string taskTitle, DateOnly dueDate, int taskId)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var daysPastDue = today.DayNumber - dueDate.DayNumber;

            var data = new Dictionary<string, string>
    {
        { "type", "task_overdue" },
        { "task_id", taskId.ToString() },
        { "due_date", dueDate.ToString("yyyy-MM-dd") },
        { "days_overdue", daysPastDue.ToString() }
    };

            return await SendNotificationAsync(
                fcmToken,
                "🚨 Task Overdue",
                $"'{taskTitle}' was due {daysPastDue} day(s) ago",
                data
            );
        }

        public async Task SendTaskNotificationToUserAsync(int userId, string title, string body, Dictionary<string, string>? data = null)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId && !string.IsNullOrEmpty(u.FcmToken) && u.IsActive);

            if (user?.FcmToken == null)
            {
                _logger.LogWarning($"No FCM token found for user {userId}");
                return;
            }

            await SendNotificationAsync(user.FcmToken, title, body, data);
        }
    }
}
