namespace attendence.Domain.Entities
{
    public class AttendanceExtensionRequest
    {
        public int Id { get; set; }
        public int LectureId { get; set; }
        public int TeacherId { get; set; }
        public string RequestType { get; set; } = "Missed"; // Missed (never marked) or Edit (already marked, needs changes)
        public string Reason { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public int? ApprovedByUserId { get; set; }
        public string? AdminNotes { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Expired
        public DateTime? ExtendsUntil { get; set; } // 24 hours after approval
        
        // Navigation properties
        public Lecture Lecture { get; set; } = null!;
        public attendence.Domain.Entities.Teacher Teacher { get; set; } = null!;
        public User? ApprovedByUser { get; set; }
    }
}
