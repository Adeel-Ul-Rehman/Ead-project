using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Admin.BulkOperations
{
    [Authorize(Roles = "Admin")]
    public class GenerateLecturesModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public GenerateLecturesModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public DateTime? StartDate { get; set; }

        [BindProperty]
        public DateTime? EndDate { get; set; }

        public LectureGenerationResult? GenerationResult { get; set; }

        public void OnGet()
        {
            // Set default dates (next week)
            StartDate = DateTime.Today.AddDays(1);
            EndDate = DateTime.Today.AddDays(7);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!StartDate.HasValue || !EndDate.HasValue)
            {
                ModelState.AddModelError("", "Both start and end dates are required.");
                return Page();
            }

            if (EndDate.Value < StartDate.Value)
            {
                ModelState.AddModelError(nameof(EndDate), "End date must be after start date.");
                return Page();
            }

            if ((EndDate.Value - StartDate.Value).TotalDays > 90)
            {
                ModelState.AddModelError(nameof(EndDate), "Maximum range is 90 days.");
                return Page();
            }

            GenerationResult = new LectureGenerationResult();

            // Get all active timetable rules
            var timetableRules = await _context.TimetableRules
                .Include(tr => tr.TeacherCourse)
                .Where(tr => tr.StartDate <= EndDate.Value && tr.EndDate >= StartDate.Value)
                .ToListAsync();

            GenerationResult.TimetableRulesProcessed = timetableRules.Count;

            // Get all holidays in the date range
            var holidays = await _context.Holidays
                .Where(h => h.Date >= StartDate.Value && h.Date <= EndDate.Value)
                .Select(h => h.Date.Date)
                .ToListAsync();

            // Process each timetable rule
            foreach (var rule in timetableRules)
            {
                var daysOfWeek = rule.DaysOfWeek.Split(',').Select(d => d.Trim()).ToList();

                // Iterate through each day in the date range
                for (var date = StartDate.Value.Date; date <= EndDate.Value.Date; date = date.AddDays(1))
                {
                    GenerationResult.TotalProcessed++;

                    // Skip if it's a holiday
                    if (holidays.Contains(date))
                    {
                        GenerationResult.LecturesSkipped++;
                        continue;
                    }

                    // Check if this day matches the timetable rule
                    var dayName = date.DayOfWeek.ToString().Substring(0, 3); // Mon, Tue, Wed, etc.
                    if (!daysOfWeek.Contains(dayName))
                    {
                        GenerationResult.LecturesSkipped++;
                        continue;
                    }

                    // Create lecture datetime
                    var lectureStart = date.Add(rule.StartTime);
                    var lectureEnd = lectureStart.AddMinutes(rule.DurationMinutes);

                    // Check if lecture already exists
                    var exists = await _context.Lectures
                        .AnyAsync(l => l.TimetableRuleId == rule.Id &&
                                      l.StartDateTime == lectureStart);

                    if (exists)
                    {
                        GenerationResult.LecturesSkipped++;
                        continue;
                    }

                    // Create new lecture
                    var lecture = new Lecture
                    {
                        TimetableRuleId = rule.Id,
                        StartDateTime = lectureStart,
                        EndDateTime = lectureEnd,
                        Status = "Scheduled"
                    };

                    _context.Lectures.Add(lecture);
                    GenerationResult.LecturesCreated++;
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Successfully generated {GenerationResult.LecturesCreated} lectures!";
            return Page();
        }

        public class LectureGenerationResult
        {
            public int LecturesCreated { get; set; }
            public int LecturesSkipped { get; set; }
            public int TotalProcessed { get; set; }
            public int TimetableRulesProcessed { get; set; }
        }
    }
}
