using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZohoPowerBIConnector.Services.ZohoSyncService.Models
{
    public class Client
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ZohoClientId { get; set; }
        public string? ZohoClientSecret { get; set; }
        public string? ZohoRefreshToken { get; set; }
        public string? OrganizationId { get; set; }
        public string? ApiBaseUrl { get; set; }
        public bool IsActive { get; set; }

        public DateTime SubscriptionEndDate { get; set; }
        public string? Email { get; set; }
        public string? PlanName { get; set; }
    }
}
