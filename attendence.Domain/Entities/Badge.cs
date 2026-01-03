namespace attendence.Domain.Entities;

public class Badge
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    public ICollection<Section> Sections { get; set; } = new List<Section>();
}