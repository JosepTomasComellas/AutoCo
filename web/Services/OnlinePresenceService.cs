using StackExchange.Redis;
using System.Text.Json;

namespace AutoCo.Web.Services;

public record OnlineUserSnapshot(
    int Id, string DisplayName, string Role, string? PhotoUrl,
    int? ClassId, string? ClassName, long ConnectedAt,
    string? IpAddress = null, string? CircuitId = null);

public sealed class OnlinePresenceService(IConnectionMultiplexer redis) : IAsyncDisposable
{
    private readonly IDatabase   _db        = redis.GetDatabase();
    private readonly ISubscriber _sub       = redis.GetSubscriber();
    private readonly string      _circuitId = Guid.NewGuid().ToString("N")[..8];
    public  string CircuitId => _circuitId;
    private Timer?  _timer;
    private string? _key;

    public event Func<Task>? OnKicked;

    public void Start(int id, string displayName, string role, string? photoUrl,
                      int? classId = null, string? className = null, string? ipAddress = null)
    {
        if (_timer is not null) return;
        var prefix = role == "Student" ? "stu" : "prof";
        _key = $"autoco:online:{prefix}:{id}:{_circuitId}";
        var json = JsonSerializer.Serialize(new OnlineUserSnapshot(
            id, displayName, role, photoUrl, classId, className,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), ipAddress, _circuitId));
        _ = _db.StringSetAsync(_key, json, TimeSpan.FromSeconds(30));
        _timer = new Timer(
            state => { _ = _db.StringSetAsync(_key!, json, TimeSpan.FromSeconds(30)); },
            null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        _sub.Subscribe(RedisChannel.Literal($"autoco:kick:{_circuitId}"), (_, _) =>
        {
            _ = HandleKickAsync();
        });
    }

    private async Task HandleKickAsync()
    {
        await StopAsync();
        if (OnKicked is not null) await OnKicked.Invoke();
    }

    public async Task StopAsync()
    {
        if (_timer is not null) { await _timer.DisposeAsync(); _timer = null; }
        if (_key  is not null) { _ = _db.KeyDeleteAsync(_key); _key = null; }
        await _sub.UnsubscribeAsync(RedisChannel.Literal($"autoco:kick:{_circuitId}"));
    }

    public async ValueTask DisposeAsync()
    {
        if (_timer is not null) await _timer.DisposeAsync();
        await _sub.UnsubscribeAsync(RedisChannel.Literal($"autoco:kick:{_circuitId}"));
    }
}
