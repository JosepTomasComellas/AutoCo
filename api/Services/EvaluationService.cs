using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Text.Json;

namespace AutoCo.Api.Services;

public interface IEvaluationService
{
    Task<EvaluationFormDto?> GetFormAsync(int activityId, int studentId);
    Task<bool>               SaveAsync(int activityId, int studentId, SaveEvaluationsRequest req,
                                       IEmailService? email = null);
}

public class EvaluationService(AppDbContext db, IServiceScopeFactory scopeFactory,
    IConnectionMultiplexer redis, ILogger<EvaluationService> logger) : IEvaluationService
{
    public async Task<EvaluationFormDto?> GetFormAsync(int activityId, int studentId)
    {
        var activity = await db.Activities
            .Include(a => a.Module).ThenInclude(m => m.Professor)
            .Include(a => a.Module).ThenInclude(m => m.Class)
            .Include(a => a.Groups).ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(a => a.Id == activityId);

        if (activity is null || !activity.IsOpen) return null;

        var isExcluded = await db.ModuleExclusions
            .AnyAsync(e => e.Module.Activities.Any(a => a.Id == activityId) && e.StudentId == studentId);
        if (isExcluded) return null;

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

        var actDto   = new ActivityDto(
            activity.Id,
            activity.ModuleId, activity.Module.Code, activity.Module.Name,
            activity.Module.ClassId, activity.Module.Class.Name, activity.Module.Class.AcademicYear,
            activity.Module.Professor.NomComplet,
            activity.Name, activity.Description, activity.IsOpen, activity.CreatedAt,
            activity.Groups.Count,
            activity.Groups.SelectMany(g => g.Members).Select(m => m.StudentId).Distinct().Count());

        var groupDto = new GroupDto(group.Id, group.ActivityId, group.Name,
            students.Select(s => new StudentDto(s.Id, s.ClassId, s.Nom, s.Cognoms, s.NomComplet, s.NumLlista, s.Email, s.CreatedAt)).ToList());

        return new EvaluationFormDto(actDto, groupDto, entries);
    }

    // Valors vàlids de puntuació (1★=E, 2★=D, 3★=C, 4★=B, 5★=A)
    private static readonly HashSet<double> ValidScores = [1.0, 3.5, 5.0, 7.5, 10.0];

    public async Task<bool> SaveAsync(int activityId, int studentId, SaveEvaluationsRequest req,
                                       IEmailService? email = null)
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

        // Carregar totes les avaluacions existents d'un sol cop (evita N+1 i race conditions)
        var existingEvals = await db.Evaluations
            .Include(e => e.Scores)
            .Where(e => e.ActivityId == activityId && e.EvaluatorId == studentId)
            .ToListAsync();
        var evalByEvaluated = existingEvals.ToDictionary(e => e.EvaluatedId);

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            foreach (var entry in req.Evaluations)
            {
                if (!validMemberIds.Contains(entry.EvaluatedId)) continue;

                var isSelf = entry.EvaluatedId == studentId;

                if (!evalByEvaluated.TryGetValue(entry.EvaluatedId, out var eval))
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
                    await db.SaveChangesAsync(); // necessitem l'Id per als scores
                    evalByEvaluated[entry.EvaluatedId] = eval;
                }
                else
                {
                    eval.Comment   = entry.Comment?.Trim();
                    eval.UpdatedAt = DateTime.UtcNow;
                }

                foreach (var (key, score) in entry.Scores)
                {
                    if (!ValidScores.Contains(score)) continue;
                    var existing = eval.Scores.FirstOrDefault(s => s.CriteriaKey == key);
                    if (existing is not null)
                        existing.Score = score;
                    else
                        db.EvaluationScores.Add(new EvaluationScore
                            { EvaluationId = eval.Id, CriteriaKey = key, Score = score });
                }
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // ── Publicar participació actualitzada a Redis (temps real) ────
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope   = scopeFactory.CreateScope();
                    var scopedDb      = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var total = await scopedDb.GroupMembers
                        .Where(gm => gm.Group.ActivityId == activityId)
                        .Select(gm => gm.StudentId).Distinct().CountAsync();

                    var submitted = await scopedDb.Evaluations
                        .Where(e => e.ActivityId == activityId && e.IsSelf)
                        .Select(e => e.EvaluatorId).Distinct().CountAsync();

                    var dto     = new ParticipationDto(activityId, submitted, total);
                    var payload = JsonSerializer.Serialize(dto);
                    var pub     = redis.GetSubscriber();
                    await pub.PublishAsync(
                        RedisChannel.Literal($"autoco:participation:{activityId}"),
                        payload);
                }
                catch (Exception ex) { logger.LogWarning(ex, "Error publicant participació a Redis (activitat {Id})", activityId); }
            });

            // ── Log de l'avaluació enviada ─────────────────────────────────
            try
            {
                var student = await db.Students.FindAsync(studentId);
                db.ActivityLogs.Add(new ActivityLog
                {
                    ActivityId   = activityId,
                    ActivityName = activity.Name,
                    ActorName    = student?.NomComplet,
                    Action       = "evaluated",
                    Details      = $"Alumne del grup {group.Name}",
                    CreatedAt    = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex) { logger.LogWarning(ex, "Error desant log d'avaluació (activitat {Id})", activityId); }

            // ── Notificació si l'activitat és 100% completa ───────────────
            // IMPORTANT: usa un scope propi per evitar que el DbContext
            // scoped de la petició HTTP quedi disposat dins del Task.Run
            if (email?.IsEnabled == true)
            {
                var capturedActivityId   = activityId;
                var capturedActivityName = activity.Name;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope     = scopeFactory.CreateScope();
                        var scopedDb        = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var scopedEmail     = scope.ServiceProvider.GetRequiredService<IEmailService>();
                        if (!scopedEmail.IsEnabled) return;

                        var allGroupMembers = await scopedDb.GroupMembers
                            .Where(gm => gm.Group.ActivityId == capturedActivityId)
                            .Select(gm => gm.StudentId)
                            .Distinct()
                            .ToListAsync();

                        var submitted = await scopedDb.Evaluations
                            .Where(e => e.ActivityId == capturedActivityId && e.IsSelf)
                            .Select(e => e.EvaluatorId)
                            .Distinct()
                            .CountAsync();

                        if (submitted >= allGroupMembers.Count && allGroupMembers.Count > 0)
                        {
                            var mod = await scopedDb.Modules
                                .Include(m => m.Professor)
                                .Include(m => m.Class)
                                .FirstOrDefaultAsync(m => m.Activities.Any(a => a.Id == capturedActivityId));
                            if (mod?.Professor is not null)
                                await scopedEmail.SendActivityCompletedAsync(
                                    mod.Professor.Email, mod.Professor.NomComplet,
                                    capturedActivityName, mod.Class.Name, allGroupMembers.Count);
                        }
                    }
                    catch (Exception ex) { logger.LogWarning(ex, "Error enviant notificació de compleció (activitat {Id})", capturedActivityId); }
                });
            }

            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            return false;
        }
    }
}
