using AutoCo.Api.Data;
using AutoCo.Api.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCo.Api.Services;

public class ActivitySchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<ActivitySchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            try   { await CheckSchedulesAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { logger.LogError(ex, "Error en el programador d'activitats"); }
        }
    }

    private async Task CheckSchedulesAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var changed = false;

        var toOpen = await db.Activities
            .Where(a => !a.IsOpen && a.OpenAt != null && a.OpenAt <= now)
            .ToListAsync();

        foreach (var a in toOpen)
        {
            a.IsOpen = true;
            a.OpenAt = null;
            db.ActivityLogs.Add(new ActivityLog
            {
                ActivityId   = a.Id,
                ActivityName = a.Name,
                Action       = "opened",
                Details      = "Obertura programada automàtica",
                CreatedAt    = now
            });
            logger.LogInformation("Activitat {Id} '{Name}' oberta automàticament", a.Id, a.Name);
            changed = true;
        }

        var toClose = await db.Activities
            .Where(a => a.IsOpen && a.CloseAt != null && a.CloseAt <= now)
            .ToListAsync();

        foreach (var a in toClose)
        {
            a.IsOpen  = false;
            a.CloseAt = null;
            db.ActivityLogs.Add(new ActivityLog
            {
                ActivityId   = a.Id,
                ActivityName = a.Name,
                Action       = "closed",
                Details      = "Tancament programat automàtic",
                CreatedAt    = now
            });
            logger.LogInformation("Activitat {Id} '{Name}' tancada automàticament", a.Id, a.Name);
            changed = true;
        }

        if (changed) await db.SaveChangesAsync();
    }
}
