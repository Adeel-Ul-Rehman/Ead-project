using attendence.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class StudentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StudentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get all students
        /// GET: api/students
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> GetStudents([FromQuery] int? sectionId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var query = _context.Students
                .Include(s => s.Section)
                    .ThenInclude(sec => sec.Badge)
                .AsQueryable();

            if (sectionId.HasValue)
                query = query.Where(s => s.SectionId == sectionId);

            var total = await query.CountAsync();
            var students = await query
                .OrderBy(s => s.RollNo)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    StudentId = s.Id,
                    FullName = s.User.FullName,
                    Email = s.User.Email,
                    RollNumber = s.RollNo,
                    s.FatherName,
                    PhoneNumber = "",
                    s.SectionId,
                    SectionName = s.Section.Name,
                    BadgeName = s.Section.Badge.Name,
                    Semester = s.Section.Semester,
                    Session = s.Section.Session
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = students,
                pagination = new
                {
                    currentPage = page,
                    pageSize,
                    totalRecords = total,
                    totalPages = (int)Math.Ceiling(total / (double)pageSize)
                }
            });
        }

        /// <summary>
        /// Get student by ID
        /// GET: api/students/{id}
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Teacher,Student")]
        public async Task<IActionResult> GetStudent(int id)
        {
            var student = await _context.Students
                .Include(s => s.Section)
                    .ThenInclude(sec => sec.Badge)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null)
                return NotFound(new { success = false, message = "Student not found" });

            return Ok(new
            {
                success = true,
                data = new
                {
                    StudentId = student.Id,
                    FullName = student.User.FullName,
                    Email = student.User.Email,
                    RollNumber = student.RollNo,
                    student.FatherName,
                    PhoneNumber = "",
                    student.SectionId,
                    SectionName = student.Section.Name,
                    BadgeName = student.Section.Badge.Name,
                    Semester = student.Section.Semester,
                    Session = student.Section.Session
                }
            });
        }

        /// <summary>
        /// Get students by section
        /// GET: api/students/section/{sectionId}
        /// </summary>
        [HttpGet("section/{sectionId}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> GetStudentsBySection(int sectionId)
        {
            var students = await _context.Students
                .Include(s => s.Section)
                .Include(s => s.User)
                .Where(s => s.SectionId == sectionId)
                .OrderBy(s => s.RollNo)
                .Select(s => new
                {
                    StudentId = s.Id,
                    FullName = s.User.FullName,
                    Email = s.User.Email,
                    RollNumber = s.RollNo,
                    PhoneNumber = ""
                })
                .ToListAsync();

            return Ok(new { success = true, data = students, count = students.Count });
        }
    }
}
