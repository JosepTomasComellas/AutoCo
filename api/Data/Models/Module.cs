namespace AutoCo.Api.Data.Models;

public class Module
{
    public int    Id        { get; set; }
    public int    ClassId   { get; set; }
    public string Code      { get; set; } = null!;
    public string Name      { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Class               Class      { get; set; } = null!;
    public ICollection<Activity> Activities { get; set; } = [];
}
