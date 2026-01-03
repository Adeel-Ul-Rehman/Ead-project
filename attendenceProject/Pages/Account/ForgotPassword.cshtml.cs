using attendence.Data.Data;
using attendence.Services.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly EmailService _emailService;

    public ForgotPasswordModel(ApplicationDbContext context, EmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Email is required.";
            return Page();
        }

        // Check if user exists
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == Email);
        if (user == null)
        {
            // Don't reveal if email exists or not for security
            SuccessMessage = "If the email exists in our system, you will receive an OTP code shortly.";
            return Page();
        }

        // Generate 6-digit OTP
        var otp = new Random().Next(100000, 999999).ToString();
        var otpExpiry = DateTime.UtcNow.AddMinutes(10); // OTP valid for 10 minutes

        // Store OTP in session
        HttpContext.Session.SetString($"OTP_{Email}", otp);
        HttpContext.Session.SetString($"OTPExpiry_{Email}", otpExpiry.ToString("o"));

        // Send OTP email
        var subject = "Password Reset - OTP Code";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
        .otp-box {{ background: white; border: 3px dashed #667eea; padding: 30px; margin: 20px 0; text-align: center; border-radius: 10px; }}
        .otp-code {{ font-size: 32px; font-weight: bold; color: #667eea; letter-spacing: 8px; font-family: 'Courier New', monospace; }}
        .warning {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; border-radius: 5px; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔐 Password Reset Request</h1>
        </div>
        <div class='content'>
            <h2>Hello {user.FullName},</h2>
            <p>We received a request to reset your password. Use the OTP code below to proceed:</p>
            
            <div class='otp-box'>
                <p style='margin: 0; color: #666; font-size: 14px; text-transform: uppercase;'>Your OTP Code</p>
                <div class='otp-code'>{otp}</div>
                <p style='margin-top: 10px; color: #999; font-size: 12px;'>Valid for 10 minutes</p>
            </div>
            
            <div class='warning'>
                <p><strong>⚠️ Security Notice:</strong></p>
                <ul style='margin: 10px 0; padding-left: 20px;'>
                    <li>This OTP is valid for 10 minutes only</li>
                    <li>Do not share this code with anyone</li>
                    <li>If you didn't request this, please ignore this email</li>
                </ul>
            </div>
            
            <p>Best regards,<br>University Attendance System Team</p>
        </div>
        <div class='footer'>
            <p>This is an automated email. Please do not reply.</p>
            <p>&copy; 2025 University Attendance Management System</p>
        </div>
    </div>
</body>
</html>";

        var emailSent = await _emailService.SendEmailAsync(Email, user.FullName, subject, htmlBody);

        if (emailSent)
        {
            return RedirectToPage("/Account/VerifyOtp", new { email = Email });
        }
        else
        {
            ErrorMessage = "Failed to send OTP. Please try again later.";
            return Page();
        }
    }
}
