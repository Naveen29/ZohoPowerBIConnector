using System.ComponentModel.DataAnnotations.Schema;

namespace ZohoPowerBIConnector.WebAPI.Models;

[Table("EmailConfig")]
public class EmailConfig
{
    public int Id { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSSL { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
