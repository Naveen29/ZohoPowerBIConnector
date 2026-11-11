using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZohoPowerBIConnector.Services.ZohoSyncService.Models
{
    public class EmailConfig
    {
        public string? SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool EnableSSL { get; set; }
        public string? FromEmail { get; set; }
        public string? FromName { get; set; }
    }
}
