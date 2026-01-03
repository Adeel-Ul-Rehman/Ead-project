using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;

namespace attendenceProject.Pages.Admin.Schedule
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public int TotalRules { get; set; }
        public int TotalLectures { get; set; }
        public int TotalSections { get; set; }
        public int TotalAssignments { get; set; }

        public async Task OnGetAsync()
        {
            TotalRules = await _context.TimetableRules.CountAsync();
            TotalLectures = await _context.Lectures.CountAsync();
            TotalSections = await _context.Sections.CountAsync();
            TotalAssignments = await _context.TeacherCourses.CountAsync();
        }
    }
}
