namespace AutoCo.Api.Data.Models;

public class ModuleExclusion
{
    public int Id        { get; set; }
    public int ModuleId  { get; set; }
    public int StudentId { get; set; }

    public Module  Module  { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
