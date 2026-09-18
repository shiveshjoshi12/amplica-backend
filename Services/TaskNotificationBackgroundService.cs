namespace BizfreeApp.Services
{
    public class TaskNotificationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TaskNotificationBackgroundService> _logger;
        private readonly IConfiguration _configuration;

        public TaskNotificationBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<TaskNotificationBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var checkIntervalMinutes = _configuration.GetValue<int>("TaskNotifications:CheckIntervalMinutes", 60);
            var interval = TimeSpan.FromMinutes(checkIntervalMinutes);

            _logger.LogInformation($"Task notification service starting with {checkIntervalMinutes} minute intervals");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var taskNotificationService = scope.ServiceProvider.GetRequiredService<ITaskNotificationService>();

                    _logger.LogInformation("Checking for due and overdue tasks...");

                    await taskNotificationService.CheckAndSendDueTaskNotificationsAsync();
                    await taskNotificationService.CheckAndSendOverdueTaskNotificationsAsync();

                    _logger.LogInformation("Task notification check completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in task notification background service");
                }

                await Task.Delay(interval, stoppingToken);
            }
        }
    }
}
