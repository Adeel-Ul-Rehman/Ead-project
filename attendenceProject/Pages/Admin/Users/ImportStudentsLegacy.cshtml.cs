using attendence.Services.Models;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace attendenceProject.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public class ImportStudentsLegacyModel : PageModel
{
    private readonly BulkImportService _bulkImportService;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;

    public int Step { get; set; } = 1;
    public string? ErrorMessage { get; set; }
    public ImportValidationResult<StudentImportModel>? ValidationResult { get; set; }
    public BulkCreateResult? CreateResult { get; set; }
    public BulkEmailResult? EmailResult { get; set; }

    public ImportStudentsLegacyModel(BulkImportService bulkImportService, EmailService emailService, IConfiguration configuration)
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

        // Validate file extension
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

            // Validate based on file type
            // NOTE: Old import system - passing sectionId=0 indicates legacy mode (section from CSV)
            if (extension == ".csv")
            {
                ValidationResult = await _bulkImportService.ValidateStudentsCsvAsync(stream, 0);
            }
            else
            {
                ValidationResult = await _bulkImportService.ValidateStudentsExcelAsync(stream, 0);
            }

            // Store validation result in TempData for next request
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
        // Retrieve validation result from TempData
        var validationJson = TempData["ValidationResult"] as string;
        if (string.IsNullOrEmpty(validationJson))
        {
            return RedirectToPage();
        }

        ValidationResult = System.Text.Json.JsonSerializer.Deserialize<ImportValidationResult<StudentImportModel>>(validationJson);
        
        if (ValidationResult == null || ValidationResult.ValidCount == 0)
        {
            ErrorMessage = "No valid records to import.";
            Step = 2;
            return Page();
        }

        try
        {
            // Create students
            // NOTE: Old import system - passing sectionId=0 indicates legacy mode (section from CSV)
            CreateResult = await _bulkImportService.CreateStudentsAsync(ValidationResult.ValidRecords, 0);

            // Send emails
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
            ErrorMessage = $"Error creating students: {errorDetail}";
            Console.WriteLine($"[Student Creation Error] {ex}");
            Step = 2;
            return Page();
        }
    }
}
