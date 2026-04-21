using AutoCo.Shared.DTOs;
using StackExchange.Redis;
using System.Text.Json;

namespace AutoCo.Web.Services;

/// <summary>
/// Servei d'allotjament que escolta el canal Redis "autoco:participation:*"
/// publicat per l'API quan un alumne envia una avaluació.
/// Cada missatge es reenvía als components Blazor via ParticipationNotificationService.
/// </summary>
public class ParticipationRedisSubscriber(
    IConnectionMultiplexer redis,
    ParticipationNotificationService notifier,
    ILogger<ParticipationRedisSubscriber> logger) : IHostedService
{
    private ISubscriber? _sub;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _sub = redis.GetSubscriber();

        await _sub.SubscribeAsync(
            RedisChannel.Pattern("autoco:participation:*"),
            async (channel, message) =>
            {
                if (message.IsNullOrEmpty) return;
                try
                {
                    var dto = JsonSerializer.Deserialize<ParticipationDto>(message!,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (dto is not null)
                        await notifier.NotifyAsync(dto.ActivityId, dto);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error processant missatge de participació del canal {Channel}", channel);
                }
            });

        logger.LogInformation("ParticipationRedisSubscriber: escoltant autoco:participation:*");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_sub is not null)
            await _sub.UnsubscribeAsync(RedisChannel.Pattern("autoco:participation:*"));
    }
}
