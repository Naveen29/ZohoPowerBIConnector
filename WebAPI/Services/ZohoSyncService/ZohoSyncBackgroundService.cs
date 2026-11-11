using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ZohoPowerBIConnector.Services.ZohoSyncService
{
    /// <summary>
    /// Background service that runs Zoho sync operations periodically
    /// </summary>
    public class ZohoSyncBackgroundService : BackgroundService
    {
        private readonly ILogger<ZohoSyncBackgroundService> _logger;
        private readonly DatabaseService _databaseService;
        private readonly int _syncIntervalMinutes;

        public ZohoSyncBackgroundService(
            ILogger<ZohoSyncBackgroundService> logger,
            DatabaseService databaseService,
            int syncIntervalMinutes = 30)
        {
            _logger = logger;
            _databaseService = databaseService;
            _syncIntervalMinutes = syncIntervalMinutes;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            SyncLogger.Initialize(_logger, Config.LogFolder, Config.LoggingEnabled);

            _logger.LogInformation("Zoho Sync Background Service started. Sync interval: {interval} minutes", _syncIntervalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformSync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Zoho sync background service");
                    SyncLogger.Log(ex, "Error in Zoho sync background service");
                }

                // Wait for the next sync interval
                await Task.Delay(TimeSpan.FromMinutes(_syncIntervalMinutes), stoppingToken);
            }
        }

        private async Task PerformSync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            SyncLogger.Log("Zoho Sync Started");

            var engine = new ZohoSyncEngine(_databaseService);
            var modules = Config.GetModules();
            var clients = Config.GetClients(_databaseService);
            var emailService = new EmailService(_databaseService);

            var today = DateTime.UtcNow.Date;

            

            foreach (var client in clients)
            {

                SyncLogger.Log($"Starting sync for client: {client.Name}- {client.ZohoClientSecret}");
                var expiry = client.SubscriptionEndDate.Date;

                // EXPIRED
                if (expiry < today)
                {
                    SyncLogger.Log($"Client expired: {client.Name}");

                    _databaseService.DisableSubscription(client.Id);

                    // send expired email once per day
                    if (!_databaseService.IsExpiredMailSentToday(client.Id))
                    {
                        try
                        {
                            await emailService.SendAsync(client.Email, "Expired", client);
                            _databaseService.UpdateExpiredMailSent(client.Id);
                        }
                        catch (Exception ex)
                        {
                            SyncLogger.Log(ex, "Failed to send expiry email");
                        }
                    }

                    continue;
                }

                // REMINDER (< 5 days)
                if ((expiry - today).TotalDays <= 5)
                {
                    if (!_databaseService.IsReminderSentToday(client.Id))
                    {
                        try
                        {
                            await emailService.SendAsync(client.Email, "Reminder", client);
                            _databaseService.UpdateReminderSent(client.Id);
                        }
                        catch (Exception ex)
                        {
                            SyncLogger.Log(ex, "Failed to send reminder email");
                        }
                    }
                }

                if (!client.IsActive)
                    continue;

                var api = new ZohoApiClient(client);

                SyncLogger.Log($"==============================");
                SyncLogger.Log($"Client Sync Started: {client.Name}");

                foreach (var module in modules)
                {
                    if (!module.Enabled)
                        continue;

                    try
                    {
                        SyncLogger.Log($"[{client.Name}] Starting module: {module.Name}");

                        await engine.SyncModule(module, client, api);

                        SyncLogger.Log($"[{client.Name}] Completed module: {module.Name}");
                    }
                    catch (Exception ex)
                    {
                        SyncLogger.Log(ex, $"Error in {module.Name} for client {client.Name}");
                    }
                }

                SyncLogger.Log($"Client Sync Completed: {client.Name}");
            }

            stopwatch.Stop();
            SyncLogger.Log($"Zoho Sync Completed in {stopwatch.ElapsedMilliseconds}ms");
            _logger.LogInformation("Zoho sync completed in {elapsed}ms", stopwatch.ElapsedMilliseconds);
        }
    }
}
