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
    // Accés via propietat del mòdul O via ProfessorClass de la classe.
    private IQueryable<Module> WithAccess(IQueryable<Module> q, int professorId) =>
        q.Where(m => m.ProfessorId == professorId ||
                     db.ProfessorClasses.Any(pc => pc.ProfessorId == professorId && pc.ClassId == m.ClassId));

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
        var q = db.Modules
            .Include(m => m.Class).Include(m => m.Professor)
            .Include(m => m.Activities).Include(m => m.Exclusions)
            .Where(m => m.Id == id);
        var m = await (isAdmin ? q : WithAccess(q, professorId)).FirstOrDefaultAsync();
        return m is null ? null : ToDto(m);
    }

    public async Task<ModuleDto> CreateAsync(int classId, int professorId, CreateModuleRequest req)
    {
        var classe = await db.Classes.FindAsync(classId)
            ?? throw new InvalidOperationException("Classe no trobada.");

        var professor = await db.Professors.FindAsync(professorId)
            ?? throw new InvalidOperationException("Professor no trobat.");

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
        modul.Professor = professor;
        modul.Activities = [];
        return ToDto(modul);
    }

    public async Task<ModuleDto?> UpdateAsync(int id, int professorId, bool isAdmin, UpdateModuleRequest req)
    {
        var q = db.Modules
            .Include(m => m.Class).Include(m => m.Professor).Include(m => m.Activities)
            .Where(m => m.Id == id);
        var m = await (isAdmin ? q : WithAccess(q, professorId)).FirstOrDefaultAsync();
        if (m is null) return null;
        m.Code = req.Code.Trim().ToUpper();
        m.Name = req.Name.Trim();
        await db.SaveChangesAsync();
        return ToDto(m);
    }

    public async Task<bool> DeleteAsync(int id, int professorId, bool isAdmin)
    {
        var q = db.Modules.Include(m => m.Class).Where(m => m.Id == id);
        var m = await (isAdmin ? q : WithAccess(q, professorId)).FirstOrDefaultAsync();
        if (m is null) return false;
        db.Modules.Remove(m);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ModuleExclusionDto>> GetExclusionsAsync(int moduleId, int professorId, bool isAdmin)
    {
        var q = db.Modules.Where(m => m.Id == moduleId);
        var hasAccess = await (isAdmin ? q : WithAccess(q, professorId)).AnyAsync();
        if (!hasAccess) return [];

        return await db.ModuleExclusions
            .Include(e => e.Student)
            .Where(e => e.ModuleId == moduleId)
            .Select(e => new ModuleExclusionDto(e.StudentId, e.Student.NomComplet, e.Student.Email))
            .ToListAsync();
    }

    public async Task<bool> AddExclusionAsync(int moduleId, int studentId, int professorId, bool isAdmin)
    {
        var q = db.Modules.Where(m => m.Id == moduleId);
        var modul = await (isAdmin ? q : WithAccess(q, professorId)).FirstOrDefaultAsync();
        if (modul is null) return false;

        var exists = await db.ModuleExclusions.AnyAsync(e => e.ModuleId == moduleId && e.StudentId == studentId);
        if (exists) return true;

        db.ModuleExclusions.Add(new ModuleExclusion { ModuleId = moduleId, StudentId = studentId });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveExclusionAsync(int moduleId, int studentId, int professorId, bool isAdmin)
    {
        var q = db.ModuleExclusions
            .Include(e => e.Module)
            .Where(e => e.ModuleId == moduleId && e.StudentId == studentId);
        var exclusion = await (isAdmin ? q : q.Where(e =>
            e.Module.ProfessorId == professorId ||
            db.ProfessorClasses.Any(pc => pc.ProfessorId == professorId && pc.ClassId == e.Module.ClassId))
        ).FirstOrDefaultAsync();
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
