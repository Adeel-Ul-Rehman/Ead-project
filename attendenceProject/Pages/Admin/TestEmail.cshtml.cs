using attendence.Services.Services;
using attendence.Services.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace attendenceProject.Pages.Admin;

[Authorize(Roles = "Admin")]
public class TestEmailModel : PageModel
{
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;

    [BindProperty]
    public string TestEmail { get; set; } = string.Empty;

    [BindProperty]
    public string TestName { get; set; } = string.Empty;

    [BindProperty]
    public string TestRole { get; set; } = "Student";

    public string Message { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }

    public TestEmailModel(EmailService emailService, IConfiguration configuration)
    {
        _emailService = emailService;
        _configuration = configuration;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            Message = "Please fill in all required fields.";
            IsSuccess = false;
            return Page();
        }

        try
        {
            // Generate a test password
            var testPassword = PasswordGenerator.GeneratePassword(10);

            // Get login URL from configuration
            var loginUrl = _configuration.GetValue<string>("AppSettings:LoginUrl") ?? "http://localhost:5000/Account/Login";

            // Send test email
            var success = await _emailService.SendCredentialsEmailAsync(
                TestEmail,
                TestName,
                testPassword,
                TestRole,
                loginUrl,
                TestRole == "Student" ? "CS-A (Test Section)" : null
            );

            if (success)
            {
                Message = $"✅ Test email sent successfully to {TestEmail}! Check your inbox (and spam folder). Password used: {testPassword}";
                IsSuccess = true;
            }
            else
            {
                Message = "❌ Failed to send email. Please check your email configuration in appsettings.json and ensure you've set up Gmail App Password correctly.";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            Message = $"❌ Error: {ex.Message}";
            IsSuccess = false;
        }

        return Page();
    }
}
