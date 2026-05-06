using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AutoCo.Tests;

public class AuthServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"]      = "test-secret-32-characters-minimum!!",
                ["JwtSettings:ExpiryHours"] = "8"
            })
            .Build();

    private static IPhotoService CreateFakePhotos() => new FakePhotoService();

    private static AuthService CreateService(AppDbContext db) =>
        new(db, CreateConfig(), CreateFakePhotos(), CreateCache());

    private static Professor SeedProfessor(AppDbContext db, string email = "prof@test.cat")
    {
        var prof = new Professor
        {
            Email        = email,
            Nom          = "Maria",
            Cognoms      = "Garcia",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            IsAdmin      = false
        };
        db.Professors.Add(prof);
        db.SaveChanges();
        return prof;
    }

    private static Student SeedStudent(AppDbContext db, string email = "joan@test.cat")
    {
        var cls = new Class { Name = "DAW1" };
        db.Classes.Add(cls);
        db.SaveChanges();
        var student = new Student
        {
            ClassId      = cls.Id,
            Nom          = "Joan",
            Cognoms      = "Puig",
            NumLlista    = 1,
            Email        = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass456")
        };
        db.Students.Add(student);
        db.SaveChanges();
        return student;
    }

    // ── Professor login ───────────────────────────────────────────────────────

    [Fact]
    public async Task ProfessorLogin_CorrectCredentials_ReturnsTokenAndRefresh()
    {
        using var db = CreateDb(nameof(ProfessorLogin_CorrectCredentials_ReturnsTokenAndRefresh));
        SeedProfessor(db);
        var svc = CreateService(db);

        var result = await svc.ProfessorLoginAsync(new("prof@test.cat", "password123"));

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.NotNull(result.RefreshToken);
        Assert.Equal("Professor", result.Role);
    }

    [Fact]
    public async Task ProfessorLogin_WrongPassword_ReturnsNull()
    {
        using var db = CreateDb(nameof(ProfessorLogin_WrongPassword_ReturnsNull));
        SeedProfessor(db);
        var svc = CreateService(db);

        var result = await svc.ProfessorLoginAsync(new("prof@test.cat", "wrong"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ProfessorLogin_UnknownEmail_ReturnsNull()
    {
        using var db = CreateDb(nameof(ProfessorLogin_UnknownEmail_ReturnsNull));
        var svc = CreateService(db);

        var result = await svc.ProfessorLoginAsync(new("nobody@test.cat", "anything"));

        Assert.Null(result);
    }

    // ── Student login ─────────────────────────────────────────────────────────

    [Fact]
    public async Task StudentLogin_CorrectCredentials_ReturnsToken()
    {
        using var db = CreateDb(nameof(StudentLogin_CorrectCredentials_ReturnsToken));
        SeedStudent(db);
        var svc = CreateService(db);

        var result = await svc.StudentLoginAsync(new("joan@test.cat", "pass456"));

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.Equal("Student", result.Role);
    }

    [Fact]
    public async Task StudentLogin_WrongPassword_ReturnsNull()
    {
        using var db = CreateDb(nameof(StudentLogin_WrongPassword_ReturnsNull));
        SeedStudent(db);
        var svc = CreateService(db);

        var result = await svc.StudentLoginAsync(new("joan@test.cat", "wrong"));

        Assert.Null(result);
    }

    // ── Refresh token ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewLoginResponse()
    {
        using var db = CreateDb(nameof(Refresh_ValidToken_ReturnsNewLoginResponse));
        SeedProfessor(db);
        var svc = CreateService(db);

        var login    = await svc.ProfessorLoginAsync(new("prof@test.cat", "password123"));
        var refreshed = await svc.RefreshAsync(login!.RefreshToken!);

        Assert.NotNull(refreshed);
        Assert.NotEmpty(refreshed.Token);
        Assert.NotNull(refreshed.RefreshToken);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken); // token rotat
    }

    [Fact]
    public async Task Refresh_InvalidToken_ReturnsNull()
    {
        using var db = CreateDb(nameof(Refresh_InvalidToken_ReturnsNull));
        var svc = CreateService(db);

        var result = await svc.RefreshAsync("token-inexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task Logout_InvalidatesRefreshToken()
    {
        using var db = CreateDb(nameof(Logout_InvalidatesRefreshToken));
        SeedProfessor(db);
        var svc = CreateService(db);

        var login = await svc.ProfessorLoginAsync(new("prof@test.cat", "password123"));
        await svc.LogoutAsync(login!.RefreshToken!);
        var refreshed = await svc.RefreshAsync(login.RefreshToken!);

        Assert.Null(refreshed);
    }
}

// ── Fake de IPhotoService per a tests ────────────────────────────────────────

file sealed class FakePhotoService : IPhotoService
{
    public string? GetStudentFotoUrl(int studentId)   => null;
    public string? GetProfessorFotoUrl(int professorId) => null;
    public Task<bool> SaveStudentFotoAsync(int studentId, Stream data, string contentType) => Task.FromResult(false);
    public Task<bool> SaveProfessorFotoAsync(int professorId, Stream data, string contentType) => Task.FromResult(false);
    public Task<(int Imported, List<string> NotFound, List<string> Errors)> ImportZipFotosAsync(
        Stream zipStream, IReadOnlyDictionary<string, int> dniToStudentId) =>
        Task.FromResult((0, new List<string>(), new List<string>()));
    public bool DeleteStudentFoto(int studentId) => false;
    public bool DeleteProfessorFoto(int professorId) => false;
}
