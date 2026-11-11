using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZohoPowerBIConnector.WebAPI.Models;

namespace ZohoPowerBIConnector.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public ContactController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] ContactMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Name, email and message are required." });
        }

        var emailConfig = await _dbContext.Set<EmailConfig>().FirstOrDefaultAsync(ec => ec.IsActive);
        if (emailConfig is null)
        {
            return StatusCode(500, new { error = "Email configuration is not available." });
        }

        try
        {
            using var smtpClient = new SmtpClient(emailConfig.SmtpHost, emailConfig.SmtpPort)
            {
                EnableSsl = emailConfig.EnableSSL,
                Credentials = new System.Net.NetworkCredential(emailConfig.Username, emailConfig.Password)
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(emailConfig.FromEmail, emailConfig.FromName),
                Subject = $"Contact Form Message from {request.Name}",
                Body = $"Name: {request.Name}\r\nEmail: {request.Email}\r\n\r\nMessage:\r\n{request.Message}",
                IsBodyHtml = false
            };
            mailMessage.To.Add(emailConfig.FromEmail);
            mailMessage.ReplyToList.Add(new MailAddress(request.Email));

            await smtpClient.SendMailAsync(mailMessage);

            var contactMessage = new ContactMessage
            {
                Name = request.Name,
                Email = request.Email,
                Message = request.Message,
                CreatedDate = DateTime.UtcNow
            };

            _dbContext.Set<ContactMessage>().Add(contactMessage);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "Your message has been sent successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Unable to send message.", details = ex.Message });
        }
    }
}
