using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace attendenceProject.Pages.Admin.Lectures
{
    [Authorize(Roles = "Admin")]
    public class ImportCSVModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ImportCSVModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        [Required(ErrorMessage = "Please select a CSV file")]
        public IFormFile? CsvFile { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || CsvFile == null)
            {
                return Page();
            }

            if (!CsvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(CsvFile), "Only CSV files are allowed");
                return Page();
            }

            var lectures = new List<Lecture>();
            var errors = new List<string>();
            int lineNumber = 0;

            try
            {
                using var reader = new StreamReader(CsvFile.OpenReadStream());
                
                // Skip comment lines and find header
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    {
                        continue; // Skip comments and empty lines
                    }
                    
                    // This should be the header line
                    if (!line.Contains("TimetableRuleId"))
                    {
                        errors.Add($"Line {lineNumber}: Invalid header. Expected 'TimetableRuleId' column");
                        break;
                    }
                    break; // Found header, move to data
                }

                // Process data rows
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;
                    
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    {
                        continue; // Skip comments and empty lines
                    }

                    try
                    {
                        var columns = line.Split(',');
                        
                        if (columns.Length < 4)
                        {
                            errors.Add($"Line {lineNumber}: Insufficient columns. Expected at least 4 columns");
                            continue;
                        }

                        // Parse required fields
                        if (!int.TryParse(columns[0].Trim(), out int timetableRuleId))
                        {
                            errors.Add($"Line {lineNumber}: Invalid TimetableRuleId '{columns[0]}'");
                            continue;
                        }

                        if (!DateTime.TryParseExact(columns[1].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                        {
                            errors.Add($"Line {lineNumber}: Invalid Date format '{columns[1]}'. Use YYYY-MM-DD");
                            continue;
                        }

                        if (!TimeSpan.TryParse(columns[2].Trim(), out TimeSpan startTime))
                        {
                            errors.Add($"Line {lineNumber}: Invalid StartTime format '{columns[2]}'. Use HH:MM");
                            continue;
                        }

                        if (!TimeSpan.TryParse(columns[3].Trim(), out TimeSpan endTime))
                        {
                            errors.Add($"Line {lineNumber}: Invalid EndTime format '{columns[3]}'. Use HH:MM");
                            continue;
                        }

                        // Verify timetable rule exists
                        var ruleExists = await _context.TimetableRules.AnyAsync(tr => tr.Id == timetableRuleId);
                        if (!ruleExists)
                        {
                            errors.Add($"Line {lineNumber}: TimetableRule ID {timetableRuleId} not found in database");
                            continue;
                        }

                        // Create datetime
                        var startDateTime = date.Date.Add(startTime);
                        var endDateTime = date.Date.Add(endTime);

                        if (endDateTime <= startDateTime)
                        {
                            errors.Add($"Line {lineNumber}: EndTime must be after StartTime");
                            continue;
                        }

                        // Check for duplicates
                        var duplicate = await _context.Lectures.AnyAsync(l =>
                            l.TimetableRuleId == timetableRuleId &&
                            l.StartDateTime == startDateTime);

                        if (duplicate)
                        {
                            errors.Add($"Line {lineNumber}: Lecture already exists for this date and time");
                            continue;
                        }

                        // Check if it's a holiday
                        var isHoliday = await _context.Holidays.AnyAsync(h => h.Date.Date == date.Date);
                        if (isHoliday)
                        {
                            errors.Add($"Line {lineNumber}: Cannot create lecture on holiday {date:yyyy-MM-dd}");
                            continue;
                        }

                        // Create lecture
                        var lecture = new Lecture
                        {
                            TimetableRuleId = timetableRuleId,
                            StartDateTime = startDateTime,
                            EndDateTime = endDateTime,
                            Status = columns.Length > 11 && !string.IsNullOrWhiteSpace(columns[11]) 
                                ? columns[11].Trim().Trim('"') 
                                : "Scheduled"
                        };

                        lectures.Add(lecture);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Line {lineNumber}: Error processing row - {ex.Message}");
                    }
                }

                if (errors.Any())
                {
                    ModelState.AddModelError("", $"Found {errors.Count} error(s) in CSV:");
                    foreach (var error in errors.Take(10)) // Show first 10 errors
                    {
                        ModelState.AddModelError("", error);
                    }
                    if (errors.Count > 10)
                    {
                        ModelState.AddModelError("", $"... and {errors.Count - 10} more errors");
                    }
                    return Page();
                }

                if (!lectures.Any())
                {
                    ModelState.AddModelError("", "No valid lectures found in CSV file");
                    return Page();
                }

                // Save all lectures
                _context.Lectures.AddRange(lectures);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Successfully imported {lectures.Count} lecture(s) from CSV";
                return RedirectToPage("/Admin/Lectures/Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error reading CSV file: {ex.Message}");
                return Page();
            }
        }

        public async Task<IActionResult> OnGetDownloadTemplateAsync()
        {
            var csv = new System.Text.StringBuilder();
            
            // Header with instructions
            csv.AppendLine("# Lecture Import CSV Template");
            csv.AppendLine("# Instructions:");
            csv.AppendLine("# 1. Fill in your lecture data below the header row");
            csv.AppendLine("# 2. TimetableRuleId: Get from database or teacher's timetable rules");
            csv.AppendLine("# 3. Date: Use YYYY-MM-DD format (e.g., 2025-12-15)");
            csv.AppendLine("# 4. StartTime and EndTime: Use HH:MM format (e.g., 09:00)");
            csv.AppendLine("# 5. Other fields are optional/informational");
            csv.AppendLine("#");
            csv.AppendLine("TimetableRuleId,Date,StartTime,EndTime,CourseCode,CourseName,Section,Badge,Room,LectureType,DayOfWeek,Status");
            
            // Sample row
            csv.AppendLine("1,2025-12-15,09:00,10:30,CS101,Programming Fundamentals,BSCS-A,BSCS,Room 301,Lecture,Mon,Scheduled");
            csv.AppendLine("1,2025-12-17,09:00,10:30,CS101,Programming Fundamentals,BSCS-A,BSCS,Room 301,Lecture,Wed,Scheduled");
            csv.AppendLine("# Add more rows as needed...");

            var fileName = $"lecture_import_template_{DateTime.Now:yyyyMMdd}.csv";
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }
    }
}
