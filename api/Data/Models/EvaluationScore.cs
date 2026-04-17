namespace AutoCo.Api.Data.Models;

public class EvaluationScore
{
    public int    Id           { get; set; }
    public int    EvaluationId { get; set; }
    public string CriteriaKey  { get; set; } = null!;
    public double Score        { get; set; }

    public Evaluation Evaluation { get; set; } = null!;
}
