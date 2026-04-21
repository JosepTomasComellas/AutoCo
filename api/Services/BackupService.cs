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

            // ── Professors (1 SaveChanges) ────────────────────────────────────
            var profEnts = bk.Professors.Select(p => new Professor
            {
                Email = p.Email, Nom = p.Nom, Cognoms = p.Cognoms,
                IsAdmin = p.IsAdmin, PasswordHash = p.PasswordHash, CreatedAt = p.CreatedAt
            }).ToList();
            db.Professors.AddRange(profEnts);
            await db.SaveChangesAsync();
            var profMap = bk.Professors.Zip(profEnts)
                .ToDictionary(x => x.First.Id, x => x.Second.Id);

            // ── Classes (1 SaveChanges) ───────────────────────────────────────
            var classEnts = bk.Classes.Select(c => new Class
                { Name = c.Name, AcademicYear = c.AcademicYear, CreatedAt = c.CreatedAt }).ToList();
            db.Classes.AddRange(classEnts);
            await db.SaveChangesAsync();

            // ── Students (1 SaveChanges) ──────────────────────────────────────
            var studentPairs = new List<(StudentBackupDto Dto, Student Ent)>();
            for (int ci = 0; ci < bk.Classes.Count; ci++)
            {
                foreach (var s in bk.Classes[ci].Students)
                {
                    var ent = new Student
                    {
                        ClassId = classEnts[ci].Id, Nom = s.Nom, Cognoms = s.Cognoms,
                        NumLlista = s.NumLlista, Email = s.Email,
                        PasswordHash = s.PasswordHash, CreatedAt = s.CreatedAt
                    };
                    studentPairs.Add((s, ent));
                    db.Students.Add(ent);
                }
            }
            await db.SaveChangesAsync();
            var studentMap = studentPairs.ToDictionary(x => x.Dto.Id, x => x.Ent.Id);

            // ── Modules (1 SaveChanges) ───────────────────────────────────────
            var modulePairs = new List<(ModuleBackupDto Dto, Module Ent)>();
            for (int ci = 0; ci < bk.Classes.Count; ci++)
            {
                foreach (var m in bk.Classes[ci].Modules)
                {
                    if (!profMap.TryGetValue(m.ProfessorId, out var newProfId)) continue;
                    var ent = new Module
                    {
                        ClassId = classEnts[ci].Id, ProfessorId = newProfId,
                        Code = m.Code, Name = m.Name, CreatedAt = m.CreatedAt
                    };
                    modulePairs.Add((m, ent));
                    db.Modules.Add(ent);
                }
            }
            await db.SaveChangesAsync();
            var moduleMap = modulePairs.ToDictionary(x => x.Dto.Id, x => x.Ent.Id);

            // ── Exclusions (1 SaveChanges) ────────────────────────────────────
            foreach (var (mDto, mEnt) in modulePairs)
                foreach (var exclId in mDto.ExcludedStudentIds)
                    if (studentMap.TryGetValue(exclId, out var newStId))
                        db.ModuleExclusions.Add(new ModuleExclusion { ModuleId = mEnt.Id, StudentId = newStId });
            await db.SaveChangesAsync();

            // ── Activities (1 SaveChanges) ────────────────────────────────────
            var actPairs = new List<(ActivityBackupDto Dto, Activity Ent)>();
            foreach (var a in bk.Activities)
            {
                if (!moduleMap.TryGetValue(a.ModuleId, out var newModId)) continue;
                var ent = new Activity
                {
                    ModuleId = newModId, Name = a.Name, Description = a.Description,
                    IsOpen = a.IsOpen, CreatedAt = a.CreatedAt
                };
                actPairs.Add((a, ent));
                db.Activities.Add(ent);
            }
            await db.SaveChangesAsync();

            // ── Groups (1 SaveChanges) ────────────────────────────────────────
            var groupPairs = new List<(GroupBackupDto Dto, Group Ent)>();
            foreach (var (aDto, actEnt) in actPairs)
                foreach (var g in aDto.Groups)
                {
                    var ent = new Group { ActivityId = actEnt.Id, Name = g.Name };
                    groupPairs.Add((g, ent));
                    db.Groups.Add(ent);
                }
            await db.SaveChangesAsync();

            // ── Group Members (1 SaveChanges) ─────────────────────────────────
            foreach (var (gDto, gEnt) in groupPairs)
                foreach (var sid in gDto.StudentIds)
                    if (studentMap.TryGetValue(sid, out var newSid))
                        db.GroupMembers.Add(new GroupMember { GroupId = gEnt.Id, StudentId = newSid });
            await db.SaveChangesAsync();

            // ── Evaluations (1 SaveChanges) ───────────────────────────────────
            var evalPairs = new List<(Evaluation Ent, Dictionary<string, double> Scores)>();
            foreach (var (aDto, actEnt) in actPairs)
                foreach (var ev in aDto.Evaluations)
                {
                    if (!studentMap.TryGetValue(ev.EvaluatorId, out var newEvatorId)) continue;
                    if (!studentMap.TryGetValue(ev.EvaluatedId, out var newEvatedId)) continue;
                    var ent = new Evaluation
                    {
                        ActivityId  = actEnt.Id, EvaluatorId = newEvatorId, EvaluatedId = newEvatedId,
                        IsSelf = ev.IsSelf, Comment = ev.Comment, UpdatedAt = ev.UpdatedAt
                    };
                    evalPairs.Add((ent, ev.Scores));
                    db.Evaluations.Add(ent);
                }
            await db.SaveChangesAsync(); // obté els Ids per als Scores

            // ── Evaluation Scores (1 SaveChanges) ─────────────────────────────
            foreach (var (evEnt, scores) in evalPairs)
                foreach (var (key, score) in scores)
                    db.EvaluationScores.Add(new EvaluationScore
                        { EvaluationId = evEnt.Id, CriteriaKey = key, Score = score });
            await db.SaveChangesAsync();

            await tx.CommitAsync();

            return new ImportResult(true, null,
                bk.Professors.Count, bk.Classes.Count,
                bk.Classes.Sum(c => c.Students.Count),
                bk.Classes.Sum(c => c.Modules.Count),
                bk.Activities.Count, evalPairs.Count);
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
