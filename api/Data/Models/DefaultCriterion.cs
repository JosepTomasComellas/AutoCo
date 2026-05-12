namespace AutoCo.Api.Data.Models;

public class DefaultCriterion
{
    public int    Id         { get; set; }
    public string Key        { get; set; } = null!;
    public string Label      { get; set; } = null!;
    public int    Weight     { get; set; } = 1;
    public int    OrderIndex { get; set; }
}
