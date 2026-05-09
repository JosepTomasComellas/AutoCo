namespace AutoCo.Api.Data.Models;

public class ProfessorClass
{
    public int ProfessorId { get; set; }
    public int ClassId     { get; set; }

    public Professor Professor { get; set; } = null!;
    public Class     Class     { get; set; } = null!;
}
