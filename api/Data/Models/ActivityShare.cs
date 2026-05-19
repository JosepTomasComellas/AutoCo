namespace AutoCo.Api.Data.Models;

public class ActivityShare
{
    public int ActivityId  { get; set; }
    public int ProfessorId { get; set; }

    public Activity  Activity  { get; set; } = null!;
    public Professor Professor { get; set; } = null!;
}
