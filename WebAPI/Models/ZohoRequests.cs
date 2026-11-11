namespace ZohoPowerBIConnector.WebAPI.Models;

public class ZohoConfigRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    // Optional: request a plan when saving configuration. If the plan is free it will be assigned,
    // otherwise the response will indicate that payment is required (payment flow coming soon).
    public int? PlanId { get; set; }
}

public class ZohoValidateRequest
{
    public string? ClientId { get; set; }
    public string? BaseUrl { get; set; }
    public string? OrganizationId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RefreshToken { get; set; }
}
