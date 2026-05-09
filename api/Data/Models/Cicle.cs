namespace AutoCo.Api.Data.Models;

public class Cicle
{
    public int    Id        { get; set; }
    public string Name      { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Class> Classes { get; set; } = [];
}
