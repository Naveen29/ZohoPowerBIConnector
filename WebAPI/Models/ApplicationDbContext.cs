using Microsoft.EntityFrameworkCore;

namespace ZohoPowerBIConnector.WebAPI.Models;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<ZohoConfiguration> ZohoConfigurations => Set<ZohoConfiguration>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<EmailConfig> EmailConfigs => Set<EmailConfig>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    // Zoho synced module tables
    public DbSet<ZohoItems> ZohoItems => Set<ZohoItems>();
    public DbSet<ZohoInventoryAdjustments> ZohoInventoryAdjustments => Set<ZohoInventoryAdjustments>();
    public DbSet<ZohoCustomers> ZohoCustomers => Set<ZohoCustomers>();
    public DbSet<ZohoSalesOrders> ZohoSalesOrders => Set<ZohoSalesOrders>();
    public DbSet<ZohoInvoices> ZohoInvoices => Set<ZohoInvoices>();
    public DbSet<ZohoInvoiceLineItems> ZohoInvoiceLineItems => Set<ZohoInvoiceLineItems>();
    public DbSet<ZohoPaymentsReceived> ZohoPaymentsReceived => Set<ZohoPaymentsReceived>();
    public DbSet<ZohoCreditNotes> ZohoCreditNotes => Set<ZohoCreditNotes>();
    public DbSet<ZohoVendors> ZohoVendors => Set<ZohoVendors>();
    public DbSet<ZohoPurchaseOrders> ZohoPurchaseOrders => Set<ZohoPurchaseOrders>();
    public DbSet<ZohoBills> ZohoBills => Set<ZohoBills>();
    public DbSet<ZohoPaymentsMade> ZohoPaymentsMade => Set<ZohoPaymentsMade>();
    public DbSet<ZohoVendorCredits> ZohoVendorCredits => Set<ZohoVendorCredits>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ensure email is unique at the database level to prevent duplicates under concurrency.
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
    }
}
