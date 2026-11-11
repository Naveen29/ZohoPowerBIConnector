using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZohoPowerBIConnector.WebAPI.Models;

// Base properties shared by all synced Zoho module tables
public abstract class ZohoSyncBase
{
    [Key]
    public int Id { get; set; }

    // Tenant / Customer key
    [Required]
    [MaxLength(200)]
    public string ClientId { get; set; } = string.Empty;

    // Original Zoho record id
    [MaxLength(200)]
    public string? ZohoRecordId { get; set; }

    // Raw JSON payload as stored
    public string? DataJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
}

// Individual module tables (lightweight scaffolds)
[Table("ZohoItems")]
public class ZohoItems : ZohoSyncBase { }

[Table("ZohoInventoryAdjustments")]
public class ZohoInventoryAdjustments : ZohoSyncBase { }

[Table("ZohoCustomers")]
public class ZohoCustomers : ZohoSyncBase { }

[Table("ZohoSalesOrders")]
public class ZohoSalesOrders : ZohoSyncBase { }

[Table("ZohoInvoices")]
public class ZohoInvoices : ZohoSyncBase { }

[Table("ZohoInvoiceLineItems")]
public class ZohoInvoiceLineItems : ZohoSyncBase { }

[Table("ZohoPaymentsReceived")]
public class ZohoPaymentsReceived : ZohoSyncBase { }

[Table("ZohoCreditNotes")]
public class ZohoCreditNotes : ZohoSyncBase { }

[Table("ZohoVendors")]
public class ZohoVendors : ZohoSyncBase { }

[Table("ZohoPurchaseOrders")]
public class ZohoPurchaseOrders : ZohoSyncBase { }

[Table("ZohoBills")]
public class ZohoBills : ZohoSyncBase { }

[Table("ZohoPaymentsMade")]
public class ZohoPaymentsMade : ZohoSyncBase { }

[Table("ZohoVendorCredits")]
public class ZohoVendorCredits : ZohoSyncBase { }
