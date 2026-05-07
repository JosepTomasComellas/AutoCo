using AutoCo.Shared.DTOs;
using StackExchange.Redis;
using System.Text.Json;

namespace AutoCo.Web.Services;

/// <summary>
/// Servei d'allotjament que escolta el canal Redis "autoco:notif:{professorId}"
/// publicat per l'API quan s'afegeix una notificació in-app.
/// Reenvía cada missatge als components Blazor via NotificationBroadcastService.
/// </summary>
public class NotificationRedisSubscriber(
    IConnectionMultiplexer redis,
    NotificationBroadcastService notifier,
    ILogger<NotificationRedisSubscriber> logger) : IHostedService
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private ISubscriber? _sub;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _sub = redis.GetSubscriber();
        await _sub.SubscribeAsync(
            RedisChannel.Pattern("autoco:notif:*"),
            async (channel, message) =>
            {
                if (message.IsNullOrEmpty) return;
                try
                {
                    var dto = JsonSerializer.Deserialize<NotificationDto>((string)message!, _json);
                    if (dto is not null)
                        await notifier.NotifyAsync(dto.ProfessorId, dto);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error processant notificació del canal {Channel}", channel);
                }
            });
        logger.LogInformation("NotificationRedisSubscriber: escoltant autoco:notif:*");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_sub is not null)
            await _sub.UnsubscribeAsync(RedisChannel.Pattern("autoco:notif:*"));
    }
}
