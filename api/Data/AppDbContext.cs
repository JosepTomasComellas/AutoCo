using AutoCo.Api.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCo.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Professor>        Professors        => Set<Professor>();
    public DbSet<Class>            Classes           => Set<Class>();
    public DbSet<Student>          Students          => Set<Student>();
    public DbSet<Module>           Modules           => Set<Module>();
    public DbSet<ModuleExclusion>  ModuleExclusions  => Set<ModuleExclusion>();
    public DbSet<Activity>         Activities        => Set<Activity>();
    public DbSet<Group>            Groups            => Set<Group>();
    public DbSet<GroupMember>      GroupMembers      => Set<GroupMember>();
    public DbSet<Evaluation>          Evaluations          => Set<Evaluation>();
    public DbSet<EvaluationScore>     EvaluationScores     => Set<EvaluationScore>();
    public DbSet<ActivityCriterion>   ActivityCriteria     => Set<ActivityCriterion>();
    public DbSet<ProfessorNote>       ProfessorNotes       => Set<ProfessorNote>();
    public DbSet<ActivityTemplate>    ActivityTemplates    => Set<ActivityTemplate>();
    public DbSet<ActivityLog>         ActivityLogs         => Set<ActivityLog>();
    public DbSet<ProfessorLogin>      ProfessorLogins      => Set<ProfessorLogin>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Professor>(e => {
            e.HasIndex(p => p.Email).IsUnique();
            e.Property(p => p.Email).HasMaxLength(200);
            e.Property(p => p.Nom).HasMaxLength(100);
            e.Property(p => p.Cognoms).HasMaxLength(200);
            e.Ignore(p => p.NomComplet);
        });

        b.Entity<Class>(e => {
            e.Property(c => c.Name).HasMaxLength(200);
        });

        b.Entity<Student>(e => {
            e.HasOne(s => s.Class)
             .WithMany(c => c.Students)
             .HasForeignKey(s => s.ClassId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => s.Email).IsUnique();
            e.Property(s => s.Email).HasMaxLength(200);
            e.Property(s => s.Nom).HasMaxLength(100);
            e.Property(s => s.Cognoms).HasMaxLength(200);
            e.Property(s => s.PasswordHash).HasMaxLength(100);
            e.Property(s => s.PlainPasswordEncrypted).HasMaxLength(500);
            e.Ignore(s => s.NomComplet);
        });

        b.Entity<Module>(e => {
            e.HasOne(m => m.Class)
             .WithMany(c => c.Modules)
             .HasForeignKey(m => m.ClassId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Professor)
             .WithMany()
             .HasForeignKey(m => m.ProfessorId)
             .OnDelete(DeleteBehavior.Restrict);
            e.Property(m => m.Code).HasMaxLength(4);
            e.Property(m => m.Name).HasMaxLength(200);
            e.HasIndex(m => new { m.ClassId, m.Code }).IsUnique();
        });

        b.Entity<ModuleExclusion>(e => {
            e.HasOne(me => me.Module)
             .WithMany(m => m.Exclusions)
             .HasForeignKey(me => me.ModuleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(me => me.Student)
             .WithMany(s => s.Exclusions)
             .HasForeignKey(me => me.StudentId)
             .OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(me => new { me.ModuleId, me.StudentId }).IsUnique();
        });

        b.Entity<Activity>(e => {
            e.HasOne(a => a.Module)
             .WithMany(m => m.Activities)
             .HasForeignKey(a => a.ModuleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(a => a.Name).HasMaxLength(300);
        });

        b.Entity<Group>(e => {
            e.HasOne(g => g.Activity)
             .WithMany(a => a.Groups)
             .HasForeignKey(g => g.ActivityId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(g => g.Name).HasMaxLength(100);
        });

        b.Entity<GroupMember>(e => {
            e.HasOne(gm => gm.Group)
             .WithMany(g => g.Members)
             .HasForeignKey(gm => gm.GroupId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(gm => gm.Student)
             .WithMany(s => s.GroupMemberships)
             .HasForeignKey(gm => gm.StudentId)
             .OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(gm => new { gm.GroupId, gm.StudentId }).IsUnique();
        });

        b.Entity<Evaluation>(e => {
            e.HasOne(ev => ev.Activity).WithMany()
             .HasForeignKey(ev => ev.ActivityId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ev => ev.Evaluator).WithMany()
             .HasForeignKey(ev => ev.EvaluatorId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(ev => ev.Evaluated).WithMany()
             .HasForeignKey(ev => ev.EvaluatedId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(ev => new { ev.ActivityId, ev.EvaluatorId, ev.EvaluatedId }).IsUnique();
        });

        b.Entity<EvaluationScore>(e => {
            e.HasOne(es => es.Evaluation)
             .WithMany(ev => ev.Scores)
             .HasForeignKey(es => es.EvaluationId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(es => new { es.EvaluationId, es.CriteriaKey }).IsUnique();
            e.Property(es => es.CriteriaKey).HasMaxLength(50);
        });

        b.Entity<ActivityCriterion>(e => {
            e.HasOne(ac => ac.Activity)
             .WithMany(a => a.Criteria)
             .HasForeignKey(ac => ac.ActivityId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(ac => new { ac.ActivityId, ac.Key }).IsUnique();
            e.Property(ac => ac.Key).HasMaxLength(50);
            e.Property(ac => ac.Label).HasMaxLength(200);
        });

        b.Entity<ProfessorNote>(e => {
            e.HasOne(n => n.Activity)
             .WithMany()
             .HasForeignKey(n => n.ActivityId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(n => n.Student)
             .WithMany()
             .HasForeignKey(n => n.StudentId)
             .OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(n => new { n.ActivityId, n.StudentId }).IsUnique();
        });

        b.Entity<ActivityTemplate>(e => {
            e.Property(t => t.Name).HasMaxLength(300);
            e.HasIndex(t => t.ProfessorId);
        });

        b.Entity<ActivityLog>(e => {
            // Sense FK — el log es preserva si l'activitat s'esborra
            e.HasIndex(l => l.ActivityId);
            e.Property(l => l.Action).HasMaxLength(50);
            e.Property(l => l.ActivityName).HasMaxLength(300);
            e.Property(l => l.ActorName).HasMaxLength(300);
        });

        b.Entity<ProfessorLogin>(e => {
            e.HasOne<Professor>()
             .WithMany()
             .HasForeignKey(l => l.ProfessorId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(l => l.ProfessorId);
            e.HasIndex(l => l.CreatedAt);
        });
    }
}
