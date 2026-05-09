using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Api.Services;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AutoCo.Tests;

public class CicleServiceTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static CicleService CreateService(AppDbContext db) => new(db);

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Empty_ReturnsEmptyList()
    {
        using var db  = CreateDb(nameof(GetAll_Empty_ReturnsEmptyList));
        var svc = CreateService(db);

        var result = await svc.GetAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAll_MultipleCicles_ReturnsSortedByName()
    {
        using var db = CreateDb(nameof(GetAll_MultipleCicles_ReturnsSortedByName));
        db.Cicles.AddRange(
            new Cicle { Name = "SMX" },
            new Cicle { Name = "DAM" },
            new Cicle { Name = "ASIX" });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var result = await svc.GetAllAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal("ASIX", result[0].Name);
        Assert.Equal("DAM",  result[1].Name);
        Assert.Equal("SMX",  result[2].Name);
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidName_ReturnsCicleDto()
    {
        using var db = CreateDb(nameof(Create_ValidName_ReturnsCicleDto));
        var svc = CreateService(db);

        var result = await svc.CreateAsync(new CreateCicleRequest("DAM"));

        Assert.True(result.Id > 0);
        Assert.Equal("DAM", result.Name);
        Assert.Equal(0, result.NumClasses);
    }

    [Fact]
    public async Task Create_TrimsWhitespace()
    {
        using var db = CreateDb(nameof(Create_TrimsWhitespace));
        var svc = CreateService(db);

        var result = await svc.CreateAsync(new CreateCicleRequest("  DAM  "));

        Assert.Equal("DAM", result.Name);
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ExistingCicle_ReturnsUpdated()
    {
        using var db = CreateDb(nameof(Update_ExistingCicle_ReturnsUpdated));
        var cicle = new Cicle { Name = "DAM" };
        db.Cicles.Add(cicle);
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var result = await svc.UpdateAsync(cicle.Id, new UpdateCicleRequest("DAW"));

        Assert.NotNull(result);
        Assert.Equal("DAW", result!.Name);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNull()
    {
        using var db = CreateDb(nameof(Update_NotFound_ReturnsNull));
        var svc = CreateService(db);

        var result = await svc.UpdateAsync(999, new UpdateCicleRequest("X"));

        Assert.Null(result);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_EmptyCicle_ReturnsTrue()
    {
        using var db = CreateDb(nameof(Delete_EmptyCicle_ReturnsTrue));
        var cicle = new Cicle { Name = "DAM" };
        db.Cicles.Add(cicle);
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var ok = await svc.DeleteAsync(cicle.Id);

        Assert.True(ok);
        Assert.Equal(0, await db.Cicles.CountAsync());
    }

    [Fact]
    public async Task Delete_NotFound_ReturnsFalse()
    {
        using var db = CreateDb(nameof(Delete_NotFound_ReturnsFalse));
        var svc = CreateService(db);

        var ok = await svc.DeleteAsync(999);

        Assert.False(ok);
    }

    [Fact]
    public async Task Delete_CicleWithClasses_ThrowsInvalidOperation()
    {
        using var db = CreateDb(nameof(Delete_CicleWithClasses_ThrowsInvalidOperation));
        var cicle = new Cicle { Name = "DAM" };
        db.Cicles.Add(cicle);
        await db.SaveChangesAsync();
        db.Classes.Add(new Class { Name = "DAM1", CicleId = cicle.Id });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteAsync(cicle.Id));
    }

    // ── NumClasses ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_NumClasses_ReflectsAssignedClasses()
    {
        using var db = CreateDb(nameof(GetAll_NumClasses_ReflectsAssignedClasses));
        var cicle = new Cicle { Name = "DAM" };
        db.Cicles.Add(cicle);
        await db.SaveChangesAsync();
        db.Classes.AddRange(
            new Class { Name = "DAM1", CicleId = cicle.Id },
            new Class { Name = "DAM2", CicleId = cicle.Id });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var result = await svc.GetAllAsync();

        Assert.Equal(2, result[0].NumClasses);
    }
}
