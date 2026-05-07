using AutoCo.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoCo.Api.Services;

public class ProfessorLoginsCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<ProfessorLoginsCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await WaitUntilNextRunAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try   { await CleanupAsync(); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Error en la neteja de logins de professors"); }

            await WaitUntilNextRunAsync(stoppingToken);
        }
    }

    private static async Task WaitUntilNextRunAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        int daysUntilSunday = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;
        // Si avui és diumenge i ja han passat les 03:00, esperem fins al proper diumenge
        if (daysUntilSunday == 0 && now.TimeOfDay >= TimeSpan.FromHours(3))
            daysUntilSunday = 7;
        var nextRun = now.Date.AddDays(daysUntilSunday).AddHours(3);
        var delay = nextRun - now;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, ct);
    }

    private async Task CleanupAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = DateTime.UtcNow.AddDays(-90);
        var deleted = await db.ProfessorLogins
            .Where(l => l.CreatedAt < cutoff)
            .ExecuteDeleteAsync();
        if (deleted > 0)
            logger.LogInformation("Neteja ProfessorLogins: {Count} registres eliminats (> 90 dies)", deleted);
    }
}
