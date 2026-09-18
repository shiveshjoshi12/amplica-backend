using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BizfreeApp.Services
{
    public class TaskStatusBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TaskStatusBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30); // Check every 30 minutes

        public TaskStatusBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<TaskStatusBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("=== Task Status Background Service STARTED at {Time} ===", DateTime.UtcNow);

            // Initial delay to ensure DB is ready
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("--- Starting delayed task check at {Time} ---", DateTime.UtcNow);
                try
                {
                    await UpdateDelayedTasks();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while updating delayed tasks.");
                }

                _logger.LogInformation("--- Finished delayed task check. Next check in {Minutes} minutes ---",
                    _checkInterval.TotalMinutes);

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task UpdateDelayedTasks()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();

            // Use local time for India (IST = UTC+5:30)
            var istTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istTimeZone);
            var today = DateOnly.FromDateTime(nowIst);

            _logger.LogInformation($"Checking for tasks overdue as of {today} (IST)");

            // Get "Delayed" status for each company
            var companyDelayedStatuses = await context.CompanyTaskStatuses
                .Where(cts => cts.Name.ToLower() == "delayed")
                .Select(cts => new { cts.CompanyId, StatusId = cts.Id })
                .ToListAsync();

            _logger.LogInformation($"Found {companyDelayedStatuses.Count} companies with 'Delayed' status");

            if (!companyDelayedStatuses.Any())
            {
                _logger.LogWarning("No 'Delayed' status found in any company. Skipping delayed task update.");
                return;
            }

            int updatedCount = 0;

            foreach (var companyStatus in companyDelayedStatuses)
            {
                _logger.LogInformation($"Processing Company ID: {companyStatus.CompanyId}, Delayed Status ID: {companyStatus.StatusId}");

                // Find tasks that are overdue (EndDate < today)
                var overdueTasks = await context.Tasks
                    .Include(t => t.StatusNavigation)
                    .Where(t =>
                        t.CompanyId == companyStatus.CompanyId &&
                        t.EndDate.HasValue &&
                        t.EndDate.Value < today &&  // Task ended BEFORE today
                        t.Status != companyStatus.StatusId &&
                        !t.IsDeleted)
                    .ToListAsync();

                _logger.LogInformation($"Found {overdueTasks.Count} overdue tasks for company {companyStatus.CompanyId}");

                foreach (var task in overdueTasks)
                {
                    var currentStatusName = task.StatusNavigation?.Name ?? "Unknown";

                    // Don't change completed or cancelled tasks
                    if (currentStatusName.ToLower() == "completed" ||
                        currentStatusName.ToLower() == "cancelled")
                    {
                        _logger.LogInformation($"Skipping Task {task.TaskId} - Status is '{currentStatusName}'");
                        continue;
                    }

                    var oldStatusId = task.Status;
                    task.Status = companyStatus.StatusId;
                    task.UpdatedAt = DateTimeOffset.UtcNow;
                    updatedCount++;

                    _logger.LogInformation(
                        $"✓ Task {task.TaskId} ({task.Title}) marked as Delayed. " +
                        $"EndDate: {task.EndDate}, OldStatus: {oldStatusId} ({currentStatusName}), " +
                        $"NewStatus: {companyStatus.StatusId} (Delayed)");
                }
            }

            if (updatedCount > 0)
            {
                await context.SaveChangesAsync();
                _logger.LogInformation($"✓✓✓ Successfully updated {updatedCount} tasks to 'Delayed' status ✓✓✓");
            }
            else
            {
                _logger.LogInformation("No overdue tasks found to update.");
            }
        }
    }
}
