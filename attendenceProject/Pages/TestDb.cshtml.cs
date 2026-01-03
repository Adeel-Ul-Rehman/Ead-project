using attendence.Data.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Pages;

public class TestDbModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public TestDbModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public int UserCount { get; set; }
    public int BadgeCount { get; set; }
    public int SectionCount { get; set; }
    public int CourseCount { get; set; }
    public int StudentCount { get; set; }
    public int TeacherCount { get; set; }
    public bool IsConnected { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            IsConnected = await _context.Database.CanConnectAsync();
            
            if (IsConnected)
            {
                UserCount = await _context.Users.CountAsync();
                BadgeCount = await _context.Badges.CountAsync();
                SectionCount = await _context.Sections.CountAsync();
                CourseCount = await _context.Courses.CountAsync();
                StudentCount = await _context.Students.CountAsync();
                TeacherCount = await _context.Teachers.CountAsync();
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ErrorMessage = ex.Message;
        }
    }
}