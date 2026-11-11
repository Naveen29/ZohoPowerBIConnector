using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZohoPowerBIConnector.Services.ZohoSyncService.Models
{
    public class ZohoModule
    {
        public string? Name { get; set; }
        public string? Endpoint { get; set; }
        public string? RecordId { get; set; }
        public string? DataRoot { get; set; }
        public string? StoredProcedure { get; set; }
        public bool Enabled { get; set; }
    }
}
