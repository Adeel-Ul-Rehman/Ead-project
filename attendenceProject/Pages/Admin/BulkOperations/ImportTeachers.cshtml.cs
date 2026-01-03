using attendence.Data.Data;
using attendence.Domain.Entities;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;

namespace attendenceProject.Pages.Admin.BulkOperations
{
    [Authorize(Roles = "Admin")]
    public class ImportTeachersModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher _passwordHasher;

        public ImportTeachersModel(ApplicationDbContext context, PasswordHasher passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        public List<ImportResult> ImportResults { get; set; } = new();

        public void OnGet()
        {
        }

        public IActionResult OnGetDownloadTemplate()
        {
            var csv = new StringBuilder();
            csv.AppendLine("FullName,Email,BadgeNumber,Department");
            csv.AppendLine("Dr. John Smith,john.smith@example.com,T-001,Computer Science");
            csv.AppendLine("Dr. Jane Doe,jane.doe@example.com,T-002,Mathematics");

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", "teachers_template.csv");
        }

        public async Task<IActionResult> OnPostAsync(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a CSV file.";
                return Page();
            }

            ImportResults = new List<ImportResult>();
            var rowNumber = 1; // Start from 1 (header row)

            using (var reader = new StreamReader(csvFile.OpenReadStream()))
            {
                // Skip header row
                await reader.ReadLineAsync();
                rowNumber++;

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        rowNumber++;
                        continue;
                    }

                    var values = line.Split(',');

                    if (values.Length < 4)
                    {
                        ImportResults.Add(new ImportResult
                        {
                            RowNumber = rowNumber,
                            Success = false,
                            ErrorMessage = "Invalid CSV format. Expected 4 columns."
                        });
                        rowNumber++;
                        continue;
                    }

                    try
                    {
                        var fullName = values[0].Trim();
                        var email = values[1].Trim();
                        var badgeNumber = values[2].Trim();
                        var department = values[3].Trim();

                        // Validate required fields
                        if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email))
                        {
                            ImportResults.Add(new ImportResult
                            {
                                RowNumber = rowNumber,
                                Success = false,
                                ErrorMessage = "FullName and Email are required."
                            });
                            rowNumber++;
                            continue;
                        }

                        // Check if email already exists
                        if (_context.Users.Any(u => u.Email == email))
                        {
                            ImportResults.Add(new ImportResult
                            {
                                RowNumber = rowNumber,
                                Success = false,
                                ErrorMessage = $"Email '{email}' already exists."
                            });
                            rowNumber++;
                            continue;
                        }

                        // Check if badge number already exists (if provided)
                        if (!string.IsNullOrEmpty(badgeNumber) && _context.Teachers.Any(t => t.BadgeNumber == badgeNumber))
                        {
                            ImportResults.Add(new ImportResult
                            {
                                RowNumber = rowNumber,
                                Success = false,
                                ErrorMessage = $"Badge number '{badgeNumber}' already exists."
                            });
                            rowNumber++;
                            continue;
                        }

                        // Create user
                        var defaultPassword = "Teacher@123";
                        var user = new User
                        {
                            FullName = fullName,
                            Email = email,
                            PasswordHash = _passwordHasher.HashPassword(defaultPassword),
                            Role = "Teacher",
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };

                        _context.Users.Add(user);
                        await _context.SaveChangesAsync();
                        
                        // Create teacher
                        var teacher = new attendence.Domain.Entities.Teacher
                        {
                            UserId = user.Id,
                            BadgeNumber = badgeNumber,
                            Designation = department
                        };

                        _context.Teachers.Add(teacher);
                        await _context.SaveChangesAsync();

                        ImportResults.Add(new ImportResult
                        {
                            RowNumber = rowNumber,
                            Success = true,
                            ErrorMessage = string.Empty
                        });
                    }
                    catch (Exception ex)
                    {
                        ImportResults.Add(new ImportResult
                        {
                            RowNumber = rowNumber,
                            Success = false,
                            ErrorMessage = ex.Message
                        });
                    }

                    rowNumber++;
                }
            }

            var successCount = ImportResults.Count(r => r.Success);
            var failCount = ImportResults.Count(r => !r.Success);

            TempData["SuccessMessage"] = $"Import completed: {successCount} successful, {failCount} failed.";
            return Page();
        }

        public class ImportResult
        {
            public int RowNumber { get; set; }
            public bool Success { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
        }
    }
}
