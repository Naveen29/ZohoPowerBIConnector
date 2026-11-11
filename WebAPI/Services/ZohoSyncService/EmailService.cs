using System.Net;
using System.Net.Mail;
using ZohoPowerBIConnector.Services.ZohoSyncService.Models;

namespace ZohoPowerBIConnector.Services.ZohoSyncService
{
    public class EmailService
    {
        private readonly DatabaseService _db;

        public EmailService(DatabaseService db)
        {
            _db = db;
        }

        public async Task SendAsync(string toEmail, string templateType, Client client)
        {
            var config = _db.GetEmailConfig();
            var template = _db.GetEmailTemplate(templateType);

            if (config == null)
                throw new Exception("Email config not found");

            if (template == null)
                throw new Exception($"Email template '{templateType}' not found");

            string body = ReplaceTemplate(template.Body, client);

            using var smtp = new SmtpClient(config.SmtpHost)
            {
                Port = config.SmtpPort,
                Credentials = new NetworkCredential(config.Username, config.Password),
                EnableSsl = config.EnableSSL
            };

            var mail = new MailMessage
            {
                From = new MailAddress(config.FromEmail, config.FromName),
                Subject = template.Subject,
                Body = body,
                IsBodyHtml = true
            };

            mail.To.Add(toEmail);

            await smtp.SendMailAsync(mail);
        }

        private string ReplaceTemplate(string body, Client client)
        {
            return body
                .Replace("{{name}}", client.Name)
                .Replace("{{plan}}", client.PlanName)
                .Replace("{{expiry}}", client.SubscriptionEndDate.ToString("dd-MMM-yyyy"));
        }
    }
}
