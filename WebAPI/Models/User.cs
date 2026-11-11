using Microsoft.EntityFrameworkCore;

namespace ZohoPowerBIConnector.WebAPI.Models;

[Index(nameof(Email), IsUnique = true)]
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsEmailConfirmed { get; set; } = false;
    public string? EmailConfirmationToken { get; set; }
    public DateTime? EmailConfirmationExpiresUtc { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiresUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public List<Subscription> Subscriptions { get; set; } = new();
    public ZohoConfiguration? ZohoConfiguration { get; set; }
}
