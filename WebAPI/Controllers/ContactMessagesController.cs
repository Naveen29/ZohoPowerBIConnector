using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using ZohoPowerBIConnector.WebAPI.Models;

namespace ZohoPowerBIConnector.WebAPI.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class ContactMessagesController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public ContactMessagesController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var messages = await _dbContext.ContactMessages
            .OrderByDescending(m => m.CreatedDate)
            .Select(m => new
            {
                m.Id,
                m.Name,
                m.Email,
                m.Message,
                m.CreatedDate
            })
            .ToListAsync();

        return Ok(messages);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var message = await _dbContext.ContactMessages.FindAsync(id);
        if (message is null)
        {
            return NotFound(new { error = "Message not found." });
        }

        _dbContext.ContactMessages.Remove(message);
        await _dbContext.SaveChangesAsync();
        return Ok(new { message = "Contact message deleted." });
    }

    [HttpPost("{id}/reply")]
    public async Task<IActionResult> Reply(int id, [FromBody] ReplyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { error = "Subject and body are required to reply." });
        }

        var message = await _dbContext.ContactMessages.FindAsync(id);
        if (message is null)
        {
            return NotFound(new { error = "Message not found." });
        }

        var emailConfig = await _dbContext.EmailConfigs.FirstOrDefaultAsync(ec => ec.IsActive);
        if (emailConfig is null)
        {
            return StatusCode(500, new { error = "Email configuration is not available." });
        }

        try
        {
            using var smtpClient = new SmtpClient(emailConfig.SmtpHost, emailConfig.SmtpPort)
            {
                EnableSsl = emailConfig.EnableSSL,
                Credentials = new NetworkCredential(emailConfig.Username, emailConfig.Password)
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(emailConfig.FromEmail, emailConfig.FromName),
                Subject = request.Subject.Trim(),
                Body = request.Body.Trim(),
                IsBodyHtml = false
            };

            mailMessage.To.Add(new MailAddress(message.Email));
            mailMessage.ReplyToList.Add(new MailAddress(emailConfig.FromEmail));
            await smtpClient.SendMailAsync(mailMessage);

            return Ok(new { message = "Reply sent successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Unable to send reply.", details = ex.Message });
        }
    }
}
