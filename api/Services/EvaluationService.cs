using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AutoCo.Api.Services;

public interface IEvaluationService
{
    Task<EvaluationFormDto?> GetFormAsync(int activityId, int studentId);
    Task<bool>               SaveAsync(int activityId, int studentId, SaveEvaluationsRequest req);
}

public class EvaluationService(AppDbContext db) : IEvaluationService
{
    public async Task<EvaluationFormDto?> GetFormAsync(int activityId, int studentId)
    {
        var activity = await db.Activities
            .Include(a => a.Class).ThenInclude(c => c.Professor)
            .Include(a => a.Groups).ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(a => a.Id == activityId);

        if (activity is null || !activity.IsOpen) return null;

        // Trobar el grup de l'alumne
        var group = activity.Groups.FirstOrDefault(g => g.Members.Any(m => m.StudentId == studentId));
        if (group is null) return null;

        // Membres del grup amb les seves dades
        var memberIds = group.Members.Select(m => m.StudentId).ToList();
        var students  = await db.Students
            .Where(s => memberIds.Contains(s.Id))
            .OrderBy(s => s.NumLlista)
            .ToListAsync();

        // Carregar avaluacions existents de l'alumne per a aquesta activitat
        var existingEvals = await db.Evaluations
            .Include(e => e.Scores)
            .Where(e => e.ActivityId == activityId && e.EvaluatorId == studentId)
            .ToListAsync();

        var entries = students.Select(s => {
            var eval = existingEvals.FirstOrDefault(e => e.EvaluatedId == s.Id);
            var scores = eval?.Scores.ToDictionary(sc => sc.CriteriaKey, sc => sc.Score)
                ?? [];
            return new EvaluationEntryDto(s.Id, s.NomComplet, s.Id == studentId, scores, eval?.Comment);
        }).ToList();

        var actDto   = new ActivityDto(activity.Id, activity.ClassId, activity.Class.Name,
            activity.Class.AcademicYear, activity.Class.Professor.NomComplet, activity.Name, activity.Description,
            activity.IsOpen, activity.CreatedAt, activity.Groups.Count,
            activity.Groups.SelectMany(g => g.Members).Select(m => m.StudentId).Distinct().Count());

        var groupDto = new GroupDto(group.Id, group.ActivityId, group.Name,
            students.Select(s => new StudentDto(s.Id, s.ClassId, s.Nom, s.Cognoms, s.NomComplet, s.NumLlista, s.CreatedAt)).ToList());

        return new EvaluationFormDto(actDto, groupDto, entries);
    }

    public async Task<bool> SaveAsync(int activityId, int studentId, SaveEvaluationsRequest req)
    {
        var activity = await db.Activities.FindAsync(activityId);
        if (activity is null || !activity.IsOpen) return false;

        // Verificar que l'alumne pertany a un grup d'aquesta activitat
        var group = await db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.ActivityId == activityId
                && g.Members.Any(m => m.StudentId == studentId));
        if (group is null) return false;

        var validMemberIds = group.Members.Select(m => m.StudentId).ToHashSet();

        foreach (var entry in req.Evaluations)
        {
            // Només es pot avaluar membres del grup
            if (!validMemberIds.Contains(entry.EvaluatedId)) continue;

            var isSelf = entry.EvaluatedId == studentId;

            // Inserir o actualitzar l'avaluació
            var eval = await db.Evaluations
                .Include(e => e.Scores)
                .FirstOrDefaultAsync(e => e.ActivityId == activityId
                    && e.EvaluatorId == studentId
                    && e.EvaluatedId == entry.EvaluatedId);

            if (eval is null)
            {
                eval = new Evaluation
                {
                    ActivityId  = activityId,
                    EvaluatorId = studentId,
                    EvaluatedId = entry.EvaluatedId,
                    IsSelf      = isSelf,
                    Comment     = entry.Comment?.Trim(),
                    UpdatedAt   = DateTime.UtcNow
                };
                db.Evaluations.Add(eval);
                await db.SaveChangesAsync(); // necessitem l'Id
            }
            else
            {
                eval.Comment   = entry.Comment?.Trim();
                eval.UpdatedAt = DateTime.UtcNow;
            }

            // Actualitzar puntuacions per criteri
            foreach (var (key, score) in entry.Scores)
            {
                if (score < 1 || score > 10) continue;

                var existing = eval.Scores.FirstOrDefault(s => s.CriteriaKey == key);
                if (existing is not null)
                {
                    existing.Score = score;
                }
                else
                {
                    db.EvaluationScores.Add(new EvaluationScore
                    {
                        EvaluationId = eval.Id,
                        CriteriaKey  = key,
                        Score        = score
                    });
                }
            }
        }

        await db.SaveChangesAsync();
        return true;
    }
}
