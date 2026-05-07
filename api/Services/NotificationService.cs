using AutoCo.Shared.DTOs;
using StackExchange.Redis;
using System.Text.Json;

namespace AutoCo.Api.Services;

public interface INotificationService
{
    Task PushAsync(int professorId, string type, string message, string? href = null);
    Task<List<NotificationDto>> GetAsync(int professorId);
    Task ClearAsync(int professorId);
}

public class NotificationService(
    IConnectionMultiplexer redis,
    ILogger<NotificationService> logger) : INotificationService
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };
    private const int MaxNotifications = 20;
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(48);

    private static string ListKey(int pid)    => $"autoco:notif:{pid}";
    private static string ChannelKey(int pid) => $"autoco:notif:{pid}";

    public async Task PushAsync(int professorId, string type, string message, string? href = null)
    {
        try
        {
            var dto     = new NotificationDto(professorId, type, message, href, DateTime.UtcNow);
            var payload = JsonSerializer.Serialize(dto, _json);
            var db      = redis.GetDatabase();
            var key     = ListKey(professorId);

            await db.ListLeftPushAsync(key, payload);
            await db.ListTrimAsync(key, 0, MaxNotifications - 1);
            await db.KeyExpireAsync(key, Ttl);
            await redis.GetSubscriber().PublishAsync(
                RedisChannel.Literal(ChannelKey(professorId)), payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error publicant notificació al professor {Id}", professorId);
        }
    }

    public async Task<List<NotificationDto>> GetAsync(int professorId)
    {
        try
        {
            var items  = await redis.GetDatabase().ListRangeAsync(ListKey(professorId));
            var result = new List<NotificationDto>();
            foreach (var item in items)
            {
                if (item.IsNullOrEmpty) continue;
                try
                {
                    var dto = JsonSerializer.Deserialize<NotificationDto>((string)item!, _json);
                    if (dto is not null) result.Add(dto);
                }
                catch { }
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error llegint notificacions del professor {Id}", professorId);
            return [];
        }
    }

    public async Task ClearAsync(int professorId)
    {
        try { await redis.GetDatabase().KeyDeleteAsync(ListKey(professorId)); }
        catch (Exception ex) { logger.LogWarning(ex, "Error esborrant notificacions del professor {Id}", professorId); }
    }
}
