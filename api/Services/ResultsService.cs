using AutoCo.Api.Data;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text;
using System.Text.Json;

namespace AutoCo.Api.Services;

public interface IResultsService
{
    Task<ActivityResultsDto?> GetResultsAsync(int activityId, int professorId, bool isAdmin);
    Task<(byte[] Content, string FileName)?> ExportCsvAsync(int activityId, int professorId, bool isAdmin);
    Task<ActivityChartDto?> GetChartAsync(int activityId, int professorId, bool isAdmin);
    Task InvalidateCacheAsync(int activityId);
}

public class ResultsService(AppDbContext db, IDistributedCache cache) : IResultsService
{
    private static readonly DistributedCacheEntryOptions _cacheOpts = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string ResultsCacheKey(int id) => $"autoco:results:{id}";
    private static string ChartCacheKey(int id)   => $"autoco:chart:{id}";

    public async Task InvalidateCacheAsync(int activityId)
    {
        await cache.RemoveAsync(ResultsCacheKey(activityId));
        await cache.RemoveAsync(ChartCacheKey(activityId));
    }

    public async Task<ActivityResultsDto?> GetResultsAsync(int activityId, int professorId, bool isAdmin)
    {
        var cacheKey = ResultsCacheKey(activityId);
        var cached   = await cache.GetStringAsync(cacheKey);
        if (cached is not null)
            return JsonSerializer.Deserialize<ActivityResultsDto>(cached, _jsonOpts);

        var result = await ComputeResultsAsync(activityId, professorId, isAdmin);
        if (result is not null)
            await cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(result, _jsonOpts), _cacheOpts);
        return result;
    }

    private async Task<ActivityResultsDto?> ComputeResultsAsync(int activityId, int professorId, bool isAdmin)
    {
        var activity = await db.Activities
            .Include(a => a.Module).ThenInclude(m => m.Professor)
            .Include(a => a.Module).ThenInclude(m => m.Class)
            .Include(a => a.Groups).ThenInclude(g => g.Members).ThenInclude(m => m.Student)
            .FirstOrDefaultAsync(a => a.Id == activityId &&
                (isAdmin || a.Module.ProfessorId == professorId));

        if (activity is null) return null;

        // Carrega TOTES les avaluacions de l'activitat en una sola consulta (elimina N+1)
        var allEvals = await db.Evaluations
            .Include(e => e.Scores)
            .Include(e => e.Evaluator)
            .Where(e => e.ActivityId == activityId)
            .ToListAsync();

        // Indexos en memòria per accés O(1)
        var selfEvalByStudentId = allEvals
            .Where(e => e.IsSelf)
            .ToDictionary(e => e.EvaluatorId);

        var peerEvalsByStudentId = allEvals
            .Where(e => !e.IsSelf)
            .GroupBy(e => e.EvaluatedId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allMembers = activity.Groups
            .SelectMany(g => g.Members.Select(m => new { m.Student, GroupName = g.Name }))
            .OrderBy(x => x.GroupName).ThenBy(x => x.Student.NumLlista)
            .ToList();

        var studentResults = new List<StudentResultDto>();

        foreach (var member in allMembers)
        {
            var s = member.Student;

            var selfEval = selfEvalByStudentId.GetValueOrDefault(s.Id);
            var selfScores = selfEval?.Scores.ToDictionary(sc => sc.CriteriaKey, sc => (double?)sc.Score)
                ?? Data.Criteria.Keys.ToDictionary(k => k, _ => (double?)null);

            var peerEvals = peerEvalsByStudentId.GetValueOrDefault(s.Id) ?? [];

            var peerDtos = peerEvals.Select(e => new PeerEvaluationDto(
                e.EvaluatorId,
                e.Evaluator.NomComplet,
                e.Scores.ToDictionary(sc => sc.CriteriaKey, sc => sc.Score),
                e.Comment)).ToList();

            var avgCoScores = Data.Criteria.Keys.ToDictionary(
                k => k,
                k => {
                    var vals = peerEvals
                        .Select(e => e.Scores.FirstOrDefault(sc => sc.CriteriaKey == k)?.Score)
                        .Where(v => v.HasValue).Select(v => v!.Value).ToList();
                    return vals.Count > 0 ? (double?)vals.Average() : null;
                });

            var allCoVals    = avgCoScores.Values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            var avgGlobal    = allCoVals.Count > 0 ? (double?)allCoVals.Average() : null;
            var allAutVals   = selfScores.Values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            var autAvgGlobal = allAutVals.Count > 0 ? (double?)allAutVals.Average() : null;

            studentResults.Add(new StudentResultDto(
                s.Id, s.Nom, s.Cognoms, s.NomComplet, s.Email, s.NumLlista, member.GroupName,
                selfScores, selfEval?.Comment,
                peerDtos, avgCoScores, avgGlobal, autAvgGlobal, peerEvals.Count));
        }

        var actDto = new ActivityDto(
            activity.Id,
            activity.ModuleId, activity.Module.Code, activity.Module.Name,
            activity.Module.ClassId, activity.Module.Class.Name, activity.Module.Class.AcademicYear,
            activity.Module.Professor.NomComplet,
            activity.Name, activity.Description, activity.IsOpen, activity.CreatedAt,
            activity.Groups.Count, allMembers.Count);

        var criteriaDto = Data.Criteria.All
            .Select(c => new CriteriaDto(c.Key, c.Label)).ToList();

        return new ActivityResultsDto(actDto, studentResults, criteriaDto);
    }

    public async Task<ActivityChartDto?> GetChartAsync(int activityId, int professorId, bool isAdmin)
    {
        var cacheKey = ChartCacheKey(activityId);
        var cached   = await cache.GetStringAsync(cacheKey);
        if (cached is not null)
            return JsonSerializer.Deserialize<ActivityChartDto>(cached, _jsonOpts);

        var result = await ComputeChartAsync(activityId, professorId, isAdmin);
        if (result is not null)
            await cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(result, _jsonOpts), _cacheOpts);
        return result;
    }

    private async Task<ActivityChartDto?> ComputeChartAsync(int activityId, int professorId, bool isAdmin)
    {
        var activity = await db.Activities
            .Include(a => a.Module).ThenInclude(m => m.Professor)
            .Include(a => a.Module).ThenInclude(m => m.Class)
            .Include(a => a.Groups).ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(a => a.Id == activityId &&
                (isAdmin || a.Module.ProfessorId == professorId));
        if (activity is null) return null;

        var criteriaAll = Data.Criteria.All.Select(c => new CriteriaDto(c.Key, c.Label)).ToList();

        // Carrega TOTES les avaluacions en una sola consulta (elimina N+1)
        var allEvals = await db.Evaluations
            .Include(e => e.Scores)
            .Where(e => e.ActivityId == activityId)
            .ToListAsync();

        var selfEvalsByStudentId = allEvals
            .Where(e => e.IsSelf)
            .GroupBy(e => e.EvaluatorId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var peerEvalsByStudentId = allEvals
            .Where(e => !e.IsSelf)
            .GroupBy(e => e.EvaluatedId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var groupCharts = new List<GroupChartDto>();
        var criteriaDetail = Data.Criteria.All
            .Select(c => new CriteriaGroupChartDto(c.Key, c.Label, [])).ToList();

        foreach (var g in activity.Groups.OrderBy(g => g.Name))
        {
            var studentIds = g.Members.Select(m => m.StudentId).ToHashSet();

            var selfEvals = studentIds
                .SelectMany(id => selfEvalsByStudentId.GetValueOrDefault(id) ?? [])
                .ToList();

            var peerEvals = studentIds
                .SelectMany(id => peerEvalsByStudentId.GetValueOrDefault(id) ?? [])
                .ToList();

            double? avgAuto = selfEvals.Count > 0
                ? selfEvals.SelectMany(e => e.Scores).Select(s => s.Score)
                    .DefaultIfEmpty().Average().NullIfZero()
                : null;
            double? avgCo = peerEvals.Count > 0
                ? peerEvals.SelectMany(e => e.Scores).Select(s => s.Score)
                    .DefaultIfEmpty().Average().NullIfZero()
                : null;

            groupCharts.Add(new GroupChartDto(
                g.Name, avgAuto, avgCo,
                studentIds.Count,
                selfEvals.Select(e => e.EvaluatorId).Distinct().Count(),
                peerEvals.Select(e => e.EvaluatedId).Distinct().Count()));

            foreach (var cd in criteriaDetail)
            {
                double? autoK = selfEvals.Count > 0
                    ? selfEvals.SelectMany(e => e.Scores.Where(s => s.CriteriaKey == cd.CriteriaKey))
                        .Select(s => s.Score).DefaultIfEmpty(0).Average().NullIfZero()
                    : null;
                double? coK = peerEvals.Count > 0
                    ? peerEvals.SelectMany(e => e.Scores.Where(s => s.CriteriaKey == cd.CriteriaKey))
                        .Select(s => s.Score).DefaultIfEmpty(0).Average().NullIfZero()
                    : null;
                cd.Groups.Add(new CriteriaGroupValueDto(g.Name, autoK, coK));
            }
        }

        return new ActivityChartDto(
            activity.Id, activity.Name,
            activity.Module.Class.Name, activity.Module.Class.AcademicYear,
            groupCharts, criteriaAll, criteriaDetail);
    }

    public async Task<(byte[] Content, string FileName)?> ExportCsvAsync(
        int activityId, int professorId, bool isAdmin)
    {
        var results = await GetResultsAsync(activityId, professorId, isAdmin);
        if (results is null) return null;

        var sb = new StringBuilder();

        var curs = string.IsNullOrWhiteSpace(results.Activity.ClassAcademicYear)
            ? "" : $" ({results.Activity.ClassAcademicYear})";
        sb.AppendLine(string.Join(";", Escape("Classe"),     Escape(results.Activity.ClassName + curs)));
        sb.AppendLine(string.Join(";", Escape("Activitat"),  Escape(results.Activity.Name)));
        sb.AppendLine(string.Join(";", Escape("Descripció"), Escape(results.Activity.Description ?? "")));
        sb.AppendLine(string.Join(";", Escape("Data"),       Escape(DateTime.Now.ToString("dd/MM/yyyy HH:mm"))));
        sb.AppendLine();

        sb.AppendLine(string.Join(";",
            new[] { "NumAlumne", "Nom", "Cognoms", "Correu", "Grup", "CoEval", "AutEval" }
            .Select(Escape)));

        foreach (var s in results.Students.OrderBy(s => s.GroupName).ThenBy(s => s.NumLlista))
        {
            var row = new List<string>
            {
                s.NumLlista.ToString(),
                s.Nom,
                s.Cognoms,
                s.Email ?? "",
                s.GroupName,
                s.AvgGlobal.HasValue    ? s.AvgGlobal.Value.ToString("F2")    : "",
                s.AutAvgGlobal.HasValue ? s.AutAvgGlobal.Value.ToString("F2") : ""
            };
            sb.AppendLine(string.Join(";", row.Select(Escape)));
        }

        var nomFitxer = $"avaluacio_{results.Activity.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";
        return (Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(), nomFitxer);
    }

    private static string Escape(string v) => $"\"{v.Replace("\"", "\"\"")}\"";
}

file static class DoubleExtensions
{
    internal static double? NullIfZero(this double v) => v == 0d ? null : v;
}
