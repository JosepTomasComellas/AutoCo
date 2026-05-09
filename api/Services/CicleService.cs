using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AutoCo.Api.Services;

public interface ICicleService
{
    Task<List<CicleDto>> GetAllAsync();
    Task<CicleDto?>      GetByIdAsync(int id);
    Task<CicleDto>       CreateAsync(CreateCicleRequest req);
    Task<CicleDto?>      UpdateAsync(int id, UpdateCicleRequest req);
    Task<bool>           DeleteAsync(int id);
}

public class CicleService(AppDbContext db) : ICicleService
{
    public async Task<List<CicleDto>> GetAllAsync() =>
        await db.Cicles
            .OrderBy(c => c.Name)
            .Select(c => new CicleDto(c.Id, c.Name, c.CreatedAt, c.Classes.Count))
            .ToListAsync();

    public async Task<CicleDto?> GetByIdAsync(int id)
    {
        var cicle = await db.Cicles
            .Include(c => c.Classes)
            .FirstOrDefaultAsync(c => c.Id == id);
        return cicle is null ? null : ToDto(cicle);
    }

    public async Task<CicleDto> CreateAsync(CreateCicleRequest req)
    {
        var cicle = new Cicle { Name = req.Name.Trim() };
        db.Cicles.Add(cicle);
        await db.SaveChangesAsync();
        return new CicleDto(cicle.Id, cicle.Name, cicle.CreatedAt, 0);
    }

    public async Task<CicleDto?> UpdateAsync(int id, UpdateCicleRequest req)
    {
        var cicle = await db.Cicles
            .Include(c => c.Classes)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cicle is null) return null;
        cicle.Name = req.Name.Trim();
        await db.SaveChangesAsync();
        return ToDto(cicle);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var cicle = await db.Cicles
            .Include(c => c.Classes)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cicle is null) return false;
        if (cicle.Classes.Count > 0)
            throw new InvalidOperationException(
                $"No es pot eliminar el cicle '{cicle.Name}': té {cicle.Classes.Count} classe(s) assignada(s). Reassigna-les primer.");
        db.Cicles.Remove(cicle);
        await db.SaveChangesAsync();
        return true;
    }

    private static CicleDto ToDto(Cicle c) =>
        new(c.Id, c.Name, c.CreatedAt, c.Classes.Count);
}
