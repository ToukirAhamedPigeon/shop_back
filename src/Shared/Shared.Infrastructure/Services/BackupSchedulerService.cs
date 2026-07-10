// src/Shared/Shared.Infrastructure/Services/BackupSchedulerService.cs
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using shop_back.src.Shared.Application.DTOs.Backups;
using shop_back.src.Shared.Application.Repositories;
using shop_back.src.Shared.Application.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace shop_back.src.Shared.Infrastructure.Services
{
    public class BackupSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackupSchedulerService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

        public BackupSchedulerService(IServiceProvider serviceProvider, ILogger<BackupSchedulerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BackupSchedulerService is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                await CheckAndRunSchedules(stoppingToken);
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("BackupSchedulerService is stopping.");
        }

        private async Task CheckAndRunSchedules(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
                var scheduleRepo = scope.ServiceProvider.GetRequiredService<IBackupScheduleRepository>();

                var now = DateTime.UtcNow;
                var schedules = await scheduleRepo.GetActiveSchedulesAsync();
                var dueSchedules = schedules.Where(s => s.NextRunAt.HasValue && s.NextRunAt.Value <= now).ToList();

                foreach (var schedule in dueSchedules)
                {
                    _logger.LogInformation($"Running scheduled backup: {schedule.Name} (ID: {schedule.Id})");
                    try
                    {
                        var request = new CreateBackupRequest
                        {
                            Name = $"Auto_{schedule.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                            IsManual = false,
                            StorageDestinations = schedule.StorageDestinations
                        };
                        await backupService.CreateBackupAsync(request, schedule.CreatedBy);

                        // 🔥 Auto Cleanup: Delete old backups based on retention days
                        try
                        {
                            await backupService.CleanupOldBackupsAsync(schedule.RetentionDays, schedule.CreatedBy);
                            _logger.LogInformation($"✅ Cleanup completed: deleted backups older than {schedule.RetentionDays} days");
                        }
                        catch (Exception cleanupEx)
                        {
                            _logger.LogError(cleanupEx, $"❌ Failed to cleanup old backups for schedule {schedule.Name}");
                        }

                        // Update last and next run
                        schedule.LastRunAt = now;
                        schedule.NextRunAt = CalculateNextRun(schedule.CronExpression, now);
                        await scheduleRepo.UpdateAsync(schedule);
                        await scheduleRepo.SaveChangesAsync();

                        _logger.LogInformation($"✅ Scheduled backup {schedule.Name} completed successfully.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"❌ Failed to run scheduled backup {schedule.Name}");
                        // Still advance the schedule to avoid endless retries
                        schedule.LastRunAt = now;
                        schedule.NextRunAt = CalculateNextRun(schedule.CronExpression, now);
                        await scheduleRepo.UpdateAsync(schedule);
                        await scheduleRepo.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error checking schedules.");
            }
        }

        private DateTime? CalculateNextRun(string cronExpression, DateTime baseTime)
        {
            try
            {
                var utcBase = DateTime.SpecifyKind(baseTime, DateTimeKind.Utc);
                var expression = CronExpression.Parse(cronExpression);
                return expression.GetNextOccurrence(utcBase, TimeZoneInfo.Utc);
            }
            catch
            {
                return null;
            }
        }
    }
}