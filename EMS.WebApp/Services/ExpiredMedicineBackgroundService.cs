using EMS.WebApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EMS.WebApp.Services
{
    public class ExpiredMedicineBackgroundService : BackgroundService
    {
        private readonly ILogger<ExpiredMedicineBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _period = TimeSpan.FromHours(6); // Run every 6 hours

        public ExpiredMedicineBackgroundService(
            ILogger<ExpiredMedicineBackgroundService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Expired Medicine Background Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncExpiredMedicinesAsync();

                    _logger.LogInformation("Expired Medicine sync completed. Next sync in {Period}.", _period);

                    await Task.Delay(_period, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Expired Medicine Background Service is stopping.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during expired medicine sync.");

                    // Wait 30 minutes before retrying on error
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }
            }
        }

        private async Task SyncExpiredMedicinesAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var expiredMedicineRepo = scope.ServiceProvider.GetRequiredService<IExpiredMedicineRepository>();

                var newExpiredMedicines = await expiredMedicineRepo.DetectNewExpiredMedicinesAsync("System Auto-Sync");

                if (newExpiredMedicines.Any())
                {
                    foreach (var expiredMedicine in newExpiredMedicines)
                    {
                        await expiredMedicineRepo.AddAsync(expiredMedicine);
                    }

                    _logger.LogInformation("Auto-sync detected and added {Count} new expired medicines.", newExpiredMedicines.Count);
                }
                else
                {
                    _logger.LogInformation("Auto-sync completed. No new expired medicines detected.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during expired medicine sync operation.");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Expired Medicine Background Service is stopping...");
            await base.StopAsync(stoppingToken);
        }
    }

    // Configuration class for background service settings
    public class ExpiredMedicineBackgroundServiceOptions
    {
        public const string SectionName = "ExpiredMedicineBackgroundService";

        public bool Enabled { get; set; } = true;
        public int SyncIntervalHours { get; set; } = 6;
        public int RetryDelayMinutes { get; set; } = 30;
        public bool NotifyOnNewExpired { get; set; } = true;
        public string[]? NotificationEmails { get; set; }
    }
}

// Enhanced version with configuration support
namespace EMS.WebApp.Services
{
    public class ConfigurableExpiredMedicineBackgroundService : BackgroundService
    {
        private readonly ILogger<ConfigurableExpiredMedicineBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ExpiredMedicineBackgroundServiceOptions _options;

        public ConfigurableExpiredMedicineBackgroundService(
            ILogger<ConfigurableExpiredMedicineBackgroundService> logger,
            IServiceProvider serviceProvider,
            Microsoft.Extensions.Options.IOptions<ExpiredMedicineBackgroundServiceOptions> options)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Expired Medicine Background Service is disabled in configuration.");
                return;
            }

            var period = TimeSpan.FromHours(_options.SyncIntervalHours);
            _logger.LogInformation("Expired Medicine Background Service started with {Period} interval.", period);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var syncResult = await SyncExpiredMedicinesAsync();

                    if (syncResult.NewItemsCount > 0 && _options.NotifyOnNewExpired)
                    {
                        await SendNotificationAsync(syncResult);
                    }

                    _logger.LogInformation("Sync completed. Found {Count} new expired medicines. Next sync in {Period}.",
                        syncResult.NewItemsCount, period);

                    await Task.Delay(period, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Expired Medicine Background Service is stopping.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during expired medicine sync.");

                    var retryDelay = TimeSpan.FromMinutes(_options.RetryDelayMinutes);
                    _logger.LogInformation("Retrying in {Delay}.", retryDelay);
                    await Task.Delay(retryDelay, stoppingToken);
                }
            }
        }

        private async Task<SyncResult> SyncExpiredMedicinesAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var expiredMedicineRepo = scope.ServiceProvider.GetRequiredService<IExpiredMedicineRepository>();

            var newExpiredMedicines = await expiredMedicineRepo.DetectNewExpiredMedicinesAsync("System Auto-Sync");

            if (newExpiredMedicines.Any())
            {
                foreach (var expiredMedicine in newExpiredMedicines)
                {
                    await expiredMedicineRepo.AddAsync(expiredMedicine);
                }
            }

            return new SyncResult
            {
                NewItemsCount = newExpiredMedicines.Count,
                SyncTime = DateTime.Now,
                NewItems = newExpiredMedicines.ToList()
            };
        }

        private async Task SendNotificationAsync(SyncResult syncResult)
        {
            try
            {
                // Implement notification logic here
                // This could send emails, create alerts, etc.
                _logger.LogInformation("Notification: {Count} new expired medicines detected at {Time}",
                    syncResult.NewItemsCount, syncResult.SyncTime);

                // Example: Send email notification
                if (_options.NotificationEmails?.Any() == true)
                {
                    // Implement email sending logic
                    _logger.LogInformation("Email notifications would be sent to: {Emails}",
                        string.Join(", ", _options.NotificationEmails));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notifications for expired medicines.");
            }
        }

        private class SyncResult
        {
            public int NewItemsCount { get; set; }
            public DateTime SyncTime { get; set; }
            public List<Data.ExpiredMedicine> NewItems { get; set; } = new();
        }
    }
}