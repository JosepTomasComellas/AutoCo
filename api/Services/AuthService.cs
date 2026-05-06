using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoCo.Api.Data;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;

namespace AutoCo.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> ProfessorLoginAsync(ProfessorLoginRequest req);
    Task<LoginResponse?> StudentLoginAsync(StudentLoginRequest req);
    Task<LoginResponse?> RefreshAsync(string refreshToken);
    Task               LogoutAsync(string refreshToken);
}

public class AuthService(AppDbContext db, IConfiguration config, IPhotoService photos,
    IDistributedCache cache) : IAuthService
{
    private static readonly DistributedCacheEntryOptions RefreshTtl = new()
        { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) };

    public async Task<LoginResponse?> ProfessorLoginAsync(ProfessorLoginRequest req)
    {
        var professor = await db.Professors
            .FirstOrDefaultAsync(p => p.Email == req.Email.Trim().ToLower());
        if (professor is null || !PasswordHelper.Verify(req.Password, professor.PasswordHash))
            return null;

        db.ProfessorLogins.Add(new AutoCo.Api.Data.Models.ProfessorLogin { ProfessorId = professor.Id });
        await db.SaveChangesAsync();

        var role         = professor.IsAdmin ? "Admin" : "Professor";
        var jwt          = GenerateToken(professor.Id.ToString(), professor.NomComplet, role);
        var refreshToken = await StoreRefreshTokenAsync(professor.Id, role);
        return new LoginResponse(jwt, professor.NomComplet, role, professor.Id,
            photos.GetProfessorFotoUrl(professor.Id), refreshToken);
    }

    public async Task<LoginResponse?> StudentLoginAsync(StudentLoginRequest req)
    {
        var student = await db.Students
            .FirstOrDefaultAsync(s => s.Email == req.Email.Trim().ToLower());
        if (student is null || !PasswordHelper.Verify(req.Password, student.PasswordHash))
            return null;

        var jwt          = GenerateToken(student.Id.ToString(), student.NomComplet, "Student",
            new Claim("classId", student.ClassId.ToString()));
        var refreshToken = await StoreRefreshTokenAsync(student.Id, "Student");
        return new LoginResponse(jwt, student.NomComplet, "Student", student.Id,
            null, refreshToken);
    }

    public async Task<LoginResponse?> RefreshAsync(string refreshToken)
    {
        var stored = await cache.GetStringAsync($"autoco:refresh:{refreshToken}");
        if (stored is null) return null;

        var parts = stored.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var id)) return null;
        var role = parts[1];

        string nomComplet;
        string? fotoUrl  = null;
        Claim[] extra    = [];

        if (role is "Professor" or "Admin")
        {
            var prof = await db.Professors.FindAsync(id);
            if (prof is null) { await cache.RemoveAsync($"autoco:refresh:{refreshToken}"); return null; }
            role      = prof.IsAdmin ? "Admin" : "Professor";
            nomComplet = prof.NomComplet;
            fotoUrl    = photos.GetProfessorFotoUrl(prof.Id);
        }
        else
        {
            var student = await db.Students.FindAsync(id);
            if (student is null) { await cache.RemoveAsync($"autoco:refresh:{refreshToken}"); return null; }
            nomComplet = student.NomComplet;
            extra      = [new Claim("classId", student.ClassId.ToString())];
        }

        // Rotació: invalida el token vell, emet token nou
        await cache.RemoveAsync($"autoco:refresh:{refreshToken}");
        var newJwt          = GenerateToken(id.ToString(), nomComplet, role, extra);
        var newRefreshToken = await StoreRefreshTokenAsync(id, role);
        return new LoginResponse(newJwt, nomComplet, role, id, fotoUrl, newRefreshToken);
    }

    public async Task LogoutAsync(string refreshToken) =>
        await cache.RemoveAsync($"autoco:refresh:{refreshToken}");

    private async Task<string> StoreRefreshTokenAsync(int id, string role)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        await cache.SetStringAsync($"autoco:refresh:{token}", $"{id}:{role}", RefreshTtl);
        return token;
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
