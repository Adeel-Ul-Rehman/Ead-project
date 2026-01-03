using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using attendence.Data.Data;
using attendence.Domain.Entities;
using System.Security.Claims;

namespace attendenceProject.Pages.Teacher.Attendance
{
    [Authorize(Roles = "Teacher")]
    public class MarkModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public MarkModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public Lecture? Lecture { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public string CourseCode { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public string LectureDateTime { get; set; } = string.Empty;
        public string? Room { get; set; }
        public List<StudentAttendanceViewModel> Students { get; set; } = new();
        public bool CanMarkAttendance { get; set; }
        public string? Message { get; set; }
        public string MessageType { get; set; } = "info"; // info, success, warning, error
        public bool IsLocked { get; set; }
        public bool HasApprovedExtension { get; set; }
        public DateTime? ExtensionDeadline { get; set; }
        public bool AlreadyMarked { get; set; }
        public bool CanEdit { get; set; }

        [BindProperty]
        public List<AttendanceInput> AttendanceRecords { get; set; } = new();
        
        public bool ViewOnly { get; set; }

        public async Task<IActionResult> OnGetAsync(int? lectureId, int? timetableRuleId, string? date, bool viewOnly = false)
        {
            ViewOnly = viewOnly;
            
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return Unauthorized();
            }

            // If lectureId is provided, use it directly
            if (lectureId.HasValue)
            {
                Lecture = await LoadLectureWithDetails(lectureId.Value);
            }
            // If timetableRuleId and date are provided, find or create lecture
            else if (timetableRuleId.HasValue && !string.IsNullOrEmpty(date))
            {
                if (!DateTime.TryParse(date, out DateTime lectureDate))
                {
                    return BadRequest("Invalid date format");
                }

                var rule = await _context.TimetableRules
                    .Include(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                    .Include(tr => tr.TeacherCourse.Section)
                        .ThenInclude(s => s.Badge)
                    .Include(tr => tr.TeacherCourse.Section.Students)
                        .ThenInclude(st => st.User)
                    .FirstOrDefaultAsync(tr => tr.Id == timetableRuleId);

                if (rule == null)
                {
                    return NotFound("Timetable rule not found");
                }

                // Verify teacher owns this rule
                if (rule.TeacherCourse.TeacherId != teacher.Id)
                {
                    return Forbid();
                }

                // Calculate start and end times
                var startDateTime = lectureDate.Date.Add(rule.StartTime);
                var endDateTime = startDateTime.AddMinutes(rule.DurationMinutes);

                // Try to find existing lecture
                var existingLecture = await _context.Lectures
                    .FirstOrDefaultAsync(l => l.TimetableRuleId == timetableRuleId && 
                                            l.StartDateTime == startDateTime);

                // If no lecture exists, create one
                if (existingLecture == null)
                {
                    existingLecture = new Lecture
                    {
                        TimetableRuleId = rule.Id,
                        StartDateTime = startDateTime,
                        EndDateTime = endDateTime,
                        Status = "Scheduled"
                    };
                    
                    _context.Lectures.Add(existingLecture);
                    await _context.SaveChangesAsync();
                }

                Lecture = await LoadLectureWithDetails(existingLecture.Id);
            }
            else
            {
                return RedirectToPage("/Teacher/Dashboard");
            }

            if (Lecture == null)
            {
                return NotFound();
            }

            // Verify teacher owns this lecture
            if (Lecture.TimetableRule.TeacherCourse.TeacherId != teacher.Id)
            {
                return Forbid();
            }

            // Set course and lecture details
            CourseTitle = Lecture.TimetableRule.TeacherCourse.Course.Title;
            CourseCode = Lecture.TimetableRule.TeacherCourse.Course.Code;
            SectionName = Lecture.TimetableRule.TeacherCourse.Section.Name;
            LectureDateTime = Lecture.StartDateTime.ToString("MMM dd, yyyy hh:mm tt");
            Room = Lecture.TimetableRule.Room;

            // Check attendance window and extension status first
            await CheckAttendanceWindow();

            // Load students with attendance if already marked, otherwise default to Present
            if (Lecture.AttendanceRecords.Any())
            {
                LoadStudentsWithAttendance();
            }
            else
            {
                LoadStudents();
            }

            return Page();
        }

        private async Task CheckAttendanceWindow()
        {
            if (Lecture == null) return;

            var now = DateTime.Now;
            var lectureStart = Lecture.StartDateTime;
            var lectureEnd = Lecture.EndDateTime;
            var markingWindowEnd = lectureStart.AddMinutes(10); // Can mark within 10 minutes of lecture START

            // Check for approved extension
            var approvedExtension = await _context.AttendanceExtensionRequests
                .Where(r => r.LectureId == Lecture.Id && r.Status == "Approved")
                .FirstOrDefaultAsync();

            if (approvedExtension != null)
            {
                var extensionDeadline = approvedExtension.ApprovedAt!.Value.AddHours(24);
                if (now <= extensionDeadline)
                {
                    // Both Missed and Edit requests allow marking/editing
                    CanMarkAttendance = true;
                    CanEdit = true;
                    HasApprovedExtension = true;
                    ExtensionDeadline = extensionDeadline;
                    
                    var requestTypeText = approvedExtension.RequestType == "Missed" ? "missed attendance" : "attendance edit";
                    Message = $"Extension approved for {requestTypeText}. You can mark/edit attendance until {extensionDeadline:MMM dd, hh:mm tt}.";
                    MessageType = "success";
                    return;
                }
                else
                {
                    CanMarkAttendance = false;
                    CanEdit = false;
                    IsLocked = true;
                    Message = "Extension window has expired. Contact admin for assistance.";
                    MessageType = "error";
                    return;
                }
            }

            // Check if attendance already marked
            bool hasAttendance = Lecture.AttendanceRecords.Any();

            // Can mark attendance: During lecture or within 10 minutes after START
            if (!hasAttendance)
            {
                if (now <= markingWindowEnd)
                {
                    CanMarkAttendance = true;
                    if (now < lectureStart)
                    {
                        var minutesUntil = (lectureStart - now).TotalMinutes;
                        Message = $"Lecture starts in {Math.Ceiling(minutesUntil)} minutes. You can mark attendance during the lecture or within 10 minutes after it starts.";
                        MessageType = "info";
                    }
                    else if (now >= lectureStart && now <= markingWindowEnd)
                    {
                        var minutesLeft = (markingWindowEnd - now).TotalMinutes;
                        Message = $"Lecture is ongoing. You have {Math.Ceiling(minutesLeft)} minutes to mark attendance.";
                        MessageType = "success";
                    }
                }
                else
                {
                    CanMarkAttendance = false;
                    IsLocked = true;
                    Message = "Marking window has closed (10 minutes after lecture start). You can request an extension from admin.";
                    MessageType = "error";
                }
            }
            // Can edit attendance: Within 20 minutes from start (10 min mark + 10 min edit)
            else if (hasAttendance && now <= Lecture.StartDateTime.AddMinutes(20))
            {
                CanEdit = true;
                CanMarkAttendance = false;
                AlreadyMarked = true;
                var minutesFromStart = (now - Lecture.StartDateTime).TotalMinutes;
                var minutesLeftToEdit = 20 - minutesFromStart;
                Message = $"Attendance marked. You can edit it for {Math.Ceiling(minutesLeftToEdit)} more minutes (20 min total window).";
                MessageType = "info";
            }
            // Lecture ended, attendance locked
            else
            {
                CanMarkAttendance = false;
                CanEdit = false;
                IsLocked = true;
                AlreadyMarked = hasAttendance;
                Message = "Lecture has ended. Attendance is now locked. Contact admin to request changes.";
                MessageType = "error";
            }
        }

        private async Task<Lecture?> LoadLectureWithDetails(int lectureId)
        {
            return await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Course)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Section)
                            .ThenInclude(s => s.Badge)
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                        .ThenInclude(tc => tc.Section)
                            .ThenInclude(s => s.Students)
                                .ThenInclude(st => st.User)
                .Include(l => l.AttendanceRecords)
                .FirstOrDefaultAsync(l => l.Id == lectureId);
        }

        public async Task<IActionResult> OnPostAsync(int lectureId, bool isEdit = false)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized();
            }

            var teacher = await _context.Teachers
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher == null)
            {
                return Unauthorized();
            }

            Lecture = await _context.Lectures
                .Include(l => l.TimetableRule)
                    .ThenInclude(tr => tr.TeacherCourse)
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (Lecture == null)
            {
                return NotFound();
            }

            // Verify teacher owns this lecture
            if (Lecture.TimetableRule.TeacherCourse.TeacherId != teacher.Id)
            {
                return Forbid();
            }

            // Check if already marked
            var existingRecords = await _context.AttendanceRecords
                .Where(r => r.LectureId == lectureId)
                .ToListAsync();

            var now = DateTime.Now;
            var lectureStart = Lecture.StartDateTime;
            var lectureEnd = Lecture.EndDateTime;
            var markingWindowEnd = lectureStart.AddMinutes(10);

            // Check for approved extension
            var approvedExtension = await _context.AttendanceExtensionRequests
                .Where(r => r.LectureId == lectureId && r.Status == "Approved")
                .FirstOrDefaultAsync();

            bool hasExtension = false;
            if (approvedExtension != null)
            {
                var extensionDeadline = approvedExtension.ApprovedAt!.Value.AddHours(24);
                hasExtension = now <= extensionDeadline;
            }

            // Determine if this is an edit or new marking
            if (existingRecords.Any())
            {
                // EDIT MODE: Can edit within 20 minutes from lecture start OR with approved extension
                bool canEdit = hasExtension || (now <= Lecture.StartDateTime.AddMinutes(20));
                
                if (!canEdit)
                {
                    TempData["Error"] = "You cannot edit attendance at this time. The 20-minute window has closed.";
                    return RedirectToPage(new { lectureId });
                }

                // Update existing records
                foreach (var input in AttendanceRecords)
                {
                    var existing = existingRecords.FirstOrDefault(r => r.StudentId == input.StudentId);
                    if (existing != null)
                    {
                        existing.Status = input.Status;
                        existing.MarkedAt = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Attendance updated successfully!";
                return RedirectToPage("/Teacher/Dashboard");
            }
            else
            {
                // MARK MODE: Can mark within 10 minutes after START (or extension)
                bool canMark = hasExtension || (now <= markingWindowEnd);

                if (!canMark)
                {
                    TempData["Error"] = "Marking window has closed (10 minutes after lecture start). Request an extension.";
                    return RedirectToPage(new { lectureId });
                }

                // Create attendance records
                var attendanceRecords = new List<AttendanceRecord>();
                foreach (var input in AttendanceRecords)
                {
                    attendanceRecords.Add(new AttendanceRecord
                    {
                        LectureId = lectureId,
                        StudentId = input.StudentId,
                        Status = input.Status,
                        MarkedAt = DateTime.Now
                    });
                }

                _context.AttendanceRecords.AddRange(attendanceRecords);
                
                // Update lecture status
                Lecture.Status = "Completed";
                await _context.SaveChangesAsync();

                var presentCount = attendanceRecords.Count(a => a.Status == "Present");
                var absentCount = attendanceRecords.Count(a => a.Status == "Absent");
                var excusedCount = attendanceRecords.Count(a => a.Status == "Excused");

                TempData["Success"] = $"Attendance marked successfully! Present: {presentCount}, Absent: {absentCount}, Excused: {excusedCount}";
                return RedirectToPage("/Teacher/Dashboard");
            }
        }

        private void LoadStudents()
        {
            if (Lecture == null) return;

            Students = Lecture.TimetableRule.TeacherCourse.Section.Students
                .OrderBy(s => s.RollNo)
                .Select(s => new StudentAttendanceViewModel
                {
                    StudentId = s.Id,
                    RollNo = s.RollNo,
                    FullName = s.User.FullName,
                    Status = "Present" // Default all to Present
                })
                .ToList();

            AttendanceRecords = Students.Select(s => new AttendanceInput
            {
                StudentId = s.StudentId,
                Status = "Present"
            }).ToList();
        }

        private void LoadStudentsWithAttendance()
        {
            if (Lecture == null) return;

            Students = Lecture.TimetableRule.TeacherCourse.Section.Students
                .OrderBy(s => s.RollNo)
                .Select(s => new StudentAttendanceViewModel
                {
                    StudentId = s.Id,
                    RollNo = s.RollNo,
                    FullName = s.User.FullName,
                    Status = Lecture.AttendanceRecords
                        .FirstOrDefault(ar => ar.StudentId == s.Id)?.Status ?? "Not Marked"
                })
                .ToList();
        }
    }

    public class StudentAttendanceViewModel
    {
        public int StudentId { get; set; }
        public string RollNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class AttendanceInput
    {
        public int StudentId { get; set; }
        public string Status { get; set; } = "Present";
    }
}
