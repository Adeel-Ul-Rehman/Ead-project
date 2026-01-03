using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace attendenceProject.Pages.Admin.Timetables
{
    public class BulkImportModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public BulkImportModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public IFormFile? CsvFile { get; set; }

        public List<ImportResult> Results { get; set; } = new();
        public bool ShowResults { get; set; } = false;

        public class ImportResult
        {
            public int Row { get; set; }
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string Details { get; set; } = string.Empty;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (CsvFile == null || CsvFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a CSV file to upload.";
                return Page();
            }

            if (!CsvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Only CSV files are allowed.";
                return Page();
            }

            ShowResults = true;
            int successCount = 0;
            int errorCount = 0;

            using var reader = new StreamReader(CsvFile.OpenReadStream());
            
            // Skip header line
            var header = await reader.ReadLineAsync();
            int rowNumber = 1;

            while (!reader.EndOfStream)
            {
                rowNumber++;
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var values = line.Split(',');
                
                if (values.Length < 7)
                {
                    Results.Add(new ImportResult
                    {
                        Row = rowNumber,
                        Success = false,
                        Message = "Invalid format",
                        Details = "Expected at least 7 columns: BadgeName,SectionName,Session,CourseCode,TeacherEmail,DaysOfWeek,StartTime,EndTime,RoomNumber"
                    });
                    errorCount++;
                    continue;
                }

                try
                {
                    var badgeName = values[0].Trim();
                    var sectionName = values[1].Trim();
                    var session = values[2].Trim();
                    var courseCode = values[3].Trim();
                    var teacherEmail = values[4].Trim();
                    var daysOfWeek = values[5].Trim();
                    var startTimeStr = values[6].Trim();
                    var endTimeStr = values[7].Trim();
                    var roomNumber = values.Length > 8 ? values[8].Trim() : "";

                    // Validate and find entities
                    var badge = await _context.Badges.FirstOrDefaultAsync(b => b.Name == badgeName);
                    if (badge == null)
                    {
                        Results.Add(new ImportResult
                        {
                            Row = rowNumber,
                            Success = false,
                            Message = "Badge not found",
                            Details = $"Badge '{badgeName}' does not exist"
                        });
                        errorCount++;
                        continue;
                    }

                    var section = await _context.Sections.FirstOrDefaultAsync(s => 
                        s.Name == sectionName && s.Session == session && s.BadgeId == badge.Id);
                    if (section == null)
                    {
                        Results.Add(new ImportResult
                        {
                            Row = rowNumber,
                            Success = false,
                            Message = "Section not found",
                            Details = $"Section '{sectionName}' for session '{session}' and badge '{badgeName}' does not exist"
                        });
                        errorCount++;
                        continue;
                    }

                    var course = await _context.Courses.FirstOrDefaultAsync(c => c.Code == courseCode);
                    if (course == null)
                    {
                        Results.Add(new ImportResult
                        {
                            Row = rowNumber,
                            Success = false,
                            Message = "Course not found",
                            Details = $"Course with code '{courseCode}' does not exist"
                        });
                        errorCount++;
                        continue;
                    }

                    var teacherUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == teacherEmail && u.Role == "Teacher");
                    if (teacherUser == null)
                    {
                        Results.Add(new ImportResult
                        {
                            Row = rowNumber,
                            Success = false,
                            Message = "Teacher not found",
                            Details = $"Teacher with email '{teacherEmail}' does not exist"
                        });
                        errorCount++;
                        continue;
                    }

                    var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == teacherUser.Id);
                    if (teacher == null)
                    {
                        Results.Add(new ImportResult
                        {
                            Row = rowNumber,
                            Success = false,
                            Message = "Teacher record not found",
                            Details = $"Teacher record for '{teacherEmail}' is missing"
                        });
                        errorCount++;
                        continue;
                    }

                    // Parse times
                    if (!TimeSpan.TryParse(startTimeStr, out var startTime))
                    {
                        Results.Add(new ImportResult
                        {
                            Row = rowNumber,
                            Success = false,
                            Message = "Invalid start time",
                            Details = $"Start time '{startTimeStr}' is not in valid format (HH:mm)"
                        });
                        errorCount++;
                        continue;
                    }

                    if (!TimeSpan.TryParse(endTimeStr, out var endTime))
                    {
                        Results.Add(new ImportResult
                        {
                            Row = rowNumber,
                            Success = false,
                            Message = "Invalid end time",
                            Details = $"End time '{endTimeStr}' is not in valid format (HH:mm)"
                        });
                        errorCount++;
                        continue;
                    }

                    // Find or create TeacherCourse
                    var teacherCourse = await _context.TeacherCourses.FirstOrDefaultAsync(tc =>
                        tc.TeacherId == teacher.Id && tc.CourseId == course.Id && tc.SectionId == section.Id);

                    if (teacherCourse == null)
                    {
                        teacherCourse = new TeacherCourse
                        {
                            TeacherId = teacher.Id,
                            CourseId = course.Id,
                            SectionId = section.Id,
                            AssignedAt = DateTime.Now
                        };
                        _context.TeacherCourses.Add(teacherCourse);
                        await _context.SaveChangesAsync();
                    }

                    // Check for existing rule to avoid duplicates
                    var existingRule = await _context.TimetableRules.FirstOrDefaultAsync(tr =>
                        tr.TeacherCourseId == teacherCourse.Id &&
                        tr.DaysOfWeek == daysOfWeek &&
                        tr.StartTime == startTime);

                    if (existingRule != null)
                    {
                        Results.Add(new ImportResult
                        {
                            Row = rowNumber,
                            Success = false,
                            Message = "Duplicate rule",
                            Details = $"Timetable rule already exists for this course, time, and days"
                        });
                        errorCount++;
                        continue;
                    }

                    // Calculate duration from start and end times
                    var duration = (int)(endTime - startTime).TotalMinutes;

                    // Create timetable rule
                    var timetableRule = new TimetableRule
                    {
                        TeacherCourseId = teacherCourse.Id,
                        DaysOfWeek = daysOfWeek,
                        StartTime = startTime,
                        DurationMinutes = duration,
                        Room = roomNumber,
                        StartDate = DateTime.Today,
                        EndDate = DateTime.Today.AddMonths(6)
                    };

                    _context.TimetableRules.Add(timetableRule);
                    await _context.SaveChangesAsync();

                    Results.Add(new ImportResult
                    {
                        Row = rowNumber,
                        Success = true,
                        Message = "Success",
                        Details = $"Timetable rule created: {courseCode} for {sectionName} on {daysOfWeek}"
                    });
                    successCount++;
                }
                catch (Exception ex)
                {
                    Results.Add(new ImportResult
                    {
                        Row = rowNumber,
                        Success = false,
                        Message = "Error",
                        Details = ex.Message
                    });
                    errorCount++;
                }
            }

            TempData["SuccessMessage"] = $"Import completed: {successCount} successful, {errorCount} errors.";
            return Page();
        }

        public IActionResult OnGetDownloadTemplate()
        {
            var csv = "BadgeName,SectionName,Session,CourseCode,TeacherEmail,DaysOfWeek,StartTime,EndTime,RoomNumber\n";
            csv += "BSCS,A,2023-2027,CS101,teacher@example.com,\"Monday,Wednesday,Friday\",08:00,09:30,Room 101\n";
            csv += "BSCS,A,2023-2027,CS102,teacher2@example.com,\"Tuesday,Thursday\",10:00,11:30,Room 102\n";

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", "timetable_template.csv");
        }
    }
}
