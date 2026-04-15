using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AutoCo.Api.Services;

public interface IProfessorService
{
    Task<List<ProfessorDto>> GetAllAsync();
    Task<ProfessorDto?>      GetByIdAsync(int id);
    Task<ProfessorDto>       CreateAsync(CreateProfessorRequest req);
    Task<ProfessorDto?>      UpdateAsync(int id, UpdateProfessorRequest req);
    Task<bool>               DeleteAsync(int id);
    Task<SendCredentialsResult> SendCredentialsAsync(int professorId);
    Task<SendAllResult>         SendAllCredentialsAsync();
}

public class ProfessorService(AppDbContext db, IEmailService email) : IProfessorService
{
    public async Task<List<ProfessorDto>> GetAllAsync()
    {
        return await db.Professors
            .OrderBy(p => p.Cognoms).ThenBy(p => p.Nom)
            .Select(p => ToDto(p, p.Classes.Count))
            .ToListAsync();
    }

    public async Task<ProfessorDto?> GetByIdAsync(int id)
    {
        var p = await db.Professors.Include(p => p.Classes).FirstOrDefaultAsync(p => p.Id == id);
        return p is null ? null : ToDto(p, p.Classes.Count);
    }

    public async Task<ProfessorDto> CreateAsync(CreateProfessorRequest req)
    {
        var professor = new Professor
        {
            Username         = req.Username.Trim(),
            PasswordHash     = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Nom              = req.Nom.Trim(),
            Cognoms          = req.Cognoms.Trim(),
            CorreuElectronic = req.CorreuElectronic?.Trim(),
            IsAdmin          = req.IsAdmin
        };
        db.Professors.Add(professor);
        await db.SaveChangesAsync();
        return ToDto(professor, 0);
    }

    public async Task<ProfessorDto?> UpdateAsync(int id, UpdateProfessorRequest req)
    {
        var professor = await db.Professors.FindAsync(id);
        if (professor is null) return null;

        professor.Nom              = req.Nom.Trim();
        professor.Cognoms          = req.Cognoms.Trim();
        professor.CorreuElectronic = req.CorreuElectronic?.Trim();
        professor.IsAdmin          = req.IsAdmin;

        if (!string.IsNullOrWhiteSpace(req.NewPassword))
            professor.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);

        await db.SaveChangesAsync();

        var numClasses = await db.Classes.CountAsync(c => c.ProfessorId == id);
        return ToDto(professor, numClasses);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var professor = await db.Professors.FindAsync(id);
        if (professor is null) return false;

        if (professor.IsAdmin && await db.Professors.CountAsync(p => p.IsAdmin) <= 1)
            throw new InvalidOperationException("No es pot eliminar l'únic administrador del sistema.");

        db.Professors.Remove(professor);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<SendCredentialsResult> SendCredentialsAsync(int professorId)
    {
        var professor = await db.Professors.FindAsync(professorId);
        if (professor is null) return new SendCredentialsResult(false, "Professor no trobat.");

        if (string.IsNullOrWhiteSpace(professor.CorreuElectronic))
            return new SendCredentialsResult(false, "El professor no té correu electrònic.");

        if (!email.IsEnabled)
            return new SendCredentialsResult(false, "El servei de correu no està configurat.");

        // Genera nova contrasenya temporal
        var newPassword = GeneratePassword();
        professor.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await db.SaveChangesAsync();

        var sent = await email.SendProfessorCredentialsAsync(
            professor.CorreuElectronic, professor.NomComplet, professor.Username, newPassword);

        return new SendCredentialsResult(sent, sent ? null : "Error en l'enviament.");
    }

    public async Task<SendAllResult> SendAllCredentialsAsync()
    {
        var professors = await db.Professors
            .Where(p => !string.IsNullOrEmpty(p.CorreuElectronic))
            .ToListAsync();

        int sent = 0, skipped = 0;
        var details = new List<string>();

        foreach (var p in professors)
        {
            var newPassword = GeneratePassword();
            p.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await db.SaveChangesAsync();

            var ok = await email.SendProfessorCredentialsAsync(
                p.CorreuElectronic!, p.NomComplet, p.Username, newPassword);

            if (ok) sent++;
            else { skipped++; details.Add($"{p.NomComplet}: error d'enviament."); }
        }

        // Professors sense correu
        var sensCorreu = await db.Professors.CountAsync(p => string.IsNullOrEmpty(p.CorreuElectronic));
        skipped += sensCorreu;

        return new SendAllResult(sent, skipped, details);
    }

    private static ProfessorDto ToDto(Professor p, int numClasses) => new(
        p.Id, p.Username, p.Nom, p.Cognoms, p.NomComplet,
        p.CorreuElectronic, p.IsAdmin, p.CreatedAt, numClasses);

    private static string GeneratePassword()
    {
        const string chars = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 10)
            .Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
    }
}
