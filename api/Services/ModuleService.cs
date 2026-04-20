using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AutoCo.Api.Services;

public interface IModuleService
{
    Task<List<ModuleDto>> GetByClassAsync(int classId);
    Task<ModuleDto?>      GetByIdAsync(int id, int professorId, bool isAdmin);
    Task<ModuleDto>       CreateAsync(int classId, int professorId, CreateModuleRequest req);
    Task<ModuleDto?>      UpdateAsync(int id, int professorId, bool isAdmin, UpdateModuleRequest req);
    Task<bool>            DeleteAsync(int id, int professorId, bool isAdmin);

    Task<List<ModuleExclusionDto>> GetExclusionsAsync(int moduleId, int professorId, bool isAdmin);
    Task<bool> AddExclusionAsync(int moduleId, int studentId, int professorId, bool isAdmin);
    Task<bool> RemoveExclusionAsync(int moduleId, int studentId, int professorId, bool isAdmin);
}

public class ModuleService(AppDbContext db) : IModuleService
{
    public async Task<List<ModuleDto>> GetByClassAsync(int classId) =>
        await db.Modules
            .Include(m => m.Class)
            .Include(m => m.Professor)
            .Include(m => m.Activities)
            .Include(m => m.Exclusions)
            .Where(m => m.ClassId == classId)
            .OrderBy(m => m.Code)
            .Select(m => ToDto(m))
            .ToListAsync();

    public async Task<ModuleDto?> GetByIdAsync(int id, int professorId, bool isAdmin)
    {
        var m = await db.Modules
            .Include(m => m.Class).Include(m => m.Professor)
            .Include(m => m.Activities).Include(m => m.Exclusions)
            .FirstOrDefaultAsync(m => m.Id == id && (isAdmin || m.ProfessorId == professorId));
        return m is null ? null : ToDto(m);
    }

    public async Task<ModuleDto> CreateAsync(int classId, int professorId, CreateModuleRequest req)
    {
        var classe = await db.Classes.FindAsync(classId)
            ?? throw new InvalidOperationException("Classe no trobada.");

        var professor = await db.Professors.FindAsync(professorId)!;

        var modul = new Module
        {
            ClassId     = classId,
            ProfessorId = professorId,
            Code        = req.Code.Trim().ToUpper(),
            Name        = req.Name.Trim()
        };
        db.Modules.Add(modul);
        await db.SaveChangesAsync();
        modul.Class     = classe;
        modul.Professor = professor!;
        modul.Activities = [];
        return ToDto(modul);
    }

    public async Task<ModuleDto?> UpdateAsync(int id, int professorId, bool isAdmin, UpdateModuleRequest req)
    {
        var m = await db.Modules
            .Include(m => m.Class).Include(m => m.Professor).Include(m => m.Activities)
            .FirstOrDefaultAsync(m => m.Id == id && (isAdmin || m.ProfessorId == professorId));
        if (m is null) return null;
        m.Code = req.Code.Trim().ToUpper();
        m.Name = req.Name.Trim();
        await db.SaveChangesAsync();
        return ToDto(m);
    }

    public async Task<bool> DeleteAsync(int id, int professorId, bool isAdmin)
    {
        var m = await db.Modules.Include(m => m.Class)
            .FirstOrDefaultAsync(m => m.Id == id && (isAdmin || m.ProfessorId == professorId));
        if (m is null) return false;
        db.Modules.Remove(m);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ModuleExclusionDto>> GetExclusionsAsync(int moduleId, int professorId, bool isAdmin)
    {
        var hasAccess = await db.Modules.AnyAsync(m => m.Id == moduleId &&
            (isAdmin || m.ProfessorId == professorId));
        if (!hasAccess) return [];

        return await db.ModuleExclusions
            .Include(e => e.Student)
            .Where(e => e.ModuleId == moduleId)
            .Select(e => new ModuleExclusionDto(e.StudentId, e.Student.NomComplet, e.Student.Email))
            .ToListAsync();
    }

    public async Task<bool> AddExclusionAsync(int moduleId, int studentId, int professorId, bool isAdmin)
    {
        var modul = await db.Modules.FirstOrDefaultAsync(m => m.Id == moduleId &&
            (isAdmin || m.ProfessorId == professorId));
        if (modul is null) return false;

        var exists = await db.ModuleExclusions.AnyAsync(e => e.ModuleId == moduleId && e.StudentId == studentId);
        if (exists) return true;

        db.ModuleExclusions.Add(new ModuleExclusion { ModuleId = moduleId, StudentId = studentId });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveExclusionAsync(int moduleId, int studentId, int professorId, bool isAdmin)
    {
        var exclusion = await db.ModuleExclusions
            .Include(e => e.Module)
            .FirstOrDefaultAsync(e => e.ModuleId == moduleId && e.StudentId == studentId &&
                (isAdmin || e.Module.ProfessorId == professorId));
        if (exclusion is null) return false;
        db.ModuleExclusions.Remove(exclusion);
        await db.SaveChangesAsync();
        return true;
    }

    private static ModuleDto ToDto(Module m) => new(
        m.Id, m.ClassId, m.Class.Name, m.Class.AcademicYear,
        m.ProfessorId, m.Professor.NomComplet,
        m.Code, m.Name, m.Activities.Count, m.Exclusions.Count);
}
