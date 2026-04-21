namespace AutoCo.Api.Data.Models;

public class GroupMember
{
    public int Id        { get; set; }
    public int GroupId   { get; set; }
    public int StudentId { get; set; }

    public Group   Group   { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
