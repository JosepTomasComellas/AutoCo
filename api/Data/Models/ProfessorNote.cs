namespace AutoCo.Api.Data.Models;

/// <summary>Nota/comentari que el professor afegeix sobre un alumne dins d'una activitat.</summary>
public class ProfessorNote
{
    public int Id { get; set; }

    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public string Note { get; set; } = "";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
