using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutoCo.Api.Data;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AutoCo.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> ProfessorLoginAsync(ProfessorLoginRequest req);
    Task<LoginResponse?> StudentLoginAsync(StudentLoginRequest req);
}

public class AuthService(AppDbContext db, IConfiguration config) : IAuthService
{
    public async Task<LoginResponse?> ProfessorLoginAsync(ProfessorLoginRequest req)
    {
        var professor = await db.Professors
            .FirstOrDefaultAsync(p => p.Email == req.Email.Trim().ToLower());
        if (professor is null || !BCrypt.Net.BCrypt.Verify(req.Password, professor.PasswordHash))
            return null;

        var role  = professor.IsAdmin ? "Admin" : "Professor";
        var token = GenerateToken(professor.Id.ToString(), professor.NomComplet, role);
        return new LoginResponse(token, professor.NomComplet, role, professor.Id);
    }

    public async Task<LoginResponse?> StudentLoginAsync(StudentLoginRequest req)
    {
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.Email == req.Email.Trim().ToLower());
        if (student is null || !BCrypt.Net.BCrypt.Verify(req.Password, student.PasswordHash))
            return null;

        var token = GenerateToken(student.Id.ToString(), student.NomComplet, "Student",
            new Claim("classId", student.ClassId.ToString()));
        return new LoginResponse(token, student.NomComplet, "Student", student.Id);
    }

    private string GenerateToken(string userId, string nomComplet, string role, params Claim[] extraClaims)
    {
        var secret = config["JwtSettings:Secret"]!;
        var hours  = int.TryParse(config["JwtSettings:ExpiryHours"], out var h) ? h : 8;
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name,           nomComplet),
            new(ClaimTypes.Role,           role)
        };
        claims.AddRange(extraClaims);

        var token = new JwtSecurityToken(
            claims: claims, expires: DateTime.UtcNow.AddHours(hours),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
