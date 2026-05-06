namespace AutoCo.Api.Data.Models;

public class AdminAuditLog
{
    public int      Id        { get; set; }
    public string   Action    { get; set; } = "";
    public int?     ActorId   { get; set; }
    public string?  ActorName { get; set; }
    public string?  Details   { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
