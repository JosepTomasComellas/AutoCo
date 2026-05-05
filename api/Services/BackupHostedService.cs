namespace AutoCo.Api.Services;

public class BackupHostedService(
    IServiceProvider sp,
    IConfiguration   cfg,
    ILogger<BackupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var enabled = cfg.GetValue("Backup:Enabled", false);
        if (!enabled)
        {
            logger.LogInformation("Backup automàtic desactivat (Backup:Enabled=false)");
            return;
        }

        var dailyHour       = cfg.GetValue("Backup:DailyHour",      2);
        var weeklyDay       = cfg.GetValue("Backup:WeeklyDay",       0); // 0 = Sunday
        var dailyRetention  = cfg.GetValue("Backup:DailyRetention",  7);
        var weeklyRetention = cfg.GetValue("Backup:WeeklyRetention", 4);

        logger.LogInformation(
            "Backup automàtic activat — hora diària: {Hour}h UTC, dia setmanal: {Day}, " +
            "retenció: {Daily} diaris / {Weekly} setmanals",
            dailyHour, (DayOfWeek)weeklyDay, dailyRetention, weeklyRetention);

        var lastDailyDate  = DateOnly.MinValue;
        var lastWeeklyDate = DateOnly.MinValue;

        // Alinear al proper minut en punt per evitar deriva
        var now = DateTime.UtcNow;
        var delay = TimeSpan.FromSeconds(60 - now.Second);
        await Task.Delay(delay, ct);

        while (!ct.IsCancellationRequested)
        {
            now = DateTime.UtcNow;
            if (now.Hour == dailyHour && now.Minute == 0)
            {
                var today = DateOnly.FromDateTime(now);

                if (today > lastDailyDate)
                {
                    lastDailyDate = today;
                    await RunBackupAsync("daily", dailyRetention, ct);
                }

                if ((int)now.DayOfWeek == weeklyDay && today > lastWeeklyDate)
                {
                    lastWeeklyDate = today;
                    await RunBackupAsync("weekly", weeklyRetention, ct);
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }

    private async Task RunBackupAsync(string type, int retention, CancellationToken ct)
    {
        try
        {
            using var scope = sp.CreateScope();
            var svc  = scope.ServiceProvider.GetRequiredService<IBackupService>();
            var info = await svc.CreateAutoBackupAsync(type);
            await svc.ApplyRetentionAsync(type, retention);
            logger.LogInformation(
                "Backup automàtic {Type} completat: {Name} ({Bytes:N0} bytes)",
                type, info.Name, info.SizeBytes);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en el backup automàtic {Type}", type);
        }
    }
}
