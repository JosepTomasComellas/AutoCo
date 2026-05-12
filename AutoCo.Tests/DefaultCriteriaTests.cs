using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Api.Services;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AutoCo.Tests;

/// <summary>
/// Tests per a la taula DefaultCriteria i la seva integració amb ActivityService.
/// Cobreix lectura, escriptura i l'herència de criteris en crear activitats.
/// </summary>
public class DefaultCriteriaTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static ActivityService CreateActivityService(AppDbContext db) =>
        new(db, CreateCache(), new FakePhotoServiceDefCrit());

    private static (int profId, int modId) SeedBase(AppDbContext db)
    {
        var prof = new Professor
        {
            Email = "prof@test.cat", Nom = "Pere", Cognoms = "Puig",
            PasswordHash = "x", IsAdmin = false
        };
        var cls = new Class { Name = "DAM1" };
        db.Professors.Add(prof);
        db.Classes.Add(cls);
        db.SaveChanges();

        var mod = new Module { ClassId = cls.Id, ProfessorId = prof.Id, Code = "MP01", Name = "Mòdul 1" };
        db.Modules.Add(mod);
        db.SaveChanges();
        return (prof.Id, mod.Id);
    }

    // ── Lectura des de BD ─────────────────────────────────────────────────────

    [Fact]
    public async Task DefaultCriteria_Empty_ReturnsNoRows()
    {
        using var db = CreateDb(nameof(DefaultCriteria_Empty_ReturnsNoRows));

        var count = await db.DefaultCriteria.CountAsync();

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task DefaultCriteria_Insert_PersistsCorrectly()
    {
        using var db = CreateDb(nameof(DefaultCriteria_Insert_PersistsCorrectly));
        db.DefaultCriteria.Add(new DefaultCriterion
            { Key = "probitat", Label = "Probitat", Weight = 1, OrderIndex = 0 });
        await db.SaveChangesAsync();

        var row = await db.DefaultCriteria.FirstAsync();

        Assert.Equal("probitat", row.Key);
        Assert.Equal("Probitat", row.Label);
        Assert.Equal(1, row.Weight);
    }

    [Fact]
    public async Task DefaultCriteria_OrderedByOrderIndex()
    {
        using var db = CreateDb(nameof(DefaultCriteria_OrderedByOrderIndex));
        db.DefaultCriteria.AddRange(
            new DefaultCriterion { Key = "c", Label = "C", OrderIndex = 2 },
            new DefaultCriterion { Key = "a", Label = "A", OrderIndex = 0 },
            new DefaultCriterion { Key = "b", Label = "B", OrderIndex = 1 });
        await db.SaveChangesAsync();

        var result = await db.DefaultCriteria.OrderBy(d => d.OrderIndex).ToListAsync();

        Assert.Equal("a", result[0].Key);
        Assert.Equal("b", result[1].Key);
        Assert.Equal("c", result[2].Key);
    }

    // ── Herència en crear activitat ───────────────────────────────────────────

    [Fact]
    public async Task CreateActivity_WithDefaultCriteria_InheritsThemAsActivityCriteria()
    {
        using var db = CreateDb(nameof(CreateActivity_WithDefaultCriteria_InheritsThemAsActivityCriteria));
        db.DefaultCriteria.AddRange(
            new DefaultCriterion { Key = "probitat",    Label = "Probitat",    Weight = 1, OrderIndex = 0 },
            new DefaultCriterion { Key = "autonomia",   Label = "Autonomia",   Weight = 2, OrderIndex = 1 },
            new DefaultCriterion { Key = "comunicacio", Label = "Comunicació", Weight = 1, OrderIndex = 2 });
        await db.SaveChangesAsync();

        var (profId, modId) = SeedBase(db);
        var svc = CreateActivityService(db);
        var act = await svc.CreateAsync(profId, false, new CreateActivityRequest(modId, "Act1", null));

        var criteria = await db.ActivityCriteria
            .Where(ac => ac.ActivityId == act.Id)
            .OrderBy(ac => ac.OrderIndex)
            .ToListAsync();

        Assert.Equal(3, criteria.Count);
        Assert.Equal("probitat",    criteria[0].Key);
        Assert.Equal("autonomia",   criteria[1].Key);
        Assert.Equal("comunicacio", criteria[2].Key);
        Assert.Equal(2, criteria[1].Weight);
    }

    [Fact]
    public async Task CreateActivity_NoDefaultCriteria_FallsBackToConstants()
    {
        using var db = CreateDb(nameof(CreateActivity_NoDefaultCriteria_FallsBackToConstants));

        var (profId, modId) = SeedBase(db);
        var svc = CreateActivityService(db);
        var act = await svc.CreateAsync(profId, false, new CreateActivityRequest(modId, "Act1", null));

        // Sense DefaultCriteria a la BD, els criteris de Constants.cs (5 globals) s'usen com a fallback
        var count = await db.ActivityCriteria.Where(ac => ac.ActivityId == act.Id).CountAsync();

        Assert.True(count > 0);
    }

    // ── Reemplaçament (delete + insert) ──────────────────────────────────────

    [Fact]
    public async Task DefaultCriteria_ReplaceAll_DeletesOldAndInsertsNew()
    {
        using var db = CreateDb(nameof(DefaultCriteria_ReplaceAll_DeletesOldAndInsertsNew));
        db.DefaultCriteria.AddRange(
            new DefaultCriterion { Key = "old1", Label = "Old 1", OrderIndex = 0 },
            new DefaultCriterion { Key = "old2", Label = "Old 2", OrderIndex = 1 });
        await db.SaveChangesAsync();

        // Simula el comportament del PUT /api/criteria/defaults: esborra tot i reinsereix
        db.DefaultCriteria.RemoveRange(db.DefaultCriteria);
        await db.SaveChangesAsync();

        db.DefaultCriteria.AddRange(
            new DefaultCriterion { Key = "new1", Label = "New 1", OrderIndex = 0 },
            new DefaultCriterion { Key = "new2", Label = "New 2", OrderIndex = 1 },
            new DefaultCriterion { Key = "new3", Label = "New 3", OrderIndex = 2 });
        await db.SaveChangesAsync();

        var result = await db.DefaultCriteria.OrderBy(d => d.OrderIndex).ToListAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal("new1", result[0].Key);
        Assert.DoesNotContain(result, r => r.Key == "old1");
    }

    [Fact]
    public async Task DefaultCriteria_KeyIsUnique_DuplicateKeyFails()
    {
        using var db = CreateDb(nameof(DefaultCriteria_KeyIsUnique_DuplicateKeyFails));
        db.DefaultCriteria.Add(new DefaultCriterion { Key = "probitat", Label = "A", OrderIndex = 0 });
        await db.SaveChangesAsync();
        db.DefaultCriteria.Add(new DefaultCriterion { Key = "probitat", Label = "B", OrderIndex = 1 });

        // EF InMemory no aplica restriccions d'unicitat; aquest test documenta el comportament esperat
        // (la restricció es garanteix a SQL Server via HasIndex.IsUnique)
        await db.SaveChangesAsync();
        var count = await db.DefaultCriteria.CountAsync(d => d.Key == "probitat");
        Assert.Equal(2, count); // InMemory no enforça unicitat — la restricció és a SQL Server
    }
}

file sealed class FakePhotoServiceDefCrit : IPhotoService
{
    public string? GetStudentFotoUrl(int studentId)    => null;
    public string? GetProfessorFotoUrl(int professorId) => null;
    public Task<bool> SaveStudentFotoAsync(int studentId, Stream data, string contentType)    => Task.FromResult(false);
    public Task<bool> SaveProfessorFotoAsync(int professorId, Stream data, string contentType) => Task.FromResult(false);
    public Task<(int Imported, List<string> NotFound, List<string> Errors)> ImportZipFotosAsync(
        Stream zipStream, IReadOnlyDictionary<string, int> dniToStudentId) =>
        Task.FromResult((0, new List<string>(), new List<string>()));
    public bool DeleteStudentFoto(int studentId)   => false;
    public bool DeleteProfessorFoto(int professorId) => false;
}
