namespace AutoCo.Api.Data.Models;

public class Activity
{
    public int    Id           { get; set; }
    public int    ModuleId     { get; set; }
    // Professor que ha creat l'activitat (pot diferir de Module.ProfessorId si és d'una classe assignada)
    public int?   CreatedByProfessorId { get; set; }
    public string Name                { get; set; } = null!;
    public string? Description { get; set; }
    public bool      IsOpen                { get; set; } = true;
    public bool      IsArchived            { get; set; } = false;
    public bool      ShowResultsToStudents { get; set; } = false;
    public DateTime? OpenAt               { get; set; }
    public DateTime? CloseAt             { get; set; }
    public DateTime  CreatedAt { get; set; } = DateTime.UtcNow;

    public Professor?                      CreatedByProfessor { get; set; }
    public Module                         Module   { get; set; } = null!;
    public ICollection<Group>             Groups   { get; set; } = [];
    public ICollection<ActivityCriterion> Criteria { get; set; } = [];
    public ICollection<ActivityShare>     Shares   { get; set; } = [];
}
