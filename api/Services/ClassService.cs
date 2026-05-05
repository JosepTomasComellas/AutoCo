using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AutoCo.Api.Services;

public interface IClassService
{
    Task<List<ClassDto>>  GetAllAsync();
    Task<ClassDto?>       GetByIdAsync(int id);
    Task<ClassDto>        CreateAsync(CreateClassRequest req);
    Task<ClassDto?>       UpdateAsync(int id, UpdateClassRequest req);
    Task<bool>            DeleteAsync(int id);

    Task<List<StudentDto>>  GetStudentsAsync(int classId);
    Task<StudentDto>        AddStudentAsync(int classId, CreateStudentRequest req);
    Task<StudentDto?>       UpdateStudentAsync(int classId, int studentId, UpdateStudentRequest req);
    Task<bool>              DeleteStudentAsync(int classId, int studentId);
    Task<StudentDto?>       MoveStudentAsync(int classId, int studentId, int targetClassId);
    Task<BulkCreateResult>  BulkAddStudentsAsync(int classId, BulkCreateStudentsRequest req);
    Task<ResetPasswordResult?> ResetPasswordAsync(int classId, int studentId);
    Task<SendPasswordResult>   SendPasswordAsync(int classId, int studentId);
    Task<SendAllResult>        SendAllPasswordsAsync(int classId);
    Task<bool>                 ChangeStudentPasswordAsync(int studentId, string currentPassword, string newPassword);
    Task<string>               GetOrRefreshPlainPasswordAsync(int studentId);
}

public class ClassService(AppDbContext db, IEmailService email, IPasswordCryptoService pwdCrypto, IPhotoService photos) : IClassService
{

    // ── Classes ──────────────────────────────────────────────────────────────

    public async Task<List<ClassDto>> GetAllAsync() =>
        await db.Classes
            .Include(c => c.Students)
            .OrderBy(c => c.Name)
            .Select(c => ToClassDto(c))
            .ToListAsync();

    public async Task<ClassDto?> GetByIdAsync(int id)
    {
        var c = await db.Classes
            .Include(c => c.Students)
            .FirstOrDefaultAsync(c => c.Id == id);
        return c is null ? null : ToClassDto(c);
    }

    public async Task<ClassDto> CreateAsync(CreateClassRequest req)
    {
        var classe = new Class { Name = req.Name.Trim(), AcademicYear = req.AcademicYear?.Trim() };
        db.Classes.Add(classe);
        await db.SaveChangesAsync();
        return ToClassDto(classe);
    }

    public async Task<ClassDto?> UpdateAsync(int id, UpdateClassRequest req)
    {
        var c = await db.Classes.Include(c => c.Students).FirstOrDefaultAsync(c => c.Id == id);
        if (c is null) return null;
        c.Name = req.Name.Trim();
        c.AcademicYear = req.AcademicYear?.Trim();
        await db.SaveChangesAsync();
        return ToClassDto(c);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var c = await db.Classes.FindAsync(id);
        if (c is null) return false;
        db.Classes.Remove(c);
        await db.SaveChangesAsync();
        return true;
    }

    // ── Alumnes ──────────────────────────────────────────────────────────────

    public async Task<List<StudentDto>> GetStudentsAsync(int classId)
    {
        var list = await db.Students.Where(s => s.ClassId == classId)
            .OrderBy(s => s.NumLlista).ToListAsync();
        return list.Select(ToStudentDto).ToList();
    }

    public async Task<StudentDto> AddStudentAsync(int classId, CreateStudentRequest req)
    {
        var password = PasswordHelper.Generate();
        var student = new Student
        {
            ClassId                = classId,
            NumLlista              = req.NumLlista,
            Nom                    = req.Nom.Trim(),
            Cognoms                = req.Cognoms.Trim(),
            Email                  = req.Email.Trim().ToLower(),
            Dni                    = string.IsNullOrWhiteSpace(req.Dni) ? null : req.Dni.Trim().ToUpperInvariant(),
            PasswordHash           = PasswordHelper.Hash(password),
            PlainPasswordEncrypted = pwdCrypto.Encrypt(password)
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return ToStudentDto(student);
    }

    public async Task<StudentDto?> UpdateStudentAsync(int classId, int studentId, UpdateStudentRequest req)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId && s.ClassId == classId);
        if (student is null) return null;
        student.Nom      = req.Nom.Trim();
        student.Cognoms  = req.Cognoms.Trim();
        student.NumLlista = req.NumLlista;
        student.Email    = req.Email.Trim().ToLower();
        student.Dni      = string.IsNullOrWhiteSpace(req.Dni) ? null : req.Dni.Trim().ToUpperInvariant();
        await db.SaveChangesAsync();
        return ToStudentDto(student);
    }

    public async Task<StudentDto?> MoveStudentAsync(int classId, int studentId, int targetClassId)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId && s.ClassId == classId);
        if (student is null) return null;

        var targetExists = await db.Classes.AnyAsync(c => c.Id == targetClassId);
        if (!targetExists) return null;

        // Elimina l'alumne de tots els grups (pertanyen a la classe antiga)
        await db.GroupMembers.Where(gm => gm.StudentId == studentId).ExecuteDeleteAsync();
        // Elimina avaluacions pendents (FK NoAction)
        await db.Evaluations
            .Where(e => e.EvaluatorId == studentId || e.EvaluatedId == studentId)
            .ExecuteDeleteAsync();

        student.ClassId = targetClassId;
        await db.SaveChangesAsync();
        return ToStudentDto(student);
    }

    public async Task<bool> DeleteStudentAsync(int classId, int studentId)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId && s.ClassId == classId);
        if (student is null) return false;

        // Les FK d'Evaluation cap a Student tenen OnDelete.NoAction → cal eliminar manualment.
        // EvaluationScores en cascade (OnDelete.Cascade) quan s'elimina Evaluation.
        await db.Evaluations
            .Where(e => e.EvaluatorId == studentId || e.EvaluatedId == studentId)
            .ExecuteDeleteAsync();

        await db.GroupMembers
            .Where(gm => gm.StudentId == studentId)
            .ExecuteDeleteAsync();

        await db.ModuleExclusions
            .Where(me => me.StudentId == studentId)
            .ExecuteDeleteAsync();

        db.Students.Remove(student);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<BulkCreateResult> BulkAddStudentsAsync(int classId, BulkCreateStudentsRequest req)
    {
        int created = 0, updated = 0, skipped = 0;
        var errors      = new List<string>();
        var batchEmails = new HashSet<string>(); // detecta duplicats dins del mateix CSV

        foreach (var s in req.Students)
        {
            if (string.IsNullOrWhiteSpace(s.Nom) || string.IsNullOrWhiteSpace(s.Cognoms) ||
                string.IsNullOrWhiteSpace(s.Email))
            {
                errors.Add($"Alumne #{s.NumLlista}: camps obligatoris buits.");
                skipped++; continue;
            }

            var emailNorm = s.Email.Trim().ToLower();

            if (!System.Text.RegularExpressions.Regex.IsMatch(emailNorm, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                errors.Add($"Alumne #{s.NumLlista}: format de correu invàlid ({emailNorm}).");
                skipped++; continue;
            }

            if (batchEmails.Contains(emailNorm))
            {
                errors.Add($"Alumne #{s.NumLlista}: correu duplicat al fitxer ({emailNorm}).");
                skipped++; continue;
            }
            batchEmails.Add(emailNorm);

            var existing = await db.Students.FirstOrDefaultAsync(x => x.Email == emailNorm);
            if (existing is not null)
            {
                // Si és d'una altra classe, no el toquem
                if (existing.ClassId != classId)
                {
                    errors.Add($"Alumne #{s.NumLlista}: correu pertany a una altra classe ({emailNorm}).");
                    skipped++; continue;
                }

                // Actualitza les dades (sense tocar la contrasenya)
                existing.NumLlista = s.NumLlista;
                existing.Nom       = s.Nom.Trim();
                existing.Cognoms   = s.Cognoms.Trim();
                existing.Dni       = string.IsNullOrWhiteSpace(s.Dni) ? existing.Dni : s.Dni.Trim().ToUpperInvariant();
                updated++;
                continue;
            }

            var password = PasswordHelper.Generate();
            db.Students.Add(new Student
            {
                ClassId                = classId,
                NumLlista              = s.NumLlista,
                Nom                    = s.Nom.Trim(),
                Cognoms                = s.Cognoms.Trim(),
                Dni                    = string.IsNullOrWhiteSpace(s.Dni) ? null : s.Dni.Trim().ToUpperInvariant(),
                Email                  = emailNorm,
                PasswordHash           = PasswordHelper.Hash(password),
                PlainPasswordEncrypted = pwdCrypto.Encrypt(password)
            });
            created++;
        }

        if (created > 0 || updated > 0)
        {
            try
            {
                await db.SaveChangesAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("IX_Students_Email") == true)
            {
                errors.Add("Error en guardar: algun correu ja existia a la base de dades.");
                return new BulkCreateResult(0, 0, skipped + created, errors);
            }
        }

        return new BulkCreateResult(created, updated, skipped, errors);
    }

    public async Task<ResetPasswordResult?> ResetPasswordAsync(int classId, int studentId)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId && s.ClassId == classId);
        if (student is null) return null;
        var newPassword = PasswordHelper.Generate();
        student.PasswordHash           = PasswordHelper.Hash(newPassword);
        student.PlainPasswordEncrypted = pwdCrypto.Encrypt(newPassword);
        await db.SaveChangesAsync();
        return new ResetPasswordResult(newPassword);
    }

    public async Task<SendPasswordResult> SendPasswordAsync(int classId, int studentId)
    {
        var student = await db.Students.Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == studentId && s.ClassId == classId);
        if (student is null) return new SendPasswordResult(false, "Alumne no trobat.");
        if (!email.IsEnabled) return new SendPasswordResult(false, "Correu no configurat.");

        var newPassword = PasswordHelper.Generate();
        student.PasswordHash           = PasswordHelper.Hash(newPassword);
        student.PlainPasswordEncrypted = pwdCrypto.Encrypt(newPassword);
        await db.SaveChangesAsync();

        var sent = await email.SendStudentPasswordAsync(student.Email, student.NomComplet,
            student.Class.Name, newPassword);
        return new SendPasswordResult(sent, sent ? null : "Error en l'enviament.");
    }

    public async Task<SendAllResult> SendAllPasswordsAsync(int classId)
    {
        var students = await db.Students.Include(s => s.Class)
            .Where(s => s.ClassId == classId).ToListAsync();
        int sent = 0, skipped = 0;
        var details = new List<string>();

        // Genera i desa tots els nous passwords d'un sol cop (evita N+1 SaveChanges)
        var passwords = new Dictionary<int, string>(students.Count);
        foreach (var s in students)
        {
            var newPassword = PasswordHelper.Generate();
            s.PasswordHash           = PasswordHelper.Hash(newPassword);
            s.PlainPasswordEncrypted = pwdCrypto.Encrypt(newPassword);
            passwords[s.Id] = newPassword;
        }
        await db.SaveChangesAsync();

        foreach (var s in students)
        {
            if (!email.IsEnabled) { skipped++; continue; }
            var ok = await email.SendStudentPasswordAsync(s.Email, s.NomComplet, s.Class.Name, passwords[s.Id]);
            if (ok) sent++; else { skipped++; details.Add($"{s.NomComplet}: error."); }
        }
        return new SendAllResult(sent, skipped, details);
    }

    public async Task<bool> ChangeStudentPasswordAsync(int studentId, string currentPassword, string newPassword)
    {
        var student = await db.Students.FindAsync(studentId);
        if (student is null || !PasswordHelper.Verify(currentPassword, student.PasswordHash))
            return false;
        student.PasswordHash           = PasswordHelper.Hash(newPassword);
        student.PlainPasswordEncrypted = pwdCrypto.Encrypt(newPassword);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<string> GetOrRefreshPlainPasswordAsync(int studentId)
    {
        var student = await db.Students.FindAsync(studentId);
        if (student is null) return "";

        if (student.PlainPasswordEncrypted is not null)
        {
            var decrypted = pwdCrypto.TryDecrypt(student.PlainPasswordEncrypted);
            if (decrypted is not null) return decrypted;
        }

        var newPassword = PasswordHelper.Generate();
        student.PasswordHash           = PasswordHelper.Hash(newPassword);
        student.PlainPasswordEncrypted = pwdCrypto.Encrypt(newPassword);
        await db.SaveChangesAsync();
        return newPassword;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ClassDto ToClassDto(Class c) => new(
        c.Id, c.Name, c.AcademicYear, c.CreatedAt, c.Students.Count);

    private StudentDto ToStudentDto(Student s) => new(
        s.Id, s.ClassId, s.Nom, s.Cognoms, s.NomComplet, s.NumLlista, s.Email, s.CreatedAt,
        s.Dni, photos.GetStudentFotoUrl(s.Id));

}
