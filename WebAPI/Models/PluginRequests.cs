namespace ZohoPowerBIConnector.WebAPI.Models;

public class PluginValidateRequest
{
    public string Email { get; set; } = string.Empty;
}

public class PluginValidateResponse
{
    public bool IsValid { get; set; }
    public string? PlanName { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Message { get; set; }
    public bool HasZohoConfig { get; set; }
}
