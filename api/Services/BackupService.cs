using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoCo.Api.Services;

public interface IBackupService
{
    Task<BackupDto>               ExportAsync();
    Task<byte[]>                  ExportZipAsync();
    Task<ImportResult>            ImportAsync(BackupDto backup);
    Task<List<BackupFileInfoDto>> ListFilesAsync();
    Task<BackupFileInfoDto>       CreateFileAsync();
    Task<BackupFileInfoDto>       CreateAutoBackupAsync(string type);
    Task                          ApplyRetentionAsync(string type, int maxCount);
    Task<(byte[] Data, string Name)?> DownloadFileAsync(string name);
    Task<bool>                    DeleteFileAsync(string name);
    Task<ImportResult>            RestoreFileAsync(string name);
}

public class BackupService(AppDbContext db, IConfiguration cfg, ILogger<BackupService> logger) : IBackupService
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented               = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
    };

    private string BackupsDir => cfg["Backup:Path"]      ?? "/app/backups";
    private string PhotosDir  => cfg["Photos:BasePath"]  ?? "/app/fotos";

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
        var criteria   = await db.ActivityCriteria.AsNoTracking().ToListAsync();
        var groups     = await db.Groups.AsNoTracking().ToListAsync();
        var members    = await db.GroupMembers.AsNoTracking().ToListAsync();
        var evals      = await db.Evaluations.AsNoTracking().Include(e => e.Scores).ToListAsync();
        var notes      = await db.ProfessorNotes.AsNoTracking().ToListAsync();
        var templates  = await db.ActivityTemplates.AsNoTracking().ToListAsync();

        var classDtos = classes.Select(c => new ClassBackupDto(
            c.Id, c.Name, c.AcademicYear, c.CreatedAt,
            students.Where(s => s.ClassId == c.Id)
                .Select(s => new StudentBackupDto(s.Id, s.Nom, s.Cognoms, s.NumLlista,
                    s.Email, s.PasswordHash, s.CreatedAt, s.PlainPasswordEncrypted, s.Dni)).ToList(),
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
                .ToList(),
            criteria.Where(c => c.ActivityId == a.Id)
                .OrderBy(c => c.OrderIndex)
                .Select(c => new CriterionBackupDto(c.Key, c.Label, c.OrderIndex, c.Weight))
                .ToList()
                .NullIfEmpty(),
            notes.Where(n => n.ActivityId == a.Id)
                .Select(n => new NoteBackupDto(n.StudentId, n.Note, n.UpdatedAt))
                .ToList()
                .NullIfEmpty(),
            a.ShowResultsToStudents, a.OpenAt, a.CloseAt
        )).ToList();

        var templateDtos = templates.Select(t => new TemplateBackupDto(
            t.Id, t.ProfessorId, t.Name, t.Description, t.CriteriaJson, t.CreatedAt)).ToList();

        return new BackupDto("2.0", DateTime.UtcNow,
            professors.Select(p => new ProfessorBackupDto(
                p.Id, p.Email, p.Nom, p.Cognoms, p.IsAdmin, p.PasswordHash, p.CreatedAt)).ToList(),
            classDtos, activityDtos,
            templateDtos.Count > 0 ? templateDtos : null,
            null); // AuditLogs no s'inclouen al backup
    }

    public async Task<byte[]> ExportZipAsync() => ToZip(await ExportAsync());

    // ZIP amb backup.json + fotos d'alumnes i professors
    private byte[] ToZip(BackupDto backup)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // ── backup.json ───────────────────────────────────────────────────
            var jsonEntry = zip.CreateEntry("backup.json", CompressionLevel.Optimal);
            using (var w = new StreamWriter(jsonEntry.Open(), System.Text.Encoding.UTF8))
                w.Write(JsonSerializer.Serialize(backup, _json));

            // ── Fotos alumnes ─────────────────────────────────────────────────
            AddFotosToZip(zip, Path.Combine(PhotosDir, "alumnes"),    "fotos/alumnes");

            // ── Fotos professors ──────────────────────────────────────────────
            AddFotosToZip(zip, Path.Combine(PhotosDir, "professors"), "fotos/professors");
        }
        return ms.ToArray();
    }

    private static void AddFotosToZip(ZipArchive zip, string sourceDir, string zipPrefix)
    {
        if (!Directory.Exists(sourceDir)) return;
        foreach (var file in Directory.GetFiles(sourceDir, "*.jpg"))
        {
            var entry = zip.CreateEntry($"{zipPrefix}/{Path.GetFileName(file)}", CompressionLevel.NoCompression);
            using var dest = entry.Open();
            using var src  = File.OpenRead(file);
            src.CopyTo(dest);
        }
    }

    // Llegeix backup.json + fotos d'un ZIP
    private static (BackupDto? Dto, Dictionary<string, byte[]> Photos) FromZipFull(byte[] data)
    {
        var photos = new Dictionary<string, byte[]>();
        BackupDto? dto = null;

        using var ms  = new MemoryStream(data);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

        foreach (var entry in zip.Entries)
        {
            if (entry.FullName == "backup.json")
            {
                using var r = new StreamReader(entry.Open(), System.Text.Encoding.UTF8);
                dto = JsonSerializer.Deserialize<BackupDto>(r.ReadToEnd(), _json);
            }
            else if (entry.FullName.StartsWith("fotos/") && entry.Name.EndsWith(".jpg"))
            {
                using var buf = new MemoryStream();
                using var es  = entry.Open();
                es.CopyTo(buf);
                // Clau: "alumnes/42" o "professors/5"
                var relative = entry.FullName["fotos/".Length..];
                var key      = relative[..relative.LastIndexOf('.')];
                photos[key]  = buf.ToArray();
            }
        }

        return (dto, photos);
    }

    // ── Import (substitueix totes les dades) ──────────────────────────────────
    public Task<ImportResult> ImportAsync(BackupDto backup) => ImportCoreAsync(backup, null);

    private async Task<ImportResult> ImportCoreAsync(BackupDto bk, Dictionary<string, byte[]>? photos)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        Dictionary<int, int> profMap    = [];
        Dictionary<int, int> studentMap = [];
        try
        {
            // Esborrar en ordre de dependències FK
            await db.EvaluationScores.ExecuteDeleteAsync();
            await db.Evaluations.ExecuteDeleteAsync();
            await db.ProfessorNotes.ExecuteDeleteAsync();
            await db.ActivityCriteria.ExecuteDeleteAsync();
            await db.GroupMembers.ExecuteDeleteAsync();
            await db.Groups.ExecuteDeleteAsync();
            await db.Activities.ExecuteDeleteAsync();
            await db.ModuleExclusions.ExecuteDeleteAsync();
            await db.Modules.ExecuteDeleteAsync();
            await db.Students.ExecuteDeleteAsync();
            await db.Classes.ExecuteDeleteAsync();
            await db.ActivityTemplates.ExecuteDeleteAsync();
            await db.Professors.ExecuteDeleteAsync();

            // ── Professors ────────────────────────────────────────────────────
            var profEnts = bk.Professors.Select(p => new Professor
            {
                Email = p.Email, Nom = p.Nom, Cognoms = p.Cognoms,
                IsAdmin = p.IsAdmin, PasswordHash = p.PasswordHash, CreatedAt = p.CreatedAt
            }).ToList();
            db.Professors.AddRange(profEnts);
            await db.SaveChangesAsync();
            profMap = bk.Professors.Zip(profEnts)
                .ToDictionary(x => x.First.Id, x => x.Second.Id);

            // ── Activity Templates ────────────────────────────────────────────
            if (bk.Templates is { Count: > 0 })
            {
                foreach (var t in bk.Templates)
                    if (profMap.TryGetValue(t.ProfessorId, out var newProfId))
                        db.ActivityTemplates.Add(new ActivityTemplate
                        {
                            ProfessorId  = newProfId,
                            Name         = t.Name,
                            Description  = t.Description,
                            CriteriaJson = t.CriteriaJson,
                            CreatedAt    = t.CreatedAt
                        });
                await db.SaveChangesAsync();
            }

            // ── Classes ───────────────────────────────────────────────────────
            var classEnts = bk.Classes.Select(c => new Class
                { Name = c.Name, AcademicYear = c.AcademicYear, CreatedAt = c.CreatedAt }).ToList();
            db.Classes.AddRange(classEnts);
            await db.SaveChangesAsync();

            // ── Students ──────────────────────────────────────────────────────
            var studentPairs = new List<(StudentBackupDto Dto, Student Ent)>();
            for (int ci = 0; ci < bk.Classes.Count; ci++)
            {
                foreach (var s in bk.Classes[ci].Students)
                {
                    var ent = new Student
                    {
                        ClassId = classEnts[ci].Id, Nom = s.Nom, Cognoms = s.Cognoms,
                        NumLlista = s.NumLlista, Email = s.Email,
                        PasswordHash = s.PasswordHash,
                        PlainPasswordEncrypted = s.PlainPasswordEncrypted,
                        Dni = s.Dni,
                        CreatedAt = s.CreatedAt
                    };
                    studentPairs.Add((s, ent));
                    db.Students.Add(ent);
                }
            }
            await db.SaveChangesAsync();
            studentMap = studentPairs.ToDictionary(x => x.Dto.Id, x => x.Ent.Id);

            // ── Modules ───────────────────────────────────────────────────────
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

            // ── Exclusions ────────────────────────────────────────────────────
            foreach (var (mDto, mEnt) in modulePairs)
                foreach (var exclId in mDto.ExcludedStudentIds)
                    if (studentMap.TryGetValue(exclId, out var newStId))
                        db.ModuleExclusions.Add(new ModuleExclusion { ModuleId = mEnt.Id, StudentId = newStId });
            await db.SaveChangesAsync();

            // ── Activities ────────────────────────────────────────────────────
            var actPairs = new List<(ActivityBackupDto Dto, Activity Ent)>();
            foreach (var a in bk.Activities)
            {
                if (!moduleMap.TryGetValue(a.ModuleId, out var newModId)) continue;
                var ent = new Activity
                {
                    ModuleId = newModId, Name = a.Name, Description = a.Description,
                    IsOpen = a.IsOpen, CreatedAt = a.CreatedAt,
                    ShowResultsToStudents = a.ShowResultsToStudents,
                    OpenAt = a.OpenAt, CloseAt = a.CloseAt
                };
                actPairs.Add((a, ent));
                db.Activities.Add(ent);
            }
            await db.SaveChangesAsync();

            // ── Activity Criteria ─────────────────────────────────────────────
            foreach (var (aDto, actEnt) in actPairs)
                if (aDto.Criteria is { Count: > 0 })
                    foreach (var c in aDto.Criteria)
                        db.ActivityCriteria.Add(new ActivityCriterion
                        {
                            ActivityId = actEnt.Id,
                            Key        = c.Key,
                            Label      = c.Label,
                            OrderIndex = c.OrderIndex,
                            Weight     = c.Weight > 0 ? c.Weight : 1
                        });
            await db.SaveChangesAsync();

            // ── Groups ────────────────────────────────────────────────────────
            var groupPairs = new List<(GroupBackupDto Dto, Group Ent)>();
            foreach (var (aDto, actEnt) in actPairs)
                foreach (var g in aDto.Groups)
                {
                    var ent = new Group { ActivityId = actEnt.Id, Name = g.Name };
                    groupPairs.Add((g, ent));
                    db.Groups.Add(ent);
                }
            await db.SaveChangesAsync();

            // ── Group Members ─────────────────────────────────────────────────
            foreach (var (gDto, gEnt) in groupPairs)
                foreach (var sid in gDto.StudentIds)
                    if (studentMap.TryGetValue(sid, out var newSid))
                        db.GroupMembers.Add(new GroupMember { GroupId = gEnt.Id, StudentId = newSid });
            await db.SaveChangesAsync();

            // ── Evaluations ───────────────────────────────────────────────────
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
            await db.SaveChangesAsync();

            // ── Evaluation Scores ─────────────────────────────────────────────
            foreach (var (evEnt, scores) in evalPairs)
                foreach (var (key, score) in scores)
                    db.EvaluationScores.Add(new EvaluationScore
                        { EvaluationId = evEnt.Id, CriteriaKey = key, Score = score });
            await db.SaveChangesAsync();

            // ── Professor Notes ───────────────────────────────────────────────
            foreach (var (aDto, actEnt) in actPairs)
                if (aDto.Notes is { Count: > 0 })
                    foreach (var n in aDto.Notes)
                        if (studentMap.TryGetValue(n.StudentId, out var newStId))
                            db.ProfessorNotes.Add(new ProfessorNote
                            {
                                ActivityId = actEnt.Id,
                                StudentId  = newStId,
                                Note       = n.Note,
                                UpdatedAt  = n.UpdatedAt
                            });
            await db.SaveChangesAsync();

            await tx.CommitAsync();

            var result = new ImportResult(true, null,
                bk.Professors.Count, bk.Classes.Count,
                bk.Classes.Sum(c => c.Students.Count),
                bk.Classes.Sum(c => c.Modules.Count),
                bk.Activities.Count, evalPairs.Count);

            // ── Fotos (fora de la transacció BD) ──────────────────────────────
            if (photos is { Count: > 0 })
                await RemapPhotosAsync(photos, studentMap, profMap);

            return result;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            logger.LogError(ex, "Error important backup");
            return new ImportResult(false, "Error intern en importar el backup. Consulta els logs del servidor.", 0, 0, 0, 0, 0, 0);
        }
    }

    private async Task RemapPhotosAsync(
        Dictionary<string, byte[]> photos,
        Dictionary<int, int>       studentMap,
        Dictionary<int, int>       profMap)
    {
        try
        {
            var alumnesDir    = Path.Combine(PhotosDir, "alumnes");
            var professorsDir = Path.Combine(PhotosDir, "professors");
            Directory.CreateDirectory(alumnesDir);
            Directory.CreateDirectory(professorsDir);

            foreach (var (key, data) in photos)
            {
                if (key.StartsWith("alumnes/") &&
                    int.TryParse(key["alumnes/".Length..], out var oldSid) &&
                    studentMap.TryGetValue(oldSid, out var newSid))
                {
                    await File.WriteAllBytesAsync(Path.Combine(alumnesDir, $"{newSid}.jpg"), data);
                }
                else if (key.StartsWith("professors/") &&
                    int.TryParse(key["professors/".Length..], out var oldPid) &&
                    profMap.TryGetValue(oldPid, out var newPid))
                {
                    await File.WriteAllBytesAsync(Path.Combine(professorsDir, $"{newPid}.jpg"), data);
                }
            }

            logger.LogInformation("Fotos remapades: {Count} fitxer(s) restaurat(s)", photos.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error remapant fotos del backup");
        }
    }

    // ── Fitxers de còpia ──────────────────────────────────────────────────────
    public Task<List<BackupFileInfoDto>> ListFilesAsync()
    {
        EnsureDir();
        var files = new DirectoryInfo(BackupsDir)
            .GetFiles("backup_*.zip")
            .Concat(new DirectoryInfo(BackupsDir).GetFiles("backup_*.json")) // compatibilitat enrere
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new BackupFileInfoDto(f.Name, f.LastWriteTimeUtc, f.Length))
            .ToList();
        return Task.FromResult(files);
    }

    public async Task<BackupFileInfoDto> CreateFileAsync()
    {
        EnsureDir();
        var backup = await ExportAsync();
        var name   = $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
        var path   = Path.Combine(BackupsDir, name);
        await File.WriteAllBytesAsync(path, ToZip(backup));
        var info   = new FileInfo(path);
        return new BackupFileInfoDto(name, info.LastWriteTimeUtc, info.Length);
    }

    public async Task<BackupFileInfoDto> CreateAutoBackupAsync(string type)
    {
        EnsureDir();
        var backup = await ExportAsync();
        var name   = $"backup_{type}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
        var path   = Path.Combine(BackupsDir, name);
        await File.WriteAllBytesAsync(path, ToZip(backup));
        var info   = new FileInfo(path);
        logger.LogInformation("Backup automàtic {Type} creat: {Name}", type, name);
        return new BackupFileInfoDto(name, info.LastWriteTimeUtc, info.Length);
    }

    public Task ApplyRetentionAsync(string type, int maxCount)
    {
        EnsureDir();
        var files = new DirectoryInfo(BackupsDir)
            .GetFiles($"backup_{type}_*.zip")
            .Concat(new DirectoryInfo(BackupsDir).GetFiles($"backup_{type}_*.json"))
            .OrderByDescending(f => f.Name)
            .ToList();
        foreach (var f in files.Skip(maxCount))
        {
            logger.LogInformation("Retenció backup {Type}: esborrant {File}", type, f.Name);
            f.Delete();
        }
        return Task.CompletedTask;
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

        if (name.EndsWith(".zip"))
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var (backup, photos) = FromZipFull(bytes);
            if (backup is null)
                return new ImportResult(false, "Format invàlid", 0, 0, 0, 0, 0, 0);
            return await ImportCoreAsync(backup, photos);
        }
        else
        {
            // Compatibilitat amb còpies .json antigues (sense fotos)
            var json   = await File.ReadAllTextAsync(path);
            var backup = JsonSerializer.Deserialize<BackupDto>(json, _json);
            if (backup is null)
                return new ImportResult(false, "Format invàlid", 0, 0, 0, 0, 0, 0);
            return await ImportCoreAsync(backup, null);
        }
    }

    // Evita path traversal
    private string? SafePath(string name)
    {
        if (name.Contains('/') || name.Contains('\\') || name.Contains("..")) return null;
        if (!name.StartsWith("backup_")) return null;
        if (!name.EndsWith(".zip") && !name.EndsWith(".json")) return null;
        return Path.Combine(BackupsDir, name);
    }
}

// ── Extensió auxiliar ─────────────────────────────────────────────────────────
file static class ListExtensions
{
    public static List<T>? NullIfEmpty<T>(this List<T> list) =>
        list.Count > 0 ? list : null;
}
