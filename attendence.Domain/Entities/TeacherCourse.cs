namespace attendence.Domain.Entities;

public class TeacherCourse
{
    public int Id { get; set; }
    public int TeacherId { get; set; }
    public int CourseId { get; set; }
    public int SectionId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.Now;

    // Navigation properties
    public Teacher Teacher { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public Section Section { get; set; } = null!;
    public ICollection<TimetableRule> TimetableRules { get; set; } = new List<TimetableRule>();
}