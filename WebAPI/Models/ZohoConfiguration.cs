namespace ZohoPowerBIConnector.WebAPI.Models;

public class ZohoConfiguration
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime? LastSyncAt { get; set; }
    public User? User { get; set; }
}
