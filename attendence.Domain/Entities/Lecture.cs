namespace attendence.Domain.Entities;

public class Lecture
{
    public int Id { get; set; }
    public int TimetableRuleId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public DateTime? AttendanceDeadline { get; set; } // Extended deadline if approved
    public string Status { get; set; } = "Scheduled"; // Scheduled/Open/Locked/Cancelled
    
    // Special Session Fields
    public string LectureType { get; set; } = "Regular"; // Regular, Quiz, Test, Lab, Practical, Workshop, GuestLecture, Review, MakeUp, Extra
    public int? CreatedByTeacherId { get; set; } // Null for regular lectures, TeacherId for special sessions
    public string? Description { get; set; } // Optional description for special sessions

    // Navigation properties
    public TimetableRule TimetableRule { get; set; } = null!;
    public Teacher? CreatedByTeacher { get; set; }
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
    public ICollection<AttendanceEditRequest> AttendanceEditRequests { get; set; } = new List<AttendanceEditRequest>();
    public ICollection<AttendanceExtensionRequest> AttendanceExtensionRequests { get; set; } = new List<AttendanceExtensionRequest>();
}