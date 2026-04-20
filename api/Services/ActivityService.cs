using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace AutoCo.Api.Services;

public interface IActivityService
{
    Task<List<ActivityDto>> GetAllAsync(int? professorId);
    Task<ActivityDto?>      GetByIdAsync(int id, int? professorId);
    Task<ActivityDto>       CreateAsync(int professorId, bool isAdmin, CreateActivityRequest req);
    Task<ActivityDto?>      UpdateAsync(int id, int professorId, bool isAdmin, UpdateActivityRequest req);
    Task<bool>              DeleteAsync(int id, int professorId, bool isAdmin);
    Task<ActivityDto?>      ToggleOpenAsync(int id, int professorId, bool isAdmin);
    Task<ActivityDto>       DuplicateAsync(int activityId, int professorId, bool isAdmin, DuplicateActivityRequest req);
    Task<ActivityDto>       DuplicateCrossAsync(int activityId, int professorId, bool isAdmin, DuplicateCrossRequest req);
    Task<ParticipationDto>  GetParticipationAsync(int activityId, int professorId, bool isAdmin);
    Task<ReminderResult>    SendRemindersAsync(int activityId, int professorId, bool isAdmin, IEmailService email);
    Task<(byte[] Content, string FileName)?>  ExportGroupsAsync(int activityId, int professorId, bool isAdmin);
    Task<ImportGroupsResult> ImportGroupsAsync(int activityId, int professorId, bool isAdmin, string csvContent);

    Task<List<GroupDto>>  GetGroupsAsync(int activityId);
    Task<GroupDto>        CreateGroupAsync(int activityId, CreateGroupRequest req);
    Task<bool>            DeleteGroupAsync(int activityId, int groupId);
    Task<bool>            AddMemberAsync(int activityId, int groupId, int studentId);
    Task<bool>            RemoveMemberAsync(int activityId, int groupId, int studentId);

    // Per al dashboard de l'alumne
    Task<List<StudentActivityDto>> GetStudentActivitiesAsync(int studentId, int classId);
}

public class ActivityService(AppDbContext db, IDistributedCache cache) : IActivityService
{
    // ── Activitats ───────────────────────────────────────────────────────────

    public async Task<List<ActivityDto>> GetAllAsync(int? professorId)
    {
        var q = db.Activities
            .Include(a => a.Module).ThenInclude(m => m.Professor)
            .Include(a => a.Module).ThenInclude(m => m.Class)
            .Include(a => a.Groups).ThenInclude(g => g.Members)
            .AsQueryable();

        if (professorId.HasValue)
            q = q.Where(a => a.Module.ProfessorId == professorId.Value);

        return await q.OrderByDescending(a => a.CreatedAt)
            .Select(a => ToDto(a))
            .ToListAsync();
    }

    public async Task<ActivityDto?> GetByIdAsync(int id, int? professorId)
    {
        var a = await db.Activities
            .Include(a => a.Module).ThenInclude(m => m.Professor)
            .Include(a => a.Module).ThenInclude(m => m.Class)
            .Include(a => a.Groups).ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(a => a.Id == id &&
                (!professorId.HasValue || a.Module.ProfessorId == professorId.Value));

        return a is null ? null : ToDto(a);
    }

    public async Task<ActivityDto> CreateAsync(int professorId, bool isAdmin, CreateActivityRequest req)
    {
        var modul = await db.Modules
            .Include(m => m.Professor)
            .Include(m => m.Class)
            .FirstOrDefaultAsync(m => m.Id == req.ModuleId && (isAdmin || m.ProfessorId == professorId))
            ?? throw new UnauthorizedAccessException("El mòdul no pertany a aquest professor.");

        var activity = new Activity
        {
            ModuleId    = req.ModuleId,
            Name        = req.Name.Trim(),
            Description = req.Description?.Trim()
        };
        db.Activities.Add(activity);
        await db.SaveChangesAsync();

        return new ActivityDto(activity.Id,
            modul.Id, modul.Code, modul.Name,
            modul.ClassId, modul.Class.Name, modul.Class.AcademicYear,
            modul.Professor.NomComplet,
            activity.Name, activity.Description, activity.IsOpen, activity.CreatedAt, 0, 0);
    }

    public async Task<ActivityDto?> UpdateAsync(int id, int professorId, bool isAdmin, UpdateActivityRequest req)
    {
        var a = await db.Activities
            .Include(a => a.Module).ThenInclude(m => m.Professor)
            .Include(a => a.Module).ThenInclude(m => m.Class)
            .Include(a => a.Groups).ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(a => a.Id == id &&
                (isAdmin || a.Module.ProfessorId == professorId));
        if (a is null) return null;

        a.Name        = req.Name.Trim();
        a.Description = req.Description?.Trim();
        await db.SaveChangesAsync();
        return ToDto(a);
    }

    public async Task<bool> DeleteAsync(int id, int professorId, bool isAdmin)
    {
        var a = await db.Activities.Include(a => a.Module).ThenInclude(m => m.Class)
            .FirstOrDefaultAsync(a => a.Id == id &&
                (isAdmin || a.Module.ProfessorId == professorId));
        if (a is null) return false;
        db.Activities.Remove(a);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<ActivityDto?> ToggleOpenAsync(int id, int professorId, bool isAdmin)
    {
        var a = await db.Activities
            .Include(a => a.Module).ThenInclude(m => m.Professor)
            .Include(a => a.Module).ThenInclude(m => m.Class)
            .Include(a => a.Groups).ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(a => a.Id == id &&
                (isAdmin || a.Module.ProfessorId == professorId));
        if (a is null) return null;

        a.IsOpen = !a.IsOpen;
        await db.SaveChangesAsync();
        return ToDto(a);
    }

    // ── Duplicar / Export grups / Import grups ───────────────────────────────

    public async Task<ActivityDto> DuplicateAsync(int activityId, int professorId, bool isAdmin, DuplicateActivityRequest req)
    {
        var original = await db.Activities
            .Include(a => a.Module).ThenInclude(m => m.Professor)
            .Include(a => a.Module).ThenInclude(m => m.Class)
            .Include(a => a.Groups).ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(a => a.Id == activityId &&
                (isAdmin || a.Module.ProfessorId == professorId))
            ?? throw new UnauthorizedAccessException("Activitat no trobada o sense permisos.");

        var nova = new Activity
        {
            ModuleId    = original.ModuleId,
            Name        = req.Name.Trim(),
            Description = req.Description?.Trim(),
            IsOpen      = true
        };
        db.Activities.Add(nova);
        await db.SaveChangesAsync();

        foreach (var g in original.Groups)
        {
            var nouGrup = new Group { ActivityId = nova.Id, Name = g.Name };
            db.Groups.Add(nouGrup);
            await db.SaveChangesAsync();
            foreach (var m in g.Members)
                db.GroupMembers.Add(new GroupMember { GroupId = nouGrup.Id, StudentId = m.StudentId });
        }
        await db.SaveChangesAsync();

        var numStudents = original.Groups.SelectMany(g => g.Members).Select(m => m.StudentId).Distinct().Count();
        return new ActivityDto(nova.Id,
            original.Module.Id, original.Module.Code, original.Module.Name,
            original.Module.ClassId, original.Module.Class.Name, original.Module.Class.AcademicYear,
            original.Module.Professor.NomComplet,
            nova.Name, nova.Description, nova.IsOpen, nova.CreatedAt,
            original.Groups.Count, numStudents);
    }

    public async Task<ActivityDto> DuplicateCrossAsync(int activityId, int professorId, bool isAdmin, DuplicateCrossRequest req)
    {
        var original = await db.Activities
            .Include(a => a.Groups).ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(a => a.Id == activityId &&
                (isAdmin || a.Module.ProfessorId == professorId))
            ?? throw new UnauthorizedAccessException("Activitat no trobada o sense permisos.");

        var targetModule = await db.Modules
            .Include(m => m.Professor)
            .Include(m => m.Class)
            .FirstOrDefaultAsync(m => m.Id == req.TargetModuleId && (isAdmin || m.ProfessorId == professorId))
            ?? throw new UnauthorizedAccessException("Mòdul destí no trobat o sense permisos.");

        var nova = new Activity
        {
            ModuleId    = req.TargetModuleId,
            Name        = req.Name.Trim(),
            Description = req.Description?.Trim(),
            IsOpen      = true
        };
        db.Activities.Add(nova);
        await db.SaveChangesAsync();

        // Copia l'estructura de grups (noms) però NO els membres (alumnes d'altra classe)
        foreach (var g in original.Groups)
        {
            db.Groups.Add(new Group { ActivityId = nova.Id, Name = g.Name });
        }
        await db.SaveChangesAsync();

        return new ActivityDto(nova.Id,
            targetModule.Id, targetModule.Code, targetModule.Name,
            targetModule.ClassId, targetModule.Class.Name, targetModule.Class.AcademicYear,
            targetModule.Professor.NomComplet,
            nova.Name, nova.Description, nova.IsOpen, nova.CreatedAt,
            original.Groups.Count, 0);
    }

    public async Task<ParticipationDto> GetParticipationAsync(int activityId, int professorId, bool isAdmin)
    {
        var hasAccess = await db.Activities.AnyAsync(a => a.Id == activityId &&
            (isAdmin || a.Module.ProfessorId == professorId));
        if (!hasAccess) return new(activityId, 0, 0);

        var total = await db.GroupMembers
            .Where(gm => gm.Group.ActivityId == activityId)
            .Select(gm => gm.StudentId)
            .Distinct()
            .CountAsync();

        var submitted = await db.Evaluations
            .Where(e => e.ActivityId == activityId)
            .Select(e => e.EvaluatorId)
            .Distinct()
            .CountAsync();

        return new(activityId, submitted, total);
    }

    public async Task<ReminderResult> SendRemindersAsync(int activityId, int professorId, bool isAdmin, IEmailService email)
    {
        if (!email.IsEnabled) return new(0, 0, true);

        var activity = await db.Activities
            .Include(a => a.Module).ThenInclude(m => m.Class)
            .Include(a => a.Groups).ThenInclude(g => g.Members).ThenInclude(m => m.Student)
            .FirstOrDefaultAsync(a => a.Id == activityId && a.IsOpen &&
                (isAdmin || a.Module.ProfessorId == professorId));
        if (activity is null) return new(0, 0, false);

        var submittedIds = await db.Evaluations
            .Where(e => e.ActivityId == activityId)
            .Select(e => e.EvaluatorId)
            .Distinct()
            .ToHashSetAsync();

        var pending = activity.Groups
            .SelectMany(g => g.Members)
            .Select(m => m.Student)
            .DistinctBy(s => s.Id)
            .Where(s => !submittedIds.Contains(s.Id))
            .ToList();

        int sent = 0, skipped = 0;
        foreach (var s in pending)
        {
            var ok = await email.SendReminderAsync(
                s.Email, s.NomComplet, activity.Name, activity.Module.Class.Name);
            if (ok) sent++; else skipped++;
        }
        return new(sent, skipped, false);
    }

    public async Task<(byte[] Content, string FileName)?> ExportGroupsAsync(int activityId, int professorId, bool isAdmin)
    {
        var activity = await db.Activities
            .Include(a => a.Module).ThenInclude(m => m.Class)
            .Include(a => a.Groups).ThenInclude(g => g.Members).ThenInclude(m => m.Student)
            .FirstOrDefaultAsync(a => a.Id == activityId &&
                (isAdmin || a.Module.ProfessorId == professorId));
        if (activity is null) return null;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Grup;Correu");
        foreach (var g in activity.Groups.OrderBy(g => g.Name))
            foreach (var m in g.Members.OrderBy(m => m.Student.NumLlista))
                sb.AppendLine($"{g.Name};{m.Student.Email}");

        var bytes    = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"grups_{activity.Name.Replace(" ", "_")}_{activityId}.csv";
        return (bytes, fileName);
    }

    public async Task<ImportGroupsResult> ImportGroupsAsync(int activityId, int professorId, bool isAdmin, string csvContent)
    {
        var activity = await db.Activities
            .Include(a => a.Module).ThenInclude(m => m.Class).ThenInclude(c => c.Students)
            .Include(a => a.Groups).ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(a => a.Id == activityId &&
                (isAdmin || a.Module.ProfessorId == professorId))
            ?? throw new UnauthorizedAccessException("Activitat no trobada o sense permisos.");

        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int assigned = 0, skipped = 0;
        var errors   = new List<string>();
        var groupCache = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines.Skip(1)) // saltar capçalera
        {
            var parts = line.Trim().Split(';');
            if (parts.Length < 2) continue;
            var groupName = parts[0].Trim();
            var email     = parts[1].Trim();
            if (string.IsNullOrEmpty(groupName) || string.IsNullOrEmpty(email)) continue;

            var student = activity.Module.Class.Students.FirstOrDefault(s =>
                s.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

            if (student is null)
            {
                errors.Add($"Alumne no trobat: {email}");
                skipped++;
                continue;
            }

            var alreadyAssigned = activity.Groups.Any(g => g.Members.Any(m => m.StudentId == student.Id));
            if (alreadyAssigned)
            {
                errors.Add($"Alumne ja assignat: {email}");
                skipped++;
                continue;
            }

            if (!groupCache.TryGetValue(groupName, out var grup))
            {
                grup = activity.Groups.FirstOrDefault(g =>
                    g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
                if (grup is null)
                {
                    grup = new Group { ActivityId = activityId, Name = groupName };
                    db.Groups.Add(grup);
                    await db.SaveChangesAsync();
                    activity.Groups.Add(grup);
                }
                groupCache[groupName] = grup;
            }

            db.GroupMembers.Add(new GroupMember { GroupId = grup.Id, StudentId = student.Id });
            assigned++;
        }

        if (assigned > 0) await db.SaveChangesAsync();
        return new ImportGroupsResult(assigned, skipped, errors);
    }

    // ── Grups ────────────────────────────────────────────────────────────────

    public async Task<List<GroupDto>> GetGroupsAsync(int activityId)
    {
        var groups = await db.Groups
            .Include(g => g.Members).ThenInclude(m => m.Student)
            .Where(g => g.ActivityId == activityId)
            .OrderBy(g => g.Name)
            .ToListAsync();

        return groups.Select(g => new GroupDto(g.Id, g.ActivityId, g.Name,
            g.Members.OrderBy(m => m.Student.NumLlista)
                     .Select(m => ToStudentDto(m.Student)).ToList())).ToList();
    }

    public async Task<GroupDto> CreateGroupAsync(int activityId, CreateGroupRequest req)
    {
        var group = new Group { ActivityId = activityId, Name = req.Name.Trim() };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return new GroupDto(group.Id, group.ActivityId, group.Name, []);
    }

    public async Task<bool> DeleteGroupAsync(int activityId, int groupId)
    {
        var group = await db.Groups
            .FirstOrDefaultAsync(g => g.Id == groupId && g.ActivityId == activityId);
        if (group is null) return false;
        db.Groups.Remove(group);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddMemberAsync(int activityId, int groupId, int studentId)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId && g.ActivityId == activityId);
        if (group is null) return false;

        var alreadyAssigned = await db.GroupMembers
            .AnyAsync(gm => gm.Group.ActivityId == activityId && gm.StudentId == studentId);
        if (alreadyAssigned) return false;

        db.GroupMembers.Add(new GroupMember { GroupId = groupId, StudentId = studentId });
        await db.SaveChangesAsync();
        await InvalidateResultsCacheAsync(activityId);
        return true;
    }

    public async Task<bool> RemoveMemberAsync(int activityId, int groupId, int studentId)
    {
        var member = await db.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.StudentId == studentId
                && gm.Group.ActivityId == activityId);
        if (member is null) return false;
        db.GroupMembers.Remove(member);
        await db.SaveChangesAsync();
        await InvalidateResultsCacheAsync(activityId);
        return true;
    }

    private Task InvalidateResultsCacheAsync(int activityId) =>
        Task.WhenAll(
            cache.RemoveAsync($"autoco:results:{activityId}"),
            cache.RemoveAsync($"autoco:chart:{activityId}")
        );

    // ── Dashboard de l'alumne ────────────────────────────────────────────────

    public async Task<List<StudentActivityDto>> GetStudentActivitiesAsync(int studentId, int classId)
    {
        var excludedModuleIds = await db.ModuleExclusions
            .Where(e => e.StudentId == studentId)
            .Select(e => e.ModuleId)
            .ToHashSetAsync();

        var activities = await db.Activities
            .Where(a => a.Module.ClassId == classId)
            .Where(a => !excludedModuleIds.Contains(a.ModuleId))
            .Where(a => a.Groups.Any(g => g.Members.Any(m => m.StudentId == studentId)))
            .Include(a => a.Module).ThenInclude(m => m.Professor)
            .Include(a => a.Module).ThenInclude(m => m.Class)
            .Include(a => a.Groups).ThenInclude(g => g.Members)
            .OrderByDescending(a => a.IsOpen)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync();

        // Carregar totes les avaluacions de l'alumne d'un sol cop (evita N+1)
        var activityIds  = activities.Select(a => a.Id).ToList();
        var evalCounts   = await db.Evaluations
            .Where(e => activityIds.Contains(e.ActivityId) && e.EvaluatorId == studentId)
            .GroupBy(e => e.ActivityId)
            .Select(g => new { ActivityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ActivityId, x => x.Count);

        var result = new List<StudentActivityDto>();
        foreach (var act in activities)
        {
            var myGroup = act.Groups.FirstOrDefault(g => g.Members.Any(m => m.StudentId == studentId));
            if (myGroup is null) continue;
            evalCounts.TryGetValue(act.Id, out var alreadyEval);
            result.Add(new StudentActivityDto(
                act.Id, act.Name, act.Description, act.IsOpen,
                myGroup.Name, myGroup.Id,
                myGroup.Members.Count, alreadyEval));
        }
        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ActivityDto ToDto(Activity a)
    {
        var numGroups   = a.Groups.Count;
        var numStudents = a.Groups.SelectMany(g => g.Members).Select(m => m.StudentId).Distinct().Count();
        return new ActivityDto(
            a.Id,
            a.ModuleId, a.Module.Code, a.Module.Name,
            a.Module.ClassId, a.Module.Class.Name, a.Module.Class.AcademicYear,
            a.Module.Professor.NomComplet,
            a.Name, a.Description, a.IsOpen, a.CreatedAt, numGroups, numStudents);
    }

    private static StudentDto ToStudentDto(Student s) => new(
        s.Id, s.ClassId, s.Nom, s.Cognoms, s.NomComplet, s.NumLlista, s.Email, s.CreatedAt);
}
