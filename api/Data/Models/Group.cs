namespace AutoCo.Api.Data.Models;

public class Group
{
    public int    Id         { get; set; }
    public int    ActivityId { get; set; }
    public string Name       { get; set; } = null!;
    public int    OrderIndex { get; set; } = 0;

    public Activity                Activity { get; set; } = null!;
    public ICollection<GroupMember> Members  { get; set; } = [];
}
