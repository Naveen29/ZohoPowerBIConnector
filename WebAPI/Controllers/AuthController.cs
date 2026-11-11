using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ZohoPowerBIConnector.WebAPI.Models;
using System.Net.Mail;

namespace ZohoPowerBIConnector.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public AuthController(ApplicationDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Email and password are required." });
        }

        var user = _dbContext.Users.SingleOrDefault(u => u.Email == request.Email);
        if (user is null)
        {
            return Unauthorized(new { error = "Invalid credentials." });
        }

        if (!user.IsEmailConfirmed)
        {
            return Unauthorized(new { error = "Email not verified. Please verify your email before logging in." });
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { error = "Invalid credentials." });
        }

        user.LastLoginUtc = DateTime.UtcNow;
        _dbContext.SaveChanges();

        var token = CreateJwtToken(user);
        return Ok(new { token, user = new { user.Id, user.Email, user.FullName } });
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Email and password are required." });
        }

        if (_dbContext.Users.Any(u => u.Email == request.Email))
        {
            return Conflict(new { error = "A user with this email already exists." });
        }

        var user = new User
        {
            Email = request.Email,
            FullName = request.FullName ?? string.Empty,
            IsEmailConfirmed = false
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        // generate OTP
        user.EmailConfirmationToken = new Random().Next(100000, 999999).ToString();
        user.EmailConfirmationExpiresUtc = DateTime.UtcNow.AddMinutes(10);

        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();

        // send OTP email
        try
        {
            var emailConfig = _dbContext.Set<EmailConfig>().FirstOrDefault(ec => ec.IsActive);
            if (emailConfig is null)
            {
                return StatusCode(500, new { error = "Email configuration is not available to send verification OTP." });
            }

            using var smtpClient = new SmtpClient(emailConfig.SmtpHost, emailConfig.SmtpPort)
            {
                EnableSsl = emailConfig.EnableSSL,
                Credentials = new System.Net.NetworkCredential(emailConfig.Username, emailConfig.Password)
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(emailConfig.FromEmail, emailConfig.FromName),
                Subject = "Verify your email",
                Body = $"Your verification code is: {user.EmailConfirmationToken}\r\nThis code will expire in 10 minutes.",
                IsBodyHtml = false
            };
            mailMessage.To.Add(user.Email);

            smtpClient.Send(mailMessage);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Unable to send verification email.", details = ex.Message });
        }

        return Ok(new { message = "Verification OTP sent to email. Please verify to complete registration." });
    }

    [HttpPost("verify-email")]
    public IActionResult VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp))
        {
            return BadRequest(new { error = "Email and OTP are required." });
        }

        var user = _dbContext.Users.SingleOrDefault(u => u.Email == request.Email);
        if (user is null)
        {
            return NotFound(new { error = "User not found." });
        }

        if (user.IsEmailConfirmed)
        {
            return BadRequest(new { error = "Email already verified." });
        }

        if (user.EmailConfirmationToken != request.Otp || user.EmailConfirmationExpiresUtc is null || user.EmailConfirmationExpiresUtc < DateTime.UtcNow)
        {
            return Unauthorized(new { error = "Invalid or expired OTP." });
        }

        user.IsEmailConfirmed = true;
        user.EmailConfirmationToken = null;
        user.EmailConfirmationExpiresUtc = null;
        _dbContext.SaveChanges();

        return Ok(new { message = "Email verified. You can now log in." });
    }

    [HttpPost("resend-otp")]
    public IActionResult ResendOtp([FromBody] ResendOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error = "Email is required." });
        }

        var user = _dbContext.Users.SingleOrDefault(u => u.Email == request.Email);
        if (user is null)
        {
            return NotFound(new { error = "User not found." });
        }

        if (user.IsEmailConfirmed)
        {
            return BadRequest(new { error = "Email already verified." });
        }

        user.EmailConfirmationToken = new Random().Next(100000, 999999).ToString();
        user.EmailConfirmationExpiresUtc = DateTime.UtcNow.AddMinutes(10);
        _dbContext.SaveChanges();

        try
        {
            var emailConfig = _dbContext.Set<EmailConfig>().FirstOrDefault(ec => ec.IsActive);
            if (emailConfig is null)
            {
                return StatusCode(500, new { error = "Email configuration is not available to send verification OTP." });
            }

            using var smtpClient = new SmtpClient(emailConfig.SmtpHost, emailConfig.SmtpPort)
            {
                EnableSsl = emailConfig.EnableSSL,
                Credentials = new System.Net.NetworkCredential(emailConfig.Username, emailConfig.Password)
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(emailConfig.FromEmail, emailConfig.FromName),
                Subject = "Verify your email - OTP",
                Body = $"Your verification code is: {user.EmailConfirmationToken}\r\nThis code will expire in 10 minutes.",
                IsBodyHtml = false
            };
            mailMessage.To.Add(user.Email);

            smtpClient.Send(mailMessage);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Unable to send verification email.", details = ex.Message });
        }

        return Ok(new { message = "Verification OTP resent to email." });
    }

    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error = "Email is required." });
        }

        var user = _dbContext.Users.SingleOrDefault(u => u.Email == request.Email);
        if (user is null)
        {
            return Ok(new { message = "If that account exists, reset instructions were sent." });
        }

        user.PasswordResetToken = Guid.NewGuid().ToString("N");
        user.PasswordResetExpiresUtc = DateTime.UtcNow.AddMinutes(30);
        _dbContext.SaveChanges();

        return Ok(new { message = "A reset token has been generated.", resetToken = user.PasswordResetToken });
    }

    [HttpPost("reset-password")]
    public IActionResult ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { error = "Email, token, and new password are required." });
        }

        var user = _dbContext.Users.SingleOrDefault(u => u.Email == request.Email);
        if (user is null || user.PasswordResetToken != request.Token || user.PasswordResetExpiresUtc is null || user.PasswordResetExpiresUtc < DateTime.UtcNow)
        {
            return Unauthorized(new { error = "Invalid or expired reset token." });
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpiresUtc = null;
        _dbContext.SaveChanges();

        return Ok(new { message = "Password updated successfully." });
    }

    private string CreateJwtToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? "ReplaceWithStrongSecretKeyForDevelopment";
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "ZohoPowerBIConnector";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "ZohoPowerBIConnectorClients";
        var authClaims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("userId", user.Id.ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.Role))
        {
            authClaims.Add(new Claim(ClaimTypes.Role, user.Role));
        }

        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            expires: DateTime.UtcNow.AddHours(1),
            claims: authClaims,
            signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
