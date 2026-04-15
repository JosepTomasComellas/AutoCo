using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AutoCo.Api.Services;

public interface IClassService
{
    Task<List<ClassDto>>    GetAllAsync(int? professorId);
    Task<ClassDto?>         GetByIdAsync(int id, int? professorId);
    Task<ClassDto>          CreateAsync(int professorId, CreateClassRequest req);
    Task<ClassDto?>         UpdateAsync(int id, int professorId, bool isAdmin, UpdateClassRequest req);
    Task<bool>              DeleteAsync(int id, int professorId, bool isAdmin);

    Task<List<StudentWithPinDto>> GetStudentsAsync(int classId, int professorId, bool isAdmin);
    Task<StudentWithPinDto>       AddStudentAsync(int classId, CreateStudentRequest req);
    Task<StudentWithPinDto?>      UpdateStudentAsync(int classId, int studentId, UpdateStudentRequest req);
    Task<bool>                    DeleteStudentAsync(int classId, int studentId);
    Task<BulkCreateResult>        BulkAddStudentsAsync(int classId, BulkCreateStudentsRequest req);
    Task<ResetPinResult?>         ResetPinAsync(int classId, int studentId);
    Task<SendPinResult?>          SendPinAsync(int classId, int studentId);
    Task<SendAllResult>           SendAllPinsAsync(int classId, int professorId, bool isAdmin);
    Task<(byte[] Content, string FileName)?> ExportStudentsAsync(int? classId, int professorId, bool isAdmin);
}

public class ClassService(AppDbContext db, IEmailService email) : IClassService
{
    // ── Classes ──────────────────────────────────────────────────────────────

    public async Task<List<ClassDto>> GetAllAsync(int? professorId)
    {
        var q = db.Classes
            .Include(c => c.Professor)
            .Include(c => c.Students)
            .AsQueryable();

        if (professorId.HasValue)
            q = q.Where(c => c.ProfessorId == professorId.Value);

        return await q.OrderBy(c => c.Name)
            .Select(c => ToDto(c))
            .ToListAsync();
    }

    public async Task<ClassDto?> GetByIdAsync(int id, int? professorId)
    {
        var c = await db.Classes
            .Include(c => c.Professor)
            .Include(c => c.Students)
            .FirstOrDefaultAsync(c => c.Id == id &&
                (!professorId.HasValue || c.ProfessorId == professorId.Value));

        return c is null ? null : ToDto(c);
    }

    public async Task<ClassDto> CreateAsync(int professorId, CreateClassRequest req)
    {
        var classe = new Class
        {
            ProfessorId  = professorId,
            Name         = req.Name.Trim(),
            AcademicYear = req.AcademicYear?.Trim()
        };
        db.Classes.Add(classe);
        await db.SaveChangesAsync();

        var professor = await db.Professors.FindAsync(professorId);
        return new ClassDto(classe.Id, classe.ProfessorId, professor!.NomComplet,
            classe.Name, classe.AcademicYear, classe.CreatedAt, 0);
    }

    public async Task<ClassDto?> UpdateAsync(int id, int professorId, bool isAdmin, UpdateClassRequest req)
    {
        var c = await db.Classes
            .Include(c => c.Professor)
            .Include(c => c.Students)
            .FirstOrDefaultAsync(c => c.Id == id &&
                (isAdmin || c.ProfessorId == professorId));
        if (c is null) return null;

        c.Name         = req.Name.Trim();
        c.AcademicYear = req.AcademicYear?.Trim();
        await db.SaveChangesAsync();
        return ToDto(c);
    }

    public async Task<bool> DeleteAsync(int id, int professorId, bool isAdmin)
    {
        var c = await db.Classes.FirstOrDefaultAsync(c => c.Id == id &&
            (isAdmin || c.ProfessorId == professorId));
        if (c is null) return false;
        db.Classes.Remove(c);
        await db.SaveChangesAsync();
        return true;
    }

    // ── Alumnes ──────────────────────────────────────────────────────────────

    public async Task<List<StudentWithPinDto>> GetStudentsAsync(int classId, int professorId, bool isAdmin)
    {
        var classExists = await db.Classes.AnyAsync(c => c.Id == classId &&
            (isAdmin || c.ProfessorId == professorId));
        if (!classExists) return [];

        return await db.Students
            .Where(s => s.ClassId == classId)
            .OrderBy(s => s.NumLlista)
            .Select(s => ToStudentDto(s))
            .ToListAsync();
    }

    public async Task<StudentWithPinDto> AddStudentAsync(int classId, CreateStudentRequest req)
    {
        var pin = string.IsNullOrWhiteSpace(req.Pin) ? GeneratePin() : req.Pin.Trim();
        var student = new Student
        {
            ClassId          = classId,
            NumLlista        = req.NumLlista,
            Nom              = req.Nom.Trim(),
            Cognoms          = req.Cognoms.Trim(),
            CorreuElectronic = req.CorreuElectronic?.Trim(),
            Pin              = pin
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return ToStudentDto(student);
    }

    public async Task<StudentWithPinDto?> UpdateStudentAsync(int classId, int studentId, UpdateStudentRequest req)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId && s.ClassId == classId);
        if (student is null) return null;

        student.NumLlista        = req.NumLlista;
        student.Nom              = req.Nom.Trim();
        student.Cognoms          = req.Cognoms.Trim();
        student.CorreuElectronic = req.CorreuElectronic?.Trim();

        if (!string.IsNullOrWhiteSpace(req.NewPin))
            student.Pin = req.NewPin.Trim();

        await db.SaveChangesAsync();
        return ToStudentDto(student);
    }

    public async Task<bool> DeleteStudentAsync(int classId, int studentId)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId && s.ClassId == classId);
        if (student is null) return false;
        db.Students.Remove(student);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<BulkCreateResult> BulkAddStudentsAsync(int classId, BulkCreateStudentsRequest req)
    {
        int created = 0, skipped = 0;
        var errors   = new List<string>();
        var toEmail  = new List<Student>();

        var classe = await db.Classes.FindAsync(classId);

        foreach (var s in req.Students)
        {
            try
            {
                var exists = await db.Students.AnyAsync(x => x.ClassId == classId && x.NumLlista == s.NumLlista);
                if (exists)
                {
                    skipped++;
                    errors.Add($"Núm. {s.NumLlista} ({s.Cognoms}, {s.Nom}): ja existeix, omès.");
                    continue;
                }

                var pin = string.IsNullOrWhiteSpace(s.Pin) ? GeneratePin() : s.Pin.Trim();
                var student = new Student
                {
                    ClassId          = classId,
                    NumLlista        = s.NumLlista,
                    Nom              = s.Nom.Trim(),
                    Cognoms          = s.Cognoms.Trim(),
                    CorreuElectronic = s.CorreuElectronic?.Trim(),
                    Pin              = pin
                };
                db.Students.Add(student);
                created++;
                if (!string.IsNullOrWhiteSpace(student.CorreuElectronic))
                    toEmail.Add(student);
            }
            catch (Exception ex)
            {
                errors.Add($"Núm. {s.NumLlista} ({s.Cognoms}, {s.Nom}): error – {ex.Message}");
            }
        }

        if (created > 0)
            await db.SaveChangesAsync();

        return new BulkCreateResult(created, skipped, errors);
    }

    public async Task<ResetPinResult?> ResetPinAsync(int classId, int studentId)
    {
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.Id == studentId && s.ClassId == classId);
        if (student is null) return null;

        student.Pin = GeneratePin();
        await db.SaveChangesAsync();
        return new ResetPinResult(student.Pin);
    }

    public async Task<SendPinResult?> SendPinAsync(int classId, int studentId)
    {
        var student = await db.Students
            .Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == studentId && s.ClassId == classId);
        if (student is null) return null;

        if (string.IsNullOrWhiteSpace(student.CorreuElectronic))
            return new SendPinResult(false, "L'alumne no té correu electrònic.");

        if (!email.IsEnabled)
            return new SendPinResult(false, "El servei de correu no està configurat.");

        var sent = await email.SendPinAsync(student.CorreuElectronic, student.NomComplet,
            student.Class.Name, student.Id, student.Pin);
        return new SendPinResult(sent, sent ? null : "Error en l'enviament.");
    }

    public async Task<SendAllResult> SendAllPinsAsync(int classId, int professorId, bool isAdmin)
    {
        var classOk = await db.Classes.AnyAsync(c => c.Id == classId &&
            (isAdmin || c.ProfessorId == professorId));
        if (!classOk) return new SendAllResult(0, 0, ["Classe no trobada."]);

        var students = await db.Students
            .Include(s => s.Class)
            .Where(s => s.ClassId == classId)
            .OrderBy(s => s.NumLlista)
            .ToListAsync();

        int sent = 0, skipped = 0;
        var details = new List<string>();

        foreach (var s in students)
        {
            if (string.IsNullOrWhiteSpace(s.CorreuElectronic)) { skipped++; continue; }
            var ok = await email.SendPinAsync(s.CorreuElectronic, s.NomComplet,
                s.Class.Name, s.Id, s.Pin);
            if (ok) sent++;
            else { skipped++; details.Add($"{s.NomComplet}: error d'enviament."); }
        }

        return new SendAllResult(sent, skipped, details);
    }

    public async Task<(byte[] Content, string FileName)?> ExportStudentsAsync(
        int? classId, int professorId, bool isAdmin)
    {
        var query = db.Students
            .Include(s => s.Class)
            .AsQueryable();

        if (classId.HasValue)
        {
            // Verificar accés a la classe
            var classOk = await db.Classes.AnyAsync(c => c.Id == classId.Value &&
                (isAdmin || c.ProfessorId == professorId));
            if (!classOk) return null;
            query = query.Where(s => s.ClassId == classId.Value);
        }
        else if (!isAdmin)
        {
            query = query.Where(s => s.Class.ProfessorId == professorId);
        }

        var students = await query
            .OrderBy(s => s.Class.Name)
            .ThenBy(s => s.NumLlista)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Classe;CursAcadèmic;NumLlista;Nom;Cognoms;CorreuElectronic;PIN");
        foreach (var s in students)
        {
            sb.AppendLine(string.Join(";",
                Esc(s.Class.Name),
                Esc(s.Class.AcademicYear ?? ""),
                s.NumLlista.ToString(),
                Esc(s.Nom),
                Esc(s.Cognoms),
                Esc(s.CorreuElectronic ?? ""),
                Esc(s.Pin)));
        }

        var nom = classId.HasValue
            ? $"alumnes_{students.FirstOrDefault()?.Class.Name?.Replace(" ", "_") ?? classId.ToString()}_{DateTime.Now:yyyyMMdd}.csv"
            : $"alumnes_totes_{DateTime.Now:yyyyMMdd}.csv";

        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return (bytes, nom);
    }

    private static string GeneratePin() => Random.Shared.Next(1000, 9999).ToString();
    private static string Esc(string v) => $"\"{v.Replace("\"", "\"\"")}\"";

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ClassDto ToDto(Class c) => new(
        c.Id, c.ProfessorId, c.Professor.NomComplet,
        c.Name, c.AcademicYear, c.CreatedAt, c.Students.Count);

    private static StudentWithPinDto ToStudentDto(Student s) => new(
        s.Id, s.ClassId, s.Nom, s.Cognoms, s.NomComplet,
        s.NumLlista, s.Pin, s.CorreuElectronic, s.CreatedAt);
}
