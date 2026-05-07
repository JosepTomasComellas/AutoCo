namespace AutoCo.Api.Data.Models;

public class ActivityCriterion
{
    public int      Id         { get; set; }
    public int      ActivityId { get; set; }
    public Activity Activity   { get; set; } = null!;
    public string   Key        { get; set; } = "";
    public string   Label      { get; set; } = "";
    public int      OrderIndex { get; set; }
    public int      Weight     { get; set; } = 1;
}
