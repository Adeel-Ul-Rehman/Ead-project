using attendence.Services.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace attendence.Services.Services;

public class EmailService
{
    private readonly EmailSettings _emailSettings;

    public EmailService(IOptions<EmailSettings> emailSettings)
    {
        _emailSettings = emailSettings.Value;
    }

    /// <summary>
    /// Send a single email
    /// </summary>
    public async Task<bool> SendEmailAsync(string toEmail, string toName, string subject, string htmlBody, string? plainTextBody = null)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = plainTextBody ?? StripHtml(htmlBody)
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            // Connect to SMTP server
            await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, SecureSocketOptions.StartTls);
            
            // Authenticate
            await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
            
            // Send email
            await client.SendAsync(message);
            
            // Disconnect
            await client.DisconnectAsync(true);

            return true;
        }
        catch (Exception ex)
        {
            // Log the error (you can inject ILogger here)
            Console.WriteLine($"Email sending failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Send credentials email to a user
    /// </summary>
    public async Task<bool> SendCredentialsEmailAsync(string toEmail, string fullName, string password, string role, string loginUrl, string? sectionName = null)
    {
        var subject = "Your University Attendance System Credentials";
        
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
        .credentials-box {{ background: white; border-left: 4px solid #667eea; padding: 20px; margin: 20px 0; border-radius: 5px; }}
        .credential-row {{ margin: 10px 0; padding: 10px; background: #f0f0f0; border-radius: 5px; }}
        .label {{ font-weight: bold; color: #667eea; }}
        .value {{ color: #333; font-family: 'Courier New', monospace; }}
        .button {{ display: inline-block; background: #667eea; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .warning {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 5px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎓 University Attendance System</h1>
            <p>Welcome to the system!</p>
        </div>
        <div class='content'>
            <h2>Hello {fullName},</h2>
            <p>Your account has been successfully created for the University Attendance Management System.</p>
            
            <div class='credentials-box'>
                <h3>📋 Your Login Credentials</h3>
                <div class='credential-row'>
                    <span class='label'>📧 Email:</span><br>
                    <span class='value'>{toEmail}</span>
                </div>
                <div class='credential-row'>
                    <span class='label'>🔒 Password:</span><br>
                    <span class='value'>{password}</span>
                </div>
                <div class='credential-row'>
                    <span class='label'>👤 Role:</span><br>
                    <span class='value'>{role}</span>
                </div>
                {(string.IsNullOrEmpty(sectionName) ? "" : $@"
                <div class='credential-row'>
                    <span class='label'>🏛️ Section:</span><br>
                    <span class='value'>{sectionName}</span>
                </div>")}
            </div>

            <div style='text-align: center;'>
                <a href='{loginUrl}' class='button'>🚀 Login Now</a>
            </div>

            <div class='warning'>
                ⚠️ <strong>Important:</strong> Please change your password after your first login for security purposes.
            </div>

            <p><strong>Login URL:</strong> <a href='{loginUrl}'>{loginUrl}</a></p>
        </div>
        <div class='footer'>
            <p>&copy; 2025 University Attendance Management System</p>
            <p>This is an automated email. Please do not reply to this message.</p>
        </div>
    </div>
</body>
</html>";

        var plainText = $@"
University Attendance System - Your Credentials

Hello {fullName},

Your account has been created successfully.

Login Details:
━━━━━━━━━━━━━━━━━━━━━━━━━━
📧 Email: {toEmail}
🔒 Password: {password}
👤 Role: {role}
{(string.IsNullOrEmpty(sectionName) ? "" : $"🏛️ Section: {sectionName}")}
━━━━━━━━━━━━━━━━━━━━━━━━━━

Login URL: {loginUrl}

⚠️ Important: Please change your password after first login.

Best regards,
University Administration

---
This is an automated email. Please do not reply.
        ";

        return await SendEmailAsync(toEmail, fullName, subject, htmlBody, plainText);
    }

    /// <summary>
    /// Send bulk emails (one by one with small delay to avoid rate limits)
    /// </summary>
    public async Task<BulkEmailResult> SendBulkCredentialsEmailsAsync(List<UserCredential> users, string loginUrl)
    {
        var result = new BulkEmailResult
        {
            TotalEmails = users.Count
        };

        foreach (var user in users)
        {
            var success = await SendCredentialsEmailAsync(
                user.Email, 
                user.FullName, 
                user.Password, 
                user.Role, 
                loginUrl, 
                user.SectionName
            );

            if (success)
            {
                result.SuccessCount++;
                result.SuccessfulEmails.Add(user.Email);
            }
            else
            {
                result.FailedCount++;
                result.FailedEmails.Add(user.Email);
            }

            // Small delay to avoid rate limiting (Gmail allows ~500/day, so this is safe)
            await Task.Delay(500); // 0.5 second delay between emails
        }

        return result;
    }

    /// <summary>
    /// Strip HTML tags for plain text version
    /// </summary>
    private string StripHtml(string html)
    {
        return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
    }
}

/// <summary>
/// Result of bulk email sending operation
/// </summary>
public class BulkEmailResult
{
    public int TotalEmails { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> SuccessfulEmails { get; set; } = new();
    public List<string> FailedEmails { get; set; } = new();
}
