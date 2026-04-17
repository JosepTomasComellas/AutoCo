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
    public DbSet<Evaluation>       Evaluations       => Set<Evaluation>();
    public DbSet<EvaluationScore>  EvaluationScores  => Set<EvaluationScore>();

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
    }
}
