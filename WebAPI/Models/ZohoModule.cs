namespace ZohoPowerBIConnector.WebAPI.Models;

public class ZohoModule
{
    public string? Name { get; set; }
    public string? Endpoint { get; set; }
    public string? RecordId { get; set; }
    public string? DataRoot { get; set; }
    public string? StoredProcedure { get; set; }
    // appsettings uses string values for Enabled, accept both
    public string? Enabled { get; set; }

    public bool IsEnabled()
    {
        if (string.IsNullOrWhiteSpace(Enabled)) return false;
        return Enabled.Trim().ToLower() == "true";
    }
}
