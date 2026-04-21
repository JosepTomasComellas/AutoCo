using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using AutoCo.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AutoCo.Tests;

/// <summary>
/// Tests unitaris de la lògica de càlcul de ResultsService.
/// Usa EF Core InMemory per simular la BD sense necessitat de SQL Server.
/// </summary>
public class ResultsServiceTests
{
    // ── Helpers de construcció ────────────────────────────────────────────────

    private static AppDbContext CreateDb(string name)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(opts);
    }

    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    /// <summary>Dades bàsiques mínimes: professor, classe, mòdul, activitat, grup i alumne.</summary>
    private static (AppDbContext db, int profId, int actId, int s1Id) SeedBase(string dbName)
    {
        var db = CreateDb(dbName);

        var prof = new Professor
        {
            Id = 1, Email = "prof@test.cat", Nom = "Maria", Cognoms = "Garcia",
            PasswordHash = "x", IsAdmin = false
        };
        var cls = new Class { Id = 1, Name = "DAW1" };
        var mod = new Module
        {
            Id = 1, ClassId = 1, ProfessorId = 1,
            Code = "MP01", Name = "Mòdul 1"
        };
        var act = new Activity
        {
            Id = 1, ModuleId = 1, Name = "Activitat 1", IsOpen = true
        };
        var grp = new Group { Id = 1, ActivityId = 1, Name = "Grup A" };
        var s1  = new Student
        {
            Id = 1, ClassId = 1, NumLlista = 1,
            Nom = "Joan", Cognoms = "Puig", Email = "joan@test.cat", PasswordHash = "x"
        };
        var s2  = new Student
        {
            Id = 2, ClassId = 1, NumLlista = 2,
            Nom = "Anna", Cognoms = "Roca", Email = "anna@test.cat", PasswordHash = "x"
        };

        db.Professors.Add(prof);
        db.Classes.Add(cls);
        db.Modules.Add(mod);
        db.Activities.Add(act);
        db.Groups.Add(grp);
        db.Students.AddRange(s1, s2);
        db.GroupMembers.AddRange(
            new GroupMember { GroupId = 1, StudentId = 1 },
            new GroupMember { GroupId = 1, StudentId = 2 });
        db.SaveChanges();

        return (db, prof.Id, act.Id, s1.Id);
    }

    /// <summary>Afegeix una autoavaluació per a l'alumne 1 (IsSelf=true) amb 5 criteris globals.</summary>
    private static void AddSelfEval(AppDbContext db, int actId, int studentId,
        double score = 7.5)
    {
        var eval = new Evaluation
        {
            Id = 10 + studentId, ActivityId = actId,
            EvaluatorId = studentId, EvaluatedId = studentId, IsSelf = true
        };
        db.Evaluations.Add(eval);
        db.SaveChanges();
        foreach (var (key, _) in Criteria.All)
            db.EvaluationScores.Add(new EvaluationScore
                { EvaluationId = eval.Id, CriteriaKey = key, Score = score });
        db.SaveChanges();
    }

    /// <summary>Afegeix una coavaluació de s2→s1 amb tots els criteris globals.</summary>
    private static void AddPeerEval(AppDbContext db, int actId,
        int evaluatorId, int evaluatedId, double score, int evalId)
    {
        var eval = new Evaluation
        {
            Id = evalId, ActivityId = actId,
            EvaluatorId = evaluatorId, EvaluatedId = evaluatedId, IsSelf = false
        };
        db.Evaluations.Add(eval);
        db.SaveChanges();
        foreach (var (key, _) in Criteria.All)
            db.EvaluationScores.Add(new EvaluationScore
                { EvaluationId = eval.Id, CriteriaKey = key, Score = score });
        db.SaveChanges();
    }

    // ── Tests de control d'accés ──────────────────────────────────────────────

    [Fact]
    public async Task GetResults_ActivityNotFound_ReturnsNull()
    {
        var db  = CreateDb(nameof(GetResults_ActivityNotFound_ReturnsNull));
        var svc = new ResultsService(db, CreateCache());

        var result = await svc.GetResultsAsync(activityId: 999, professorId: 1, isAdmin: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetResults_OtherProfessor_ReturnsNull()
    {
        var (db, _, actId, _) = SeedBase(nameof(GetResults_OtherProfessor_ReturnsNull));
        var svc = new ResultsService(db, CreateCache());

        // El professor amb Id=2 no és el propietari del mòdul
        var result = await svc.GetResultsAsync(actId, professorId: 2, isAdmin: false);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetResults_Admin_CanAccessAnyActivity()
    {
        var (db, _, actId, _) = SeedBase(nameof(GetResults_Admin_CanAccessAnyActivity));
        var svc = new ResultsService(db, CreateCache());

        // isAdmin=true: bypassa la comprovació de propietat
        var result = await svc.GetResultsAsync(actId, professorId: 99, isAdmin: true);

        Assert.NotNull(result);
    }

    // ── Tests de càlcul de notes ─────────────────────────────────────────────

    [Fact]
    public async Task GetResults_NoEvaluations_BothAveragesNull()
    {
        var (db, profId, actId, _) = SeedBase(nameof(GetResults_NoEvaluations_BothAveragesNull));
        var svc = new ResultsService(db, CreateCache());

        var result = await svc.GetResultsAsync(actId, profId, isAdmin: false);

        Assert.NotNull(result);
        Assert.All(result.Students, s =>
        {
            Assert.Null(s.AvgGlobal);
            Assert.Null(s.AutAvgGlobal);
        });
    }

    [Fact]
    public async Task GetResults_SelfEvalOnly_AutAvgGlobalCorrect()
    {
        var (db, profId, actId, s1Id) = SeedBase(nameof(GetResults_SelfEvalOnly_AutAvgGlobalCorrect));
        // S1 s'autoavalua amb 7.5 en tots els criteris
        AddSelfEval(db, actId, s1Id, score: 7.5);

        var svc    = new ResultsService(db, CreateCache());
        var result = await svc.GetResultsAsync(actId, profId, isAdmin: false);

        var s1 = result!.Students.Single(s => s.StudentId == s1Id);
        Assert.Equal(7.5, s1.AutAvgGlobal);   // mitjana de 5 criteris tots a 7.5
        Assert.Null(s1.AvgGlobal);             // cap coavaluació → null
    }

    [Fact]
    public async Task GetResults_PeerEvalOnly_AvgGlobalCorrect()
    {
        var (db, profId, actId, s1Id) = SeedBase(nameof(GetResults_PeerEvalOnly_AvgGlobalCorrect));
        // S2 (id=2) coavalua S1 (id=1) amb 5.0
        AddPeerEval(db, actId, evaluatorId: 2, evaluatedId: s1Id, score: 5.0, evalId: 50);

        var svc    = new ResultsService(db, CreateCache());
        var result = await svc.GetResultsAsync(actId, profId, isAdmin: false);

        var s1 = result!.Students.Single(s => s.StudentId == s1Id);
        Assert.Equal(5.0, s1.AvgGlobal!.Value, precision: 5);  // única coavaluació → 5.0
        Assert.Null(s1.AutAvgGlobal);                    // cap autoavaluació → null
    }

    [Fact]
    public async Task GetResults_BothEvals_BothAveragesCorrect()
    {
        var (db, profId, actId, s1Id) = SeedBase(nameof(GetResults_BothEvals_BothAveragesCorrect));
        AddSelfEval(db, actId, s1Id, score: 10.0);
        AddPeerEval(db, actId, evaluatorId: 2, evaluatedId: s1Id, score: 5.0, evalId: 51);

        var svc    = new ResultsService(db, CreateCache());
        var result = await svc.GetResultsAsync(actId, profId, isAdmin: false);

        var s1 = result!.Students.Single(s => s.StudentId == s1Id);
        Assert.Equal(10.0, s1.AutAvgGlobal!.Value, precision: 5);
        Assert.Equal(5.0,  s1.AvgGlobal!.Value,    precision: 5);
    }

    [Fact]
    public async Task GetResults_MultiplePeerEvals_AvgGlobalIsAverage()
    {
        var (db, profId, actId, s1Id) = SeedBase(
            nameof(GetResults_MultiplePeerEvals_AvgGlobalIsAverage));

        // Tres alumnes avaluen S1: 5.0, 7.5, 10.0 → mitjana = 7.5
        // Necessitem un tercer alumne i membre de grup
        var s3 = new Student
        {
            Id = 3, ClassId = 1, NumLlista = 3,
            Nom = "Pau", Cognoms = "Mas", Email = "pau@test.cat", PasswordHash = "x"
        };
        db.Students.Add(s3);
        db.GroupMembers.Add(new GroupMember { GroupId = 1, StudentId = 3 });
        db.SaveChanges();

        AddPeerEval(db, actId, evaluatorId: 2, evaluatedId: s1Id, score: 5.0,  evalId: 60);
        AddPeerEval(db, actId, evaluatorId: 3, evaluatedId: s1Id, score: 10.0, evalId: 61);

        var svc    = new ResultsService(db, CreateCache());
        var result = await svc.GetResultsAsync(actId, profId, isAdmin: false);

        var s1 = result!.Students.Single(s => s.StudentId == s1Id);
        // 2 peers, 5 criteris cadascun: mitjana per criteri = 7.5, global = 7.5
        Assert.Equal(7.5, s1.AvgGlobal!.Value, precision: 5);
    }

    [Fact]
    public async Task GetResults_AvgCoScores_PerCriteriaCorrect()
    {
        var (db, profId, actId, s1Id) = SeedBase(nameof(GetResults_AvgCoScores_PerCriteriaCorrect));
        // Usem criteris personalitzats per simplificar (un sol criteri)
        db.ActivityCriteria.Add(new ActivityCriterion
        {
            Id = 1, ActivityId = actId, Key = "qualitat", Label = "Qualitat", OrderIndex = 0
        });
        db.SaveChanges();

        // S2 coavalua S1 amb 8.5 al criteri "qualitat"
        var eval = new Evaluation
        {
            Id = 70, ActivityId = actId, EvaluatorId = 2, EvaluatedId = s1Id, IsSelf = false
        };
        db.Evaluations.Add(eval);
        db.SaveChanges();
        db.EvaluationScores.Add(new EvaluationScore
            { EvaluationId = 70, CriteriaKey = "qualitat", Score = 8.5 });
        db.SaveChanges();

        var svc    = new ResultsService(db, CreateCache());
        var result = await svc.GetResultsAsync(actId, profId, isAdmin: false);

        var s1 = result!.Students.Single(s => s.StudentId == s1Id);
        Assert.True(s1.AvgCoScores.TryGetValue("qualitat", out var coScore));
        Assert.Equal(8.5, coScore!.Value, precision: 5);
        Assert.Equal(8.5, s1.AvgGlobal!.Value, precision: 5);
    }

    [Fact]
    public async Task GetResults_PeerCount_CountsDistinctEvaluators()
    {
        var (db, profId, actId, s1Id) = SeedBase(nameof(GetResults_PeerCount_CountsDistinctEvaluators));
        // Afegim un tercer alumne
        db.Students.Add(new Student
        {
            Id = 3, ClassId = 1, NumLlista = 3,
            Nom = "Laia", Cognoms = "Font", Email = "laia@test.cat", PasswordHash = "x"
        });
        db.GroupMembers.Add(new GroupMember { GroupId = 1, StudentId = 3 });
        db.SaveChanges();

        AddPeerEval(db, actId, evaluatorId: 2, evaluatedId: s1Id, score: 7.5, evalId: 80);
        AddPeerEval(db, actId, evaluatorId: 3, evaluatedId: s1Id, score: 7.5, evalId: 81);

        var svc    = new ResultsService(db, CreateCache());
        var result = await svc.GetResultsAsync(actId, profId, isAdmin: false);

        var s1 = result!.Students.Single(s => s.StudentId == s1Id);
        Assert.Equal(2, s1.NumPeerEvaluators);
    }

    [Fact]
    public async Task GetResults_StudentsOrdered_ByGroupThenNumLlista()
    {
        var db = CreateDb(nameof(GetResults_StudentsOrdered_ByGroupThenNumLlista));

        var prof = new Professor
        {
            Id = 1, Email = "p@t.cat", Nom = "X", Cognoms = "Y",
            PasswordHash = "x", IsAdmin = false
        };
        var cls = new Class { Id = 1, Name = "C1" };
        var mod = new Module { Id = 1, ClassId = 1, ProfessorId = 1, Code = "M1", Name = "M" };
        var act = new Activity { Id = 1, ModuleId = 1, Name = "A", IsOpen = true };
        var gA  = new Group { Id = 1, ActivityId = 1, Name = "Alpha" };
        var gB  = new Group { Id = 2, ActivityId = 1, Name = "Beta" };
        var s1  = new Student
        {
            Id = 1, ClassId = 1, NumLlista = 2,
            Nom = "B", Cognoms = "B", Email = "b@t.cat", PasswordHash = "x"
        };
        var s2  = new Student
        {
            Id = 2, ClassId = 1, NumLlista = 1,
            Nom = "A", Cognoms = "A", Email = "a@t.cat", PasswordHash = "x"
        };
        var s3  = new Student
        {
            Id = 3, ClassId = 1, NumLlista = 1,
            Nom = "C", Cognoms = "C", Email = "c@t.cat", PasswordHash = "x"
        };

        db.Professors.Add(prof);
        db.Classes.Add(cls);
        db.Modules.Add(mod);
        db.Activities.Add(act);
        db.Groups.AddRange(gA, gB);
        db.Students.AddRange(s1, s2, s3);
        db.GroupMembers.AddRange(
            new GroupMember { GroupId = 1, StudentId = 1 }, // Alpha, Num=2
            new GroupMember { GroupId = 1, StudentId = 2 }, // Alpha, Num=1
            new GroupMember { GroupId = 2, StudentId = 3 }  // Beta,  Num=1
        );
        db.SaveChanges();

        var svc    = new ResultsService(db, CreateCache());
        var result = await svc.GetResultsAsync(1, professorId: 1, isAdmin: false);

        // Ordre esperat: Alpha/NumLlista1, Alpha/NumLlista2, Beta/NumLlista1
        var students = result!.Students;
        Assert.Equal(3, students.Count);
        Assert.Equal("Alpha", students[0].GroupName); Assert.Equal(1, students[0].NumLlista);
        Assert.Equal("Alpha", students[1].GroupName); Assert.Equal(2, students[1].NumLlista);
        Assert.Equal("Beta",  students[2].GroupName); Assert.Equal(1, students[2].NumLlista);
    }

    [Fact]
    public async Task GetResults_ReturnsCriteria_GlobalWhenNoCustom()
    {
        var (db, profId, actId, _) = SeedBase(nameof(GetResults_ReturnsCriteria_GlobalWhenNoCustom));
        var svc    = new ResultsService(db, CreateCache());
        var result = await svc.GetResultsAsync(actId, profId, isAdmin: false);

        // Sense criteris personalitzats → retorna els 5 globals
        Assert.Equal(Criteria.All.Count, result!.Criteria.Count);
        Assert.Equal(Criteria.All[0].Key, result.Criteria[0].Key);
    }

    [Fact]
    public async Task GetResults_ReturnsCriteria_CustomOverridesGlobal()
    {
        var (db, profId, actId, _) = SeedBase(
            nameof(GetResults_ReturnsCriteria_CustomOverridesGlobal));

        db.ActivityCriteria.AddRange(
            new ActivityCriterion { Id = 1, ActivityId = actId, Key = "c1", Label = "Criteri 1", OrderIndex = 0 },
            new ActivityCriterion { Id = 2, ActivityId = actId, Key = "c2", Label = "Criteri 2", OrderIndex = 1 }
        );
        db.SaveChanges();

        var svc    = new ResultsService(db, CreateCache());
        var result = await svc.GetResultsAsync(actId, profId, isAdmin: false);

        Assert.Equal(2, result!.Criteria.Count);
        Assert.Equal("c1", result.Criteria[0].Key);
        Assert.Equal("c2", result.Criteria[1].Key);
    }

    // ── Test de caché ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetResults_CacheHit_ReturnsSameData()
    {
        var (db, profId, actId, s1Id) = SeedBase(nameof(GetResults_CacheHit_ReturnsSameData));
        AddSelfEval(db, actId, s1Id, score: 5.0);

        var cache = CreateCache();
        var svc   = new ResultsService(db, cache);

        // Primera crida — escriu a caché
        var first = await svc.GetResultsAsync(actId, profId, isAdmin: false);

        // Modifiquem la BD directament (simula un canvi extern)
        AddPeerEval(db, actId, evaluatorId: 2, evaluatedId: s1Id, score: 10.0, evalId: 90);

        // Segona crida — ha de retornar les dades en caché (sense el canvi)
        var second = await svc.GetResultsAsync(actId, profId, isAdmin: false);

        var s1First  = first!.Students.Single(s => s.StudentId == s1Id);
        var s1Second = second!.Students.Single(s => s.StudentId == s1Id);
        Assert.Null(s1First.AvgGlobal);
        Assert.Null(s1Second.AvgGlobal); // caché → no reflexa el nou peer eval
    }

    [Fact]
    public async Task GetResults_AfterInvalidateCache_ReturnsFreshData()
    {
        var (db, profId, actId, s1Id) = SeedBase(
            nameof(GetResults_AfterInvalidateCache_ReturnsFreshData));
        AddSelfEval(db, actId, s1Id, score: 5.0);

        var cache = CreateCache();
        var svc   = new ResultsService(db, cache);

        // Primera crida — escriu a caché
        await svc.GetResultsAsync(actId, profId, isAdmin: false);

        // Invalidem i afegim nous datos
        await svc.InvalidateCacheAsync(actId);
        AddPeerEval(db, actId, evaluatorId: 2, evaluatedId: s1Id, score: 10.0, evalId: 91);

        // Tercera crida — ha de llegir de BD (dades fresques)
        var fresh = await svc.GetResultsAsync(actId, profId, isAdmin: false);

        var s1 = fresh!.Students.Single(s => s.StudentId == s1Id);
        Assert.Equal(10.0, s1.AvgGlobal!.Value, precision: 5);
    }
}
