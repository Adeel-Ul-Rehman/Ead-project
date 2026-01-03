using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages.Student;

[Authorize(Roles = "Student")]
public class HolidaysModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public HolidaysModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<HolidayViewModel> UpcomingHolidays { get; set; } = new();
    public List<Holiday> AllHolidays { get; set; } = new();

    public async Task OnGetAsync()
    {
        var today = DateTime.Today;

        // Get all holidays from current date onwards
        var upcomingHolidaysData = await _context.Holidays
            .Where(h => h.Date >= today)
            .OrderBy(h => h.Date)
            .ToListAsync();

        UpcomingHolidays = upcomingHolidaysData
            .Select(h => new HolidayViewModel
            {
                Name = h.Reason,
                StartDate = h.Date,
                EndDate = h.Date, // Single day holiday
                Description = h.Reason,
                DaysUntil = (h.Date - today).Days
            })
            .ToList();

        // Get all holidays for the academic year
        AllHolidays = await _context.Holidays
            .OrderBy(h => h.Date)
            .ToListAsync();
    }
}

public class HolidayViewModel
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Description { get; set; }
    public int DaysUntil { get; set; }
}
