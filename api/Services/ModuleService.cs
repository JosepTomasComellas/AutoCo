using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AutoCo.Api.Services;

public interface IModuleService
{
    Task<List<ModuleDto>> GetByClassAsync(int classId, int professorId, bool isAdmin);
    Task<ModuleDto?>      GetByIdAsync(int id, int professorId, bool isAdmin);
    Task<ModuleDto>       CreateAsync(int classId, int professorId, bool isAdmin, CreateModuleRequest req);
    Task<ModuleDto?>      UpdateAsync(int id, int professorId, bool isAdmin, UpdateModuleRequest req);
    Task<bool>            DeleteAsync(int id, int professorId, bool isAdmin);
}

public class ModuleService(AppDbContext db) : IModuleService
{
    public async Task<List<ModuleDto>> GetByClassAsync(int classId, int professorId, bool isAdmin)
    {
        return await db.Modules
            .Include(m => m.Class).ThenInclude(c => c.Professor)
            .Include(m => m.Activities)
            .Where(m => m.ClassId == classId && (isAdmin || m.Class.ProfessorId == professorId))
            .OrderBy(m => m.Code)
            .Select(m => ToDto(m))
            .ToListAsync();
    }

    public async Task<ModuleDto?> GetByIdAsync(int id, int professorId, bool isAdmin)
    {
        var m = await db.Modules
            .Include(m => m.Class).ThenInclude(c => c.Professor)
            .Include(m => m.Activities)
            .FirstOrDefaultAsync(m => m.Id == id && (isAdmin || m.Class.ProfessorId == professorId));
        return m is null ? null : ToDto(m);
    }

    public async Task<ModuleDto> CreateAsync(int classId, int professorId, bool isAdmin, CreateModuleRequest req)
    {
        var classe = await db.Classes.Include(c => c.Professor)
            .FirstOrDefaultAsync(c => c.Id == classId && (isAdmin || c.ProfessorId == professorId))
            ?? throw new UnauthorizedAccessException("Classe no trobada o sense permisos.");

        var modul = new Module
        {
            ClassId = classId,
            Code    = req.Code.Trim().ToUpper(),
            Name    = req.Name.Trim()
        };
        db.Modules.Add(modul);
        await db.SaveChangesAsync();
        modul.Class = classe;
        return ToDto(modul);
    }

    public async Task<ModuleDto?> UpdateAsync(int id, int professorId, bool isAdmin, UpdateModuleRequest req)
    {
        var m = await db.Modules
            .Include(m => m.Class).ThenInclude(c => c.Professor)
            .Include(m => m.Activities)
            .FirstOrDefaultAsync(m => m.Id == id && (isAdmin || m.Class.ProfessorId == professorId));
        if (m is null) return null;

        m.Code = req.Code.Trim().ToUpper();
        m.Name = req.Name.Trim();
        await db.SaveChangesAsync();
        return ToDto(m);
    }

    public async Task<bool> DeleteAsync(int id, int professorId, bool isAdmin)
    {
        var m = await db.Modules.Include(m => m.Class)
            .FirstOrDefaultAsync(m => m.Id == id && (isAdmin || m.Class.ProfessorId == professorId));
        if (m is null) return false;
        db.Modules.Remove(m);
        await db.SaveChangesAsync();
        return true;
    }

    private static ModuleDto ToDto(Module m) => new(
        m.Id, m.ClassId, m.Class.Name, m.Class.AcademicYear,
        m.Code, m.Name, m.Activities.Count);
}
