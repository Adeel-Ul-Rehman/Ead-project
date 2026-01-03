namespace attendence.Domain.Entities;

public class Section
{
    public int Id { get; set; }
    public int BadgeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Semester { get; set; }
    public string Session { get; set; } = string.Empty;

    // Navigation properties
    public Badge Badge { get; set; } = null!;
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<TeacherCourse> TeacherCourses { get; set; } = new List<TeacherCourse>();
}