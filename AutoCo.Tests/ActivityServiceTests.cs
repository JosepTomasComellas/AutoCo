using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Api.Services;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AutoCo.Tests;

public class ActivityServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static ActivityService CreateService(AppDbContext db) =>
        new(db, CreateCache(), new FakePhotoServiceAct());

    private static (int profId, int classId, int moduleId) SeedBase(AppDbContext db)
    {
        var prof = new Professor
        {
            Email = "prof@test.cat", Nom = "Maria", Cognoms = "Garcia",
            PasswordHash = "x", IsAdmin = false
        };
        var cls = new Class { Name = "DAW1" };
        db.Professors.Add(prof);
        db.Classes.Add(cls);
        db.SaveChanges();

        var mod = new Module { ClassId = cls.Id, ProfessorId = prof.Id, Code = "MP01", Name = "Mòdul 1" };
        db.Modules.Add(mod);
        db.SaveChanges();

        return (prof.Id, cls.Id, mod.Id);
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_ReturnsActivity()
    {
        using var db = CreateDb(nameof(Create_ValidRequest_ReturnsActivity));
        var (profId, _, modId) = SeedBase(db);
        var svc = CreateService(db);

        var dto = await svc.CreateAsync(profId, false,
            new CreateActivityRequest(modId, "Activitat 1", null));

        Assert.NotNull(dto);
        Assert.Equal("Activitat 1", dto.Name);
        Assert.True(dto.IsOpen); // les activitats es creen obertes per defecte
    }

    [Fact]
    public async Task Create_WrongProfessor_ThrowsUnauthorized()
    {
        using var db = CreateDb(nameof(Create_WrongProfessor_ThrowsUnauthorized));
        var (_, _, modId) = SeedBase(db);
        var svc = CreateService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(999, false, new CreateActivityRequest(modId, "X", null)));
    }

    [Fact]
    public async Task Create_AsAdmin_CanAccessAnyModule()
    {
        using var db = CreateDb(nameof(Create_AsAdmin_CanAccessAnyModule));
        var (_, _, modId) = SeedBase(db);
        var svc = CreateService(db);

        var dto = await svc.CreateAsync(999, isAdmin: true,
            new CreateActivityRequest(modId, "Admin Activity", null));

        Assert.NotNull(dto);
        Assert.Equal("Admin Activity", dto.Name);
    }

    // ── ToggleOpenAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task Toggle_OpenActivity_ClosesIt()
    {
        using var db = CreateDb(nameof(Toggle_OpenActivity_ClosesIt));
        var (profId, _, modId) = SeedBase(db);
        var svc = CreateService(db);
        var created = await svc.CreateAsync(profId, false,
            new CreateActivityRequest(modId, "Test", null));

        // Tancar (l'activitat s'ha creat oberta)
        var closed = await svc.ToggleOpenAsync(created.Id, profId, false);
        Assert.False(closed!.IsOpen);

        // Obrir
        var opened = await svc.ToggleOpenAsync(created.Id, profId, false);
        Assert.True(opened!.IsOpen);
    }

    [Fact]
    public async Task Toggle_NonExistentActivity_ReturnsNull()
    {
        using var db = CreateDb(nameof(Toggle_NonExistentActivity_ReturnsNull));
        var svc = CreateService(db);

        var result = await svc.ToggleOpenAsync(999, 1, false);

        Assert.Null(result);
    }

    // ── GetAllPagedAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllPaged_ReturnsCorrectPage()
    {
        using var db = CreateDb(nameof(GetAllPaged_ReturnsCorrectPage));
        var (profId, _, modId) = SeedBase(db);
        var svc = CreateService(db);

        for (int i = 1; i <= 5; i++)
            await svc.CreateAsync(profId, false, new CreateActivityRequest(modId, $"Act {i}", null));

        var (items, total) = await svc.GetAllPagedAsync(profId, page: 1, size: 3);

        Assert.Equal(5, total);
        Assert.Equal(3, items.Count);
    }
}

// ── Fake de IPhotoService per a tests d'activitat ────────────────────────────

file sealed class FakePhotoServiceAct : IPhotoService
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
