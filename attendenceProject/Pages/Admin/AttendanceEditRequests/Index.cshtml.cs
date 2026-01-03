using attendence.Data.Data;
using attendence.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TeacherEntity = attendence.Domain.Entities.Teacher;

namespace attendenceProject.Pages.Admin.AttendanceEditRequests
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // Messages
        [TempData]
        public string? SuccessMessage { get; set; }
        [TempData]
        public string? ErrorMessage { get; set; }

        // Statistics
        public int TotalEditRequests { get; set; }
        public int TotalExtensionRequests { get; set; }
        public int PendingEditRequests { get; set; }
        public int PendingExtensionRequests { get; set; }
        public int ApprovedEditRequests { get; set; }
        public int ApprovedExtensionRequests { get; set; }
        public int RejectedEditRequests { get; set; }
        public int RejectedExtensionRequests { get; set; }
        public int ExpiredExtensionRequests { get; set; }
        public int TotalRequestsThisWeek { get; set; }

        // Filter properties
        [BindProperty(SupportsGet = true)]
        public string ActiveTab { get; set; } = "all";

        [BindProperty(SupportsGet = true)]
        public string RequestType { get; set; } = "all"; // all, edit, extension

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; } = "";

        [BindProperty(SupportsGet = true)]
        public int? SelectedTeacherId { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        // Data
        public List<UnifiedRequestViewModel> AllRequests { get; set; } = new();
        public List<TeacherEntity> Teachers { get; set; } = new();

        public class UnifiedRequestViewModel
        {
            public int Id { get; set; }
            public string RequestType { get; set; } = "edit"; // edit or extension
            public string ExtensionRequestType { get; set; } = ""; // For extension requests: Missed or Edit
            public string TeacherName { get; set; } = "";
            public string TeacherEmail { get; set; } = "";
            public string CourseName { get; set; } = "";
            public string CourseTitle { get; set; } = "";
            public string SectionName { get; set; } = "";
            public string BadgeName { get; set; } = "";
            public DateTime LectureDate { get; set; }
            public TimeSpan LectureStartTime { get; set; }
            public TimeSpan LectureEndTime { get; set; }
            public string Reason { get; set; } = "";
            public DateTime RequestedAt { get; set; }
            public string Status { get; set; } = "";
            public DateTime? ReviewedAt { get; set; }
            public DateTime? ProcessedAt { get; set; }
            public string? ProcessedByName { get; set; }
            public string? AdminNotes { get; set; }
            public DateTime? ExtendsUntil { get; set; }
            public int HoursRemaining { get; set; }
            public int LectureId { get; set; }
        }

        public async Task OnGetAsync(string? tab, string? requestType, string? searchTerm, int? teacherId, DateTime? startDate, DateTime? endDate)
        {
            ActiveTab = tab ?? "all";
            RequestType = requestType ?? "all";
            SearchTerm = searchTerm ?? "";
            SelectedTeacherId = teacherId;
            StartDate = startDate;
            EndDate = endDate;

            var now = DateTime.Now;

            // Load teachers for filter
            Teachers = await _context.Teachers
                .Include(t => t.User)
                .OrderBy(t => t.User.FullName)
                .ToListAsync();

            // Auto-expire extension requests that are pending for more than 24 hours
            var expiredExtensionRequests = await _context.AttendanceExtensionRequests
                .Where(r => r.Status == "Pending" && r.RequestedAt.AddHours(24) < now)
                .ToListAsync();

            foreach (var request in expiredExtensionRequests)
            {
                request.Status = "Expired";
            }

            if (expiredExtensionRequests.Any())
            {
                await _context.SaveChangesAsync();
            }

            // Get all edit requests
            var editRequestsQuery = _context.AttendanceEditRequests
                .Include(r => r.Teacher)
                    .ThenInclude(t => t.User)
                .Include(r => r.Lecture)
                    .ThenInclude(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                            .ThenInclude(tc => tc.Course)
                .Include(r => r.Lecture)
                    .ThenInclude(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                            .ThenInclude(tc => tc.Section)
                                .ThenInclude(s => s.Badge)
                .AsQueryable();

            // Get all extension requests
            var extensionRequestsQuery = _context.AttendanceExtensionRequests
                .Include(r => r.Teacher)
                    .ThenInclude(t => t.User)
                .Include(r => r.Lecture)
                    .ThenInclude(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                            .ThenInclude(tc => tc.Course)
                .Include(r => r.Lecture)
                    .ThenInclude(l => l.TimetableRule)
                        .ThenInclude(tr => tr.TeacherCourse)
                            .ThenInclude(tc => tc.Section)
                                .ThenInclude(s => s.Badge)
                .Include(r => r.ApprovedByUser)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var search = SearchTerm.ToLower();
                editRequestsQuery = editRequestsQuery.Where(r =>
                    r.Teacher.User.FullName.ToLower().Contains(search) ||
                    r.Teacher.User.Email.ToLower().Contains(search) ||
                    r.Lecture.TimetableRule.TeacherCourse.Course.Code.ToLower().Contains(search) ||
                    r.Lecture.TimetableRule.TeacherCourse.Course.Title.ToLower().Contains(search) ||
                    r.Reason.ToLower().Contains(search)
                );

                extensionRequestsQuery = extensionRequestsQuery.Where(r =>
                    r.Teacher.User.FullName.ToLower().Contains(search) ||
                    r.Teacher.User.Email.ToLower().Contains(search) ||
                    r.Lecture.TimetableRule.TeacherCourse.Course.Code.ToLower().Contains(search) ||
                    r.Lecture.TimetableRule.TeacherCourse.Course.Title.ToLower().Contains(search) ||
                    r.Reason.ToLower().Contains(search)
                );
            }

            if (SelectedTeacherId.HasValue)
            {
                editRequestsQuery = editRequestsQuery.Where(r => r.TeacherId == SelectedTeacherId.Value);
                extensionRequestsQuery = extensionRequestsQuery.Where(r => r.TeacherId == SelectedTeacherId.Value);
            }

            if (StartDate.HasValue)
            {
                editRequestsQuery = editRequestsQuery.Where(r => r.Lecture.StartDateTime.Date >= StartDate.Value);
                extensionRequestsQuery = extensionRequestsQuery.Where(r => r.Lecture.StartDateTime.Date >= StartDate.Value);
            }

            if (EndDate.HasValue)
            {
                editRequestsQuery = editRequestsQuery.Where(r => r.Lecture.StartDateTime.Date <= EndDate.Value);
                extensionRequestsQuery = extensionRequestsQuery.Where(r => r.Lecture.StartDateTime.Date <= EndDate.Value);
            }

            // Execute queries
            var editRequests = await editRequestsQuery.ToListAsync();
            var extensionRequests = await extensionRequestsQuery.ToListAsync();

            // Convert to unified view models
            var unifiedRequests = new List<UnifiedRequestViewModel>();

            // Add edit requests
            unifiedRequests.AddRange(editRequests.Select(r => new UnifiedRequestViewModel
            {
                Id = r.Id,
                RequestType = "edit",
                ExtensionRequestType = "",
                TeacherName = r.Teacher?.User?.FullName ?? "N/A",
                TeacherEmail = r.Teacher?.User?.Email ?? "N/A",
                CourseName = r.Lecture?.TimetableRule?.TeacherCourse?.Course?.Code ?? "N/A",
                CourseTitle = r.Lecture?.TimetableRule?.TeacherCourse?.Course?.Title ?? "N/A",
                SectionName = r.Lecture?.TimetableRule?.TeacherCourse?.Section?.Name ?? "N/A",
                BadgeName = r.Lecture?.TimetableRule?.TeacherCourse?.Section?.Badge?.Name ?? "N/A",
                LectureDate = r.Lecture?.StartDateTime.Date ?? DateTime.MinValue,
                LectureStartTime = r.Lecture?.StartDateTime.TimeOfDay ?? TimeSpan.Zero,
                LectureEndTime = r.Lecture?.EndDateTime.TimeOfDay ?? TimeSpan.Zero,
                Reason = r.Reason,
                RequestedAt = r.RequestedAt,
                Status = r.Status,
                ReviewedAt = r.ReviewedAt,
                ProcessedAt = r.ReviewedAt,
                LectureId = r.LectureId,
                HoursRemaining = 0
            }));

            // Add extension requests
            unifiedRequests.AddRange(extensionRequests.Select(r => new UnifiedRequestViewModel
            {
                Id = r.Id,
                RequestType = "extension",
                ExtensionRequestType = r.RequestType,
                TeacherName = r.Teacher?.User?.FullName ?? "N/A",
                TeacherEmail = r.Teacher?.User?.Email ?? "N/A",
                CourseName = r.Lecture?.TimetableRule?.TeacherCourse?.Course?.Code ?? "N/A",
                CourseTitle = r.Lecture?.TimetableRule?.TeacherCourse?.Course?.Title ?? "N/A",
                SectionName = r.Lecture?.TimetableRule?.TeacherCourse?.Section?.Name ?? "N/A",
                BadgeName = r.Lecture?.TimetableRule?.TeacherCourse?.Section?.Badge?.Name ?? "N/A",
                LectureDate = r.Lecture?.StartDateTime.Date ?? DateTime.MinValue,
                LectureStartTime = r.Lecture?.StartDateTime.TimeOfDay ?? TimeSpan.Zero,
                LectureEndTime = r.Lecture?.EndDateTime.TimeOfDay ?? TimeSpan.Zero,
                Reason = r.Reason,
                RequestedAt = r.RequestedAt,
                Status = r.Status,
                ProcessedAt = r.ApprovedAt ?? r.RejectedAt,
                ProcessedByName = r.ApprovedByUser?.FullName,
                AdminNotes = r.AdminNotes,
                ExtendsUntil = r.ExtendsUntil,
                LectureId = r.LectureId,
                HoursRemaining = r.RequestedAt.AddHours(24) > now && r.Status == "Pending"
                    ? (int)(r.RequestedAt.AddHours(24) - now).TotalHours
                    : 0
            }));

            // Sort by requested date (newest first)
            AllRequests = unifiedRequests.OrderByDescending(r => r.RequestedAt).ToList();

            // Calculate statistics
            TotalEditRequests = editRequests.Count;
            TotalExtensionRequests = extensionRequests.Count;
            PendingEditRequests = editRequests.Count(r => r.Status == "Pending");
            PendingExtensionRequests = extensionRequests.Count(r => r.Status == "Pending");
            ApprovedEditRequests = editRequests.Count(r => r.Status == "Approved");
            ApprovedExtensionRequests = extensionRequests.Count(r => r.Status == "Approved");
            RejectedEditRequests = editRequests.Count(r => r.Status == "Rejected");
            RejectedExtensionRequests = extensionRequests.Count(r => r.Status == "Rejected");
            ExpiredExtensionRequests = extensionRequests.Count(r => r.Status == "Expired");

            var weekStart = DateTime.Now.AddDays(-7);
            TotalRequestsThisWeek = unifiedRequests.Count(r => r.RequestedAt >= weekStart);
        }

        public async Task<IActionResult> OnPostApproveEditAsync(int id, string? adminNotes)
        {
            try
            {
                var request = await _context.AttendanceEditRequests.FindAsync(id);
                if (request == null)
                {
                    TempData["ErrorMessage"] = "Edit request not found.";
                    return RedirectToPage();
                }

                if (request.Status != "Pending")
                {
                    TempData["ErrorMessage"] = "This request has already been processed.";
                    return RedirectToPage();
                }

                request.Status = "Approved";
                request.ReviewedAt = DateTime.Now;
                // Note: AttendanceEditRequest doesn't have AdminNotes field currently
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Edit request approved successfully. Teacher can now edit the attendance.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error approving request: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostRejectEditAsync(int id, string? adminNotes)
        {
            try
            {
                var request = await _context.AttendanceEditRequests.FindAsync(id);
                if (request == null)
                {
                    TempData["ErrorMessage"] = "Edit request not found.";
                    return RedirectToPage();
                }

                if (request.Status != "Pending")
                {
                    TempData["ErrorMessage"] = "This request has already been processed.";
                    return RedirectToPage();
                }

                request.Status = "Rejected";
                request.ReviewedAt = DateTime.Now;
                // Note: AttendanceEditRequest doesn't have AdminNotes field currently
                await _context.SaveChangesAsync();

                TempData["ErrorMessage"] = "Edit request rejected.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error rejecting request: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostApproveExtensionAsync(int id, string? adminNotes)
        {
            try
            {
                var request = await _context.AttendanceExtensionRequests
                    .Include(r => r.Lecture)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (request == null)
                {
                    TempData["ErrorMessage"] = "Extension request not found.";
                    return RedirectToPage();
                }

                if (request.Status != "Pending")
                {
                    TempData["ErrorMessage"] = "This request has already been processed.";
                    return RedirectToPage();
                }

                request.Status = "Approved";
                request.ApprovedAt = DateTime.Now;
                request.ApprovedByUserId = null; // Set to null for now to avoid FK constraint issues
                request.AdminNotes = adminNotes;
                request.ExtendsUntil = DateTime.Now.AddHours(24);

                // Extend the lecture's attendance marking time
                if (request.Lecture != null)
                {
                    request.Lecture.AttendanceDeadline = request.ExtendsUntil;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Extension request approved! Attendance marking extended until {request.ExtendsUntil:MMM dd, yyyy hh:mm tt}";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error approving extension request: {ex.Message}";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostRejectExtensionAsync(int id, string? adminNotes)
        {
            try
            {
                var request = await _context.AttendanceExtensionRequests.FindAsync(id);
                if (request == null)
                {
                    TempData["ErrorMessage"] = "Extension request not found.";
                    return RedirectToPage();
                }

                if (request.Status != "Pending")
                {
                    TempData["ErrorMessage"] = "This request has already been processed.";
                    return RedirectToPage();
                }

                request.Status = "Rejected";
                request.RejectedAt = DateTime.Now;
                request.ApprovedByUserId = null; // Set to null for now to avoid FK constraint issues
                request.AdminNotes = adminNotes ?? "Request rejected by admin.";

                await _context.SaveChangesAsync();

                TempData["ErrorMessage"] = "Extension request rejected.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error rejecting extension request: {ex.Message}";
                return RedirectToPage();
            }
        }

        public class EditRequestViewModel
        {
            public int Id { get; set; }
            public string TeacherName { get; set; } = string.Empty;
            public string TeacherEmail { get; set; } = string.Empty;
            public string CourseName { get; set; } = string.Empty;
            public string CourseTitle { get; set; } = string.Empty;
            public string SectionName { get; set; } = string.Empty;
            public string BadgeName { get; set; } = string.Empty;
            public DateTime LectureDate { get; set; }
            public TimeSpan LectureStartTime { get; set; }
            public TimeSpan LectureEndTime { get; set; }
            public string Reason { get; set; } = string.Empty;
            public DateTime RequestedAt { get; set; }
            public DateTime? ReviewedAt { get; set; }
            public string Status { get; set; } = string.Empty;
            public int LectureId { get; set; }
        }
    }
}
