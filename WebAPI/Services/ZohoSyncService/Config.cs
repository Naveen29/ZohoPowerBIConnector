
using Microsoft.Extensions.Configuration;
using ZohoPowerBIConnector.Services.ZohoSyncService.Models;

namespace ZohoPowerBIConnector.Services.ZohoSyncService
{
    public static class Config
    {
        private static IConfiguration? config;

        public static void Initialize(IConfiguration configuration)
        {
            config = configuration;
        }

        // Database
        public static string ConnectionString => config?["Database:ConnectionString"] ?? string.Empty;

        // Sync Settings
        public static int BufferMinutes =>
            int.TryParse(config?["Sync:BufferMinutes"], out int value) ? value : 5;

        //Log Setting
        public static bool LoggingEnabled => bool.TryParse(config?["Logging:Enabled"], out bool value) && value;
        public static string LogFolder => config?["Logging:LogFolder"] ?? "Logs";

        // Zoho Modules
        public static List<ZohoModule> GetModules()
        {
            var modules = new List<ZohoModule>();
            config?.GetSection("ZohoModules").Bind(modules);
            return modules;
        }

        public static List<Client> GetClients(DatabaseService db)
        {
            return db.GetSubscribedClients();
        }
    }
}
