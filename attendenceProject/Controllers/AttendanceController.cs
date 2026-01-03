using attendence.Data.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class AttendanceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AttendanceController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get attendance records for a student
        /// GET: api/attendance/student/{studentId}
        /// </summary>
        [HttpGet("student/{studentId}")]
        [Authorize(Roles = "Student,Teacher,Admin")]
        public async Task<IActionResult> GetStudentAttendance(int studentId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var query = _context.AttendanceRecords
                .Include(a => a.Lecture)
                    .ThenInclude(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                            .ThenInclude(tc => tc.Course)
                .Where(a => a.StudentId == studentId);

            if (startDate.HasValue)
                query = query.Where(a => a.Lecture.StartDateTime.Date >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(a => a.Lecture.StartDateTime.Date <= endDate.Value.Date);

            var records = await query
                .OrderByDescending(a => a.Lecture.StartDateTime)
                .Select(a => new
                {
                    RecordId = a.Id,
                    a.StudentId,
                    a.LectureId,
                    a.Status,
                    a.MarkedAt,
                    LectureDate = a.Lecture.StartDateTime,
                    CourseCode = a.Lecture.TimetableRule.TeacherCourse.Course.Code,
                    CourseTitle = a.Lecture.TimetableRule.TeacherCourse.Course.Title
                })
                .ToListAsync();

            return Ok(new { success = true, data = records, count = records.Count });
        }

        /// <summary>
        /// Get attendance summary for a student
        /// GET: api/attendance/student/{studentId}/summary
        /// </summary>
        [HttpGet("student/{studentId}/summary")]
        [Authorize(Roles = "Student,Teacher,Admin")]
        public async Task<IActionResult> GetStudentAttendanceSummary(int studentId)
        {
            var records = await _context.AttendanceRecords
                .Include(a => a.Lecture)
                .Where(a => a.StudentId == studentId)
                .ToListAsync();

            var total = records.Count;
            var present = records.Count(a => a.Status == "Present");
            var absent = records.Count(a => a.Status == "Absent");
            var late = records.Count(a => a.Status == "Late");
            var excused = records.Count(a => a.Status == "Excused");

            var percentage = total > 0 ? (present + late) * 100.0 / total : 0;

            return Ok(new
            {
                success = true,
                data = new
                {
                    studentId,
                    total,
                    present,
                    absent,
                    late,
                    excused,
                    attendancePercentage = Math.Round(percentage, 2)
                }
            });
        }

        /// <summary>
        /// Get attendance for a specific lecture
        /// GET: api/attendance/lecture/{lectureId}
        /// </summary>
        [HttpGet("lecture/{lectureId}")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> GetLectureAttendance(int lectureId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Section)
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound(new { success = false, message = "Lecture not found" });

            var records = await _context.AttendanceRecords
                .Include(a => a.Student)
                    .ThenInclude(s => s.User)
                .Where(a => a.LectureId == lectureId)
                .Select(a => new
                {
                    RecordId = a.Id,
                    a.StudentId,
                    StudentName = a.Student.User.FullName,
                    StudentRollNumber = a.Student.RollNo,
                    a.Status,
                    a.MarkedAt
                })
                .OrderBy(a => a.StudentRollNumber)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                lecture = new
                {
                    LectureId = lecture.Id,
                    LectureDate = lecture.StartDateTime,
                    CourseCode = lecture.TimetableRule.TeacherCourse.Course.Code,
                    CourseTitle = lecture.TimetableRule.TeacherCourse.Course.Title,
                    SectionName = lecture.TimetableRule.TeacherCourse.Section.Name
                },
                attendance = records,
                summary = new
                {
                    total = records.Count,
                    present = records.Count(r => r.Status == "Present"),
                    absent = records.Count(r => r.Status == "Absent"),
                    late = records.Count(r => r.Status == "Late"),
                    excused = records.Count(r => r.Status == "Excused")
                }
            });
        }

        /// <summary>
        /// Mark attendance for multiple students
        /// POST: api/attendance/mark
        /// </summary>
        [HttpPost("mark")]
        [Authorize(Roles = "Teacher,Admin")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Invalid request", errors = ModelState });

            var lecture = await _context.Lectures.FindAsync(request.LectureId);
            if (lecture == null)
                return NotFound(new { success = false, message = "Lecture not found" });

            var recordsToUpdate = new List<object>();

            foreach (var item in request.Attendance)
            {
                var record = await _context.AttendanceRecords
                    .FirstOrDefaultAsync(a => a.LectureId == request.LectureId && a.StudentId == item.StudentId);

                if (record != null)
                {
                    record.Status = item.Status;
                    record.MarkedAt = DateTime.Now;
                    recordsToUpdate.Add(new { item.StudentId, updated = true });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Attendance marked successfully",
                updated = recordsToUpdate.Count
            });
        }
    }

    // DTOs
    public class MarkAttendanceRequest
    {
        public int LectureId { get; set; }
        public List<AttendanceItem> Attendance { get; set; } = new();
    }

    public class AttendanceItem
    {
        public int StudentId { get; set; }
        public string Status { get; set; } = "Present"; // Present, Absent, Late, Excused
    }
}
