namespace AutoCo.Api.Data.Models;

public class Class
{
    public int    Id           { get; set; }
    public int    ProfessorId  { get; set; }
    public string Name         { get; set; } = null!;
    public string? AcademicYear { get; set; }
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    public Professor             Professor  { get; set; } = null!;
    public ICollection<Student>  Students   { get; set; } = [];
    public ICollection<Module>   Modules    { get; set; } = [];
}
