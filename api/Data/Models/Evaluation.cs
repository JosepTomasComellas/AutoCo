namespace AutoCo.Api.Data.Models;

public class Evaluation
{
    public int    Id          { get; set; }
    public int    ActivityId  { get; set; }
    public int    EvaluatorId { get; set; }
    public int    EvaluatedId { get; set; }
    public bool   IsSelf      { get; set; }
    public string? Comment    { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Activity  Activity  { get; set; } = null!;
    public Student   Evaluator { get; set; } = null!;
    public Student   Evaluated { get; set; } = null!;
    public ICollection<EvaluationScore> Scores { get; set; } = [];
}
