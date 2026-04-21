using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoCo.Api.Services;

public interface IBackupService
{
    Task<BackupDto>              ExportAsync();
    Task<ImportResult>           ImportAsync(BackupDto backup);
    Task<List<BackupFileInfoDto>> ListFilesAsync();
    Task<BackupFileInfoDto>      CreateFileAsync();
    Task<(byte[] Data, string Name)?> DownloadFileAsync(string name);
    Task<bool>                   DeleteFileAsync(string name);
    Task<ImportResult>           RestoreFileAsync(string name);
}

public class BackupService(AppDbContext db, IConfiguration cfg, ILogger<BackupService> logger) : IBackupService
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented             = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition    = JsonIgnoreCondition.WhenWritingNull
    };

    private string BackupsDir => cfg["Backup:Path"] ?? "/app/backups";

    private void EnsureDir() => Directory.CreateDirectory(BackupsDir);

    // ── Export ────────────────────────────────────────────────────────────────
    public async Task<BackupDto> ExportAsync()
    {
        var professors = await db.Professors.AsNoTracking().ToListAsync();
        var classes    = await db.Classes.AsNoTracking().ToListAsync();
        var students   = await db.Students.AsNoTracking().ToListAsync();
        var modules    = await db.Modules.AsNoTracking().ToListAsync();
        var exclusions = await db.ModuleExclusions.AsNoTracking().ToListAsync();
        var activities = await db.Activities.AsNoTracking().ToListAsync();
        var groups     = await db.Groups.AsNoTracking().ToListAsync();
        var members    = await db.GroupMembers.AsNoTracking().ToListAsync();
        var evals      = await db.Evaluations.AsNoTracking().Include(e => e.Scores).ToListAsync();

        var classDtos = classes.Select(c => new ClassBackupDto(
            c.Id, c.Name, c.AcademicYear, c.CreatedAt,
            students.Where(s => s.ClassId == c.Id)
                .Select(s => new StudentBackupDto(s.Id, s.Nom, s.Cognoms, s.NumLlista,
                    s.Email, s.PasswordHash, s.CreatedAt)).ToList(),
            modules.Where(m => m.ClassId == c.Id)
                .Select(m => new ModuleBackupDto(m.Id, m.ProfessorId, m.Code, m.Name, m.CreatedAt,
                    exclusions.Where(e => e.ModuleId == m.Id).Select(e => e.StudentId).ToList()))
                .ToList()
        )).ToList();

        var activityDtos = activities.Select(a => new ActivityBackupDto(
            a.Id, a.ModuleId, a.Name, a.Description, a.IsOpen, a.CreatedAt,
            groups.Where(g => g.ActivityId == a.Id)
                .Select(g => new GroupBackupDto(g.Id, g.Name,
                    members.Where(m => m.GroupId == g.Id).Select(m => m.StudentId).ToList()))
                .ToList(),
            evals.Where(e => e.ActivityId == a.Id)
                .Select(e => new EvaluationBackupDto(
                    e.EvaluatorId, e.EvaluatedId, e.IsSelf,
                    e.Scores.ToDictionary(s => s.CriteriaKey, s => s.Score),
                    e.Comment, e.UpdatedAt))
                .ToList()
        )).ToList();

        return new BackupDto("1.0", DateTime.UtcNow,
            professors.Select(p => new ProfessorBackupDto(
                p.Id, p.Email, p.Nom, p.Cognoms, p.IsAdmin, p.PasswordHash, p.CreatedAt)).ToList(),
            classDtos, activityDtos);
    }

    // ── Import (substitueix totes les dades) ──────────────────────────────────
    public async Task<ImportResult> ImportAsync(BackupDto bk)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Esborrar en ordre de dependències FK
            await db.EvaluationScores.ExecuteDeleteAsync();
            await db.Evaluations.ExecuteDeleteAsync();
            await db.GroupMembers.ExecuteDeleteAsync();
            await db.Groups.ExecuteDeleteAsync();
            await db.Activities.ExecuteDeleteAsync();
            await db.ModuleExclusions.ExecuteDeleteAsync();
            await db.Modules.ExecuteDeleteAsync();
            await db.Students.ExecuteDeleteAsync();
            await db.Classes.ExecuteDeleteAsync();
            await db.Professors.ExecuteDeleteAsync();

            // Mapes: ID original → ID nou assignat per la BD
            var profMap    = new Dictionary<int, int>();
            var classMap   = new Dictionary<int, int>();
            var studentMap = new Dictionary<int, int>();
            var moduleMap  = new Dictionary<int, int>();
            var groupMap   = new Dictionary<int, int>();
            var actMap     = new Dictionary<int, int>();

            // Professors
            foreach (var p in bk.Professors)
            {
                var ent = new Professor
                {
                    Email = p.Email, Nom = p.Nom, Cognoms = p.Cognoms,
                    IsAdmin = p.IsAdmin, PasswordHash = p.PasswordHash, CreatedAt = p.CreatedAt
                };
                db.Professors.Add(ent);
                await db.SaveChangesAsync();
                profMap[p.Id] = ent.Id;
            }

            // Classes + alumnes + mòduls
            foreach (var c in bk.Classes)
            {
                var classEnt = new Class
                    { Name = c.Name, AcademicYear = c.AcademicYear, CreatedAt = c.CreatedAt };
                db.Classes.Add(classEnt);
                await db.SaveChangesAsync();
                classMap[c.Id] = classEnt.Id;

                foreach (var s in c.Students)
                {
                    var sEnt = new Student
                    {
                        ClassId = classEnt.Id, Nom = s.Nom, Cognoms = s.Cognoms,
                        NumLlista = s.NumLlista, Email = s.Email,
                        PasswordHash = s.PasswordHash, CreatedAt = s.CreatedAt
                    };
                    db.Students.Add(sEnt);
                    await db.SaveChangesAsync();
                    studentMap[s.Id] = sEnt.Id;
                }

                foreach (var m in c.Modules)
                {
                    if (!profMap.TryGetValue(m.ProfessorId, out var newProfId)) continue;
                    var mEnt = new Module
                    {
                        ClassId = classEnt.Id, ProfessorId = newProfId,
                        Code = m.Code, Name = m.Name, CreatedAt = m.CreatedAt
                    };
                    db.Modules.Add(mEnt);
                    await db.SaveChangesAsync();
                    moduleMap[m.Id] = mEnt.Id;

                    foreach (var exclStudentId in m.ExcludedStudentIds)
                    {
                        if (!studentMap.TryGetValue(exclStudentId, out var newStId)) continue;
                        db.ModuleExclusions.Add(new ModuleExclusion
                            { ModuleId = mEnt.Id, StudentId = newStId });
                    }
                    await db.SaveChangesAsync();
                }
            }

            // Activitats + grups + membres + avaluacions
            int totalEvals = 0;
            foreach (var a in bk.Activities)
            {
                if (!moduleMap.TryGetValue(a.ModuleId, out var newModId)) continue;
                var actEnt = new Activity
                {
                    ModuleId = newModId, Name = a.Name, Description = a.Description,
                    IsOpen = a.IsOpen, CreatedAt = a.CreatedAt
                };
                db.Activities.Add(actEnt);
                await db.SaveChangesAsync();
                actMap[a.Id] = actEnt.Id;

                foreach (var g in a.Groups)
                {
                    var gEnt = new Group { ActivityId = actEnt.Id, Name = g.Name };
                    db.Groups.Add(gEnt);
                    await db.SaveChangesAsync();
                    groupMap[g.Id] = gEnt.Id;

                    foreach (var sid in g.StudentIds)
                    {
                        if (!studentMap.TryGetValue(sid, out var newSid)) continue;
                        db.GroupMembers.Add(new GroupMember { GroupId = gEnt.Id, StudentId = newSid });
                    }
                    await db.SaveChangesAsync();
                }

                foreach (var ev in a.Evaluations)
                {
                    if (!studentMap.TryGetValue(ev.EvaluatorId, out var newEvaluatorId)) continue;
                    if (!studentMap.TryGetValue(ev.EvaluatedId, out var newEvaluatedId)) continue;

                    var evEnt = new Evaluation
                    {
                        ActivityId  = actEnt.Id,
                        EvaluatorId = newEvaluatorId,
                        EvaluatedId = newEvaluatedId,
                        IsSelf      = ev.IsSelf,
                        Comment     = ev.Comment,
                        UpdatedAt   = ev.UpdatedAt
                    };
                    db.Evaluations.Add(evEnt);
                    await db.SaveChangesAsync();

                    foreach (var (key, score) in ev.Scores)
                    {
                        db.EvaluationScores.Add(new EvaluationScore
                            { EvaluationId = evEnt.Id, CriteriaKey = key, Score = score });
                    }
                    await db.SaveChangesAsync();
                    totalEvals++;
                }
            }

            await tx.CommitAsync();

            return new ImportResult(true, null,
                bk.Professors.Count, bk.Classes.Count,
                bk.Classes.Sum(c => c.Students.Count),
                bk.Classes.Sum(c => c.Modules.Count),
                bk.Activities.Count, totalEvals);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            logger.LogError(ex, "Error important backup");
            return new ImportResult(false, "Error intern en importar el backup. Consulta els logs del servidor.", 0, 0, 0, 0, 0, 0);
        }
    }

    // ── Fitxers de còpia ──────────────────────────────────────────────────────
    public Task<List<BackupFileInfoDto>> ListFilesAsync()
    {
        EnsureDir();
        var files = new DirectoryInfo(BackupsDir)
            .GetFiles("backup_*.json")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new BackupFileInfoDto(f.Name, f.LastWriteTimeUtc, f.Length))
            .ToList();
        return Task.FromResult(files);
    }

    public async Task<BackupFileInfoDto> CreateFileAsync()
    {
        EnsureDir();
        var backup = await ExportAsync();
        var name   = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        var path   = Path.Combine(BackupsDir, name);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(backup, _json));
        var info   = new FileInfo(path);
        return new BackupFileInfoDto(name, info.LastWriteTimeUtc, info.Length);
    }

    public async Task<(byte[] Data, string Name)?> DownloadFileAsync(string name)
    {
        var path = SafePath(name);
        if (path is null || !File.Exists(path)) return null;
        return (await File.ReadAllBytesAsync(path), name);
    }

    public Task<bool> DeleteFileAsync(string name)
    {
        var path = SafePath(name);
        if (path is null || !File.Exists(path)) return Task.FromResult(false);
        File.Delete(path);
        return Task.FromResult(true);
    }

    public async Task<ImportResult> RestoreFileAsync(string name)
    {
        var path = SafePath(name);
        if (path is null || !File.Exists(path))
            return new ImportResult(false, "Fitxer no trobat", 0, 0, 0, 0, 0, 0);
        var json   = await File.ReadAllTextAsync(path);
        var backup = JsonSerializer.Deserialize<BackupDto>(json, _json);
        if (backup is null)
            return new ImportResult(false, "Format invàlid", 0, 0, 0, 0, 0, 0);
        return await ImportAsync(backup);
    }

    // Evita path traversal
    private string? SafePath(string name)
    {
        if (name.Contains('/') || name.Contains('\\') || name.Contains("..")) return null;
        if (!name.StartsWith("backup_") || !name.EndsWith(".json")) return null;
        return Path.Combine(BackupsDir, name);
    }
}
