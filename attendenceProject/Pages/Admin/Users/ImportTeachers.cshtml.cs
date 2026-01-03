using attendence.Services.Models;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace attendenceProject.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public class ImportTeachersModel : PageModel
{
    private readonly BulkImportService _bulkImportService;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;

    public int Step { get; set; } = 1;
    public string? ErrorMessage { get; set; }
    public ImportValidationResult<TeacherImportModel>? ValidationResult { get; set; }
    public BulkCreateResult? CreateResult { get; set; }
    public BulkEmailResult? EmailResult { get; set; }

    public ImportTeachersModel(BulkImportService bulkImportService, EmailService emailService, IConfiguration configuration)
    {
        _bulkImportService = bulkImportService;
        _emailService = emailService;
        _configuration = configuration;
    }

    public void OnGet()
    {
        Step = 1;
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            ErrorMessage = "Please select a file to upload.";
            Step = 1;
            return Page();
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".csv" && extension != ".xlsx")
        {
            ErrorMessage = "Invalid file format. Please upload a CSV or Excel (.xlsx) file.";
            Step = 1;
            return Page();
        }

        try
        {
            using var stream = file.OpenReadStream();

            if (extension == ".csv")
            {
                ValidationResult = await _bulkImportService.ValidateTeachersCsvAsync(stream);
            }
            else
            {
                ValidationResult = await _bulkImportService.ValidateTeachersExcelAsync(stream);
            }

            TempData["ValidationResult"] = System.Text.Json.JsonSerializer.Serialize(ValidationResult);
            Step = 2;
            return Page();
        }
        catch (Exception ex)
        {
            var errorDetail = ex.InnerException != null ? $"{ex.Message} - {ex.InnerException.Message}" : ex.Message;
            ErrorMessage = $"Error processing file: {errorDetail}";
            Console.WriteLine($"[Import Error] {ex}");
            Step = 1;
            return Page();
        }
    }

    public async Task<IActionResult> OnPostConfirmAsync()
    {
        var validationJson = TempData["ValidationResult"] as string;
        if (string.IsNullOrEmpty(validationJson))
        {
            return RedirectToPage();
        }

        ValidationResult = System.Text.Json.JsonSerializer.Deserialize<ImportValidationResult<TeacherImportModel>>(validationJson);
        
        if (ValidationResult == null || ValidationResult.ValidCount == 0)
        {
            ErrorMessage = "No valid records to import.";
            Step = 2;
            return Page();
        }

        try
        {
            CreateResult = await _bulkImportService.CreateTeachersAsync(ValidationResult.ValidRecords);

            if (CreateResult.UserCredentials.Count > 0)
            {
                var loginUrl = _configuration.GetValue<string>("AppSettings:LoginUrl") ?? "http://localhost:5000/Account/Login";
                EmailResult = await _emailService.SendBulkCredentialsEmailsAsync(CreateResult.UserCredentials, loginUrl);
            }

            Step = 3;
            return Page();
        }
        catch (Exception ex)
        {
            var errorDetail = ex.InnerException != null ? $"{ex.Message} - {ex.InnerException.Message}" : ex.Message;
            ErrorMessage = $"Error creating teachers: {errorDetail}";
            Console.WriteLine($"[Teacher Creation Error] {ex}");
            Step = 2;
            return Page();
        }
    }
}
