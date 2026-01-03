using attendence.Data.Data;
using attendence.Domain.Entities;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace attendenceProject.Pages.Admin.Users;

[Authorize(Roles = "Admin")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly PasswordHasher _passwordHasher;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;

    public CreateModel(ApplicationDbContext context, PasswordHasher passwordHasher, EmailService emailService, IConfiguration configuration)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _configuration = configuration;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;

        // Teacher fields
        public string? BadgeNumber { get; set; }
        public string? Designation { get; set; }

        // Student fields
        public string? RollNo { get; set; }
        public string? FatherName { get; set; }
        public int? SectionId { get; set; }
    }

    public List<SelectListItem> SemesterOptions { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadSemesterOptions();
    }
    
    private async Task LoadSemesterOptions()
    {
        // Get unique semester and session combinations
        var semesters = await _context.Sections
            .Select(s => new { s.Semester, s.Session })
            .Distinct()
            .OrderByDescending(s => s.Session) // Latest sessions first
            .ThenBy(s => s.Semester)
            .ToListAsync();

        SemesterOptions = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "-- Select Semester --", Disabled = true, Selected = true }
        };

        foreach (var sem in semesters)
        {
            string ordinal = sem.Semester switch
            {
                1 => "1st",
                2 => "2nd",
                3 => "3rd",
                _ => $"{sem.Semester}th"
            };
            
            SemesterOptions.Add(new SelectListItem
            {
                Value = $"{sem.Semester}|{sem.Session}",
                Text = $"{ordinal} Semester ({sem.Session})"
            });
        }
    }
    
    public async Task<JsonResult> OnGetSectionsAsync(int semester, string session)
    {
        var sections = await _context.Sections
            .Include(s => s.Badge)
            .Where(s => s.Semester == semester && s.Session == session)
            .OrderBy(s => s.Name)
            .Select(s => new 
            {
                value = s.Id,
                text = $"Section {s.Name} - {s.Badge.Name}"
            })
            .ToListAsync();

        return new JsonResult(sections);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Load semesters for dropdown
        await LoadSemesterOptions();

        // Validate role-specific fields
        if (Input.Role == "Teacher")
        {
            if (string.IsNullOrWhiteSpace(Input.BadgeNumber))
            {
                ModelState.AddModelError("Input.BadgeNumber", "Badge Number is required for teachers.");
            }
        }
        else if (Input.Role == "Student")
        {
            if (string.IsNullOrWhiteSpace(Input.RollNo))
            {
                ModelState.AddModelError("Input.RollNo", "Roll Number is required for students.");
            }
            if (Input.SectionId == null || Input.SectionId == 0)
            {
                ModelState.AddModelError("Input.SectionId", "Section is required for students.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Check for duplicate email
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == Input.Email.ToLower());

        if (existingUser != null)
        {
            ModelState.AddModelError("Input.Email", "A user with this email already exists.");
            return Page();
        }

        // Check for duplicate badge number if teacher
        if (Input.Role == "Teacher" && !string.IsNullOrWhiteSpace(Input.BadgeNumber))
        {
            var existingTeacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.BadgeNumber.ToLower() == Input.BadgeNumber.ToLower());

            if (existingTeacher != null)
            {
                ModelState.AddModelError("Input.BadgeNumber", "A teacher with this badge number already exists.");
                return Page();
            }
        }

        // Check for duplicate roll number if student
        if (Input.Role == "Student" && !string.IsNullOrWhiteSpace(Input.RollNo))
        {
            var existingStudent = await _context.Students
                .FirstOrDefaultAsync(s => s.RollNo.ToLower() == Input.RollNo.ToLower());

            if (existingStudent != null)
            {
                ModelState.AddModelError("Input.RollNo", "A student with this roll number already exists.");
                return Page();
            }
        }

        // Create user
        var user = new User
        {
            FullName = Input.FullName,
            Email = Input.Email,
            PasswordHash = _passwordHasher.HashPassword(Input.Password),
            Role = Input.Role,
            CreatedAt = DateTime.Now
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Create role-specific records
        string? sectionName = null;
        if (Input.Role == "Teacher")
        {
            var teacher = new attendence.Domain.Entities.Teacher
            {
                UserId = user.Id,
                BadgeNumber = Input.BadgeNumber!,
                Designation = Input.Designation
            };
            _context.Teachers.Add(teacher);
            await _context.SaveChangesAsync();
        }
        else if (Input.Role == "Student")
        {
            var student = new attendence.Domain.Entities.Student
            {
                UserId = user.Id,
                SectionId = Input.SectionId!.Value,
                RollNo = Input.RollNo!,
                FatherName = Input.FatherName
            };
            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            // Get section name for email
            var section = await _context.Sections
                .Include(s => s.Badge)
                .FirstOrDefaultAsync(s => s.Id == Input.SectionId.Value);
            if (section != null)
            {
                sectionName = $"Section {section.Name} - {section.Badge.Name}";
            }
        }

        // Send credentials email
        try
        {
            var loginUrl = $"{_configuration["AppSettings:BaseUrl"]}/Account/Login";
            await _emailService.SendCredentialsEmailAsync(
                Input.Email,
                Input.FullName,
                Input.Password,
                Input.Role,
                loginUrl,
                sectionName
            );
        }
        catch (Exception ex)
        {
            // Log error but don't fail the creation
            Console.WriteLine($"Failed to send email: {ex.Message}");
        }

        TempData["SuccessMessage"] = $"{Input.Role} '{Input.FullName}' created successfully! Credentials have been sent to {Input.Email}";
        return RedirectToPage("./Index");
    }
}
