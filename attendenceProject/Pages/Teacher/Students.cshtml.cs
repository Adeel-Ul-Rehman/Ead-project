using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;
using attendence.Domain.Entities;
using System.Security.Claims;
using OfficeOpenXml;

namespace attendenceProject.Pages.Teacher
{
    [Authorize(Roles = "Teacher")]
    public class StudentsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public StudentsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<SectionViewModel> Sections { get; set; } = new();
        public List<LectureViewModel> Lectures { get; set; } = new();
        public LectureDetailViewModel? SelectedLecture { get; set; }
        public List<SectionStudentViewModel> SectionStudents { get; set; } = new();
        public int? SelectedSectionId { get; set; }
        public int? SelectedLectureId { get; set; }
        public string? SelectedSectionName { get; set; }

        public async Task<IActionResult> OnGetAsync(int? sectionId, int? lectureId)
        {
            // Get teacher from claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var teacher = await _context.Teachers
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return Unauthorized();
            }

            // Get all sections taught by this teacher
            var teacherSections = await _context.TeacherCourses
                .Where(tc => tc.TeacherId == teacher.Id)
                .Include(tc => tc.Section)
                    .ThenInclude(s => s.Badge)
                .Include(tc => tc.Section.Students)
                .Include(tc => tc.Course)
                .Select(tc => new { tc.Section, tc.Course })
                .Distinct()
                .ToListAsync();

            // Group by section and get courses for each
            var sectionsGrouped = teacherSections
                .GroupBy(x => x.Section.Id)
                .Select(g => new SectionViewModel
                {
                    SectionId = g.First().Section.Id,
                    SectionName = g.First().Section.Name,
                    BadgeName = g.First().Section.Badge.Name,
                    Semester = g.First().Section.Semester,
                    StudentCount = g.First().Section.Students.Count,
                    Courses = string.Join(", ", g.Select(x => x.Course.Code).Distinct())
                })
                .OrderBy(s => s.SectionName)
                .ToList();

            Sections = sectionsGrouped;

            // If a section is selected, load its students and lectures
            if (sectionId.HasValue)
            {
                SelectedSectionId = sectionId.Value;
                var section = await _context.Sections
                    .Include(s => s.Badge)
                    .FirstOrDefaultAsync(s => s.Id == sectionId.Value);
                if (section != null)
                {
                    SelectedSectionName = $"{section.Name} - {section.Badge.Name}";
                }
                await LoadSectionStudents(sectionId.Value, teacher.Id);
                await LoadSectionLectures(sectionId.Value, teacher.Id);
            }

            // If a lecture is selected, load attendance details
            if (lectureId.HasValue)
            {
                SelectedLectureId = lectureId.Value;
                await LoadLectureDetails(lectureId.Value, sectionId ?? 0);
            }

            return Page();
        }

        private async Task LoadSectionStudents(int sectionId, int teacherId)
        {
            // Get all students in the section
            var students = await _context.Students
                .Include(s => s.User)
                .Where(s => s.SectionId == sectionId)
                .OrderBy(s => s.RollNo)
                .ToListAsync();

            // Get all completed lectures for this section by this teacher
            var completedLectures = await _context.Lectures
                .Include(l => l.AttendanceRecords)
                .Where(l => l.TimetableRule.TeacherCourse.SectionId == sectionId &&
                           l.TimetableRule.TeacherCourse.TeacherId == teacherId &&
                           l.Status == "Completed")
                .ToListAsync();

            var totalLectures = completedLectures.Count;

            SectionStudents = students.Select(student =>
            {
                var attendanceRecords = completedLectures
                    .SelectMany(l => l.AttendanceRecords)
                    .Where(ar => ar.StudentId == student.Id)
                    .ToList();

                var presentCount = attendanceRecords.Count(ar => ar.Status == "Present");
                var absentCount = attendanceRecords.Count(ar => ar.Status == "Absent");
                var lateCount = attendanceRecords.Count(ar => ar.Status == "Late");
                var excusedCount = attendanceRecords.Count(ar => ar.Status == "Excused");

                var attendancePercentage = totalLectures > 0
                    ? Math.Round((double)presentCount / totalLectures * 100, 2)
                    : 0;

                return new SectionStudentViewModel
                {
                    StudentId = student.Id,
                    RollNo = student.RollNo,
                    FullName = student.User.FullName,
                    FatherName = student.FatherName ?? "N/A",
                    Email = student.User.Email,
                    TotalLectures = totalLectures,
                    PresentCount = presentCount,
                    AbsentCount = absentCount,
                    LateCount = lateCount,
                    ExcusedCount = excusedCount,
                    AttendancePercentage = attendancePercentage
                };
            }).ToList();
        }

        private async Task LoadSectionLectures(int sectionId, int teacherId)
        {
            // Get all lectures for this section taught by this teacher
            var lectures = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Where(l => l.TimetableRule.TeacherCourse.SectionId == sectionId &&
                           l.TimetableRule.TeacherCourse.TeacherId == teacherId &&
                           l.Status == "Completed")
                .OrderByDescending(l => l.StartDateTime)
                .ToListAsync();

            Lectures = lectures.Select(l => new LectureViewModel
            {
                LectureId = l.Id,
                CourseName = l.TimetableRule.TeacherCourse.Course.Title,
                CourseCode = l.TimetableRule.TeacherCourse.Course.Code,
                StartDateTime = l.StartDateTime,
                Room = l.TimetableRule.Room,
                LectureType = l.TimetableRule.LectureType,
                AttendanceMarked = _context.AttendanceRecords.Any(ar => ar.LectureId == l.Id)
            }).ToList();
        }

        private async Task LoadLectureDetails(int lectureId, int sectionId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Section)
                            .ThenInclude(s => s.Badge)
                .Include(l => l.AttendanceRecords)
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null) return;

            var section = lecture.TimetableRule.TeacherCourse.Section;

            // Get all students in the section
            var students = await _context.Students
                .Include(s => s.User)
                .Where(s => s.SectionId == sectionId)
                .OrderBy(s => s.RollNo)
                .ToListAsync();

            var studentDetails = new List<StudentDetailViewModel>();

            foreach (var student in students)
            {
                var attendanceRecord = lecture.AttendanceRecords
                    .FirstOrDefault(ar => ar.StudentId == student.Id);

                studentDetails.Add(new StudentDetailViewModel
                {
                    StudentId = student.Id,
                    RollNo = student.RollNo,
                    FullName = student.User.FullName,
                    FatherName = student.FatherName ?? "N/A",
                    Status = attendanceRecord?.Status ?? "Not Marked",
                    MarkedAt = attendanceRecord?.MarkedAt
                });
            }

            SelectedLecture = new LectureDetailViewModel
            {
                LectureId = lecture.Id,
                CourseName = lecture.TimetableRule.TeacherCourse.Course.Title,
                CourseCode = lecture.TimetableRule.TeacherCourse.Course.Code,
                SectionName = section.Name,
                BadgeName = section.Badge.Name,
                Semester = section.Semester,
                StartDateTime = lecture.StartDateTime,
                EndDateTime = lecture.EndDateTime,
                Room = lecture.TimetableRule.Room,
                LectureType = lecture.TimetableRule.LectureType,
                Students = studentDetails
            };
        }

    }

    public class SectionViewModel
    {
        public int SectionId { get; set; }
        public string SectionName { get; set; } = string.Empty;
        public string BadgeName { get; set; } = string.Empty;
        public int Semester { get; set; }
        public int StudentCount { get; set; }
        public string Courses { get; set; } = string.Empty;
    }

    public class LectureViewModel
    {
        public int LectureId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public DateTime StartDateTime { get; set; }
        public string Room { get; set; } = string.Empty;
        public string LectureType { get; set; } = string.Empty;
        public bool AttendanceMarked { get; set; }
    }

    public class LectureDetailViewModel
    {
        public int LectureId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string BadgeName { get; set; } = string.Empty;
        public int Semester { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string Room { get; set; } = string.Empty;
        public string LectureType { get; set; } = string.Empty;
        public List<StudentDetailViewModel> Students { get; set; } = new();
    }

    public class StudentDetailViewModel
    {
        public int StudentId { get; set; }
        public string RollNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string FatherName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? MarkedAt { get; set; }
    }
    public class SectionStudentViewModel
    {
        public int StudentId { get; set; }
        public string RollNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string FatherName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int TotalLectures { get; set; }
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int LateCount { get; set; }
        public int ExcusedCount { get; set; }
        public double AttendancePercentage { get; set; }
    }
}
