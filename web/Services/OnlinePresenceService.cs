using StackExchange.Redis;
using System.Text.Json;

namespace AutoCo.Web.Services;

public record OnlineUserSnapshot(
    int Id, string DisplayName, string Role, string? PhotoUrl,
    int? ClassId, string? ClassName, long ConnectedAt);

public sealed class OnlinePresenceService(IConnectionMultiplexer redis) : IAsyncDisposable
{
    private readonly IDatabase _db = redis.GetDatabase();
    private Timer?   _timer;
    private string?  _key;

    public void Start(int id, string displayName, string role, string? photoUrl,
                      int? classId = null, string? className = null)
    {
        if (_timer is not null) return;
        var prefix = role == "Student" ? "stu" : "prof";
        _key = $"autoco:online:{prefix}:{id}";
        var json = JsonSerializer.Serialize(new OnlineUserSnapshot(
            id, displayName, role, photoUrl, classId, className,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        _ = _db.StringSetAsync(_key, json, TimeSpan.FromSeconds(30));
        _timer = new Timer(
            state => { _ = _db.StringSetAsync(_key!, json, TimeSpan.FromSeconds(30)); },
            null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    public async Task StopAsync()
    {
        if (_timer is not null) { await _timer.DisposeAsync(); _timer = null; }
        if (_key  is not null) { _ = _db.KeyDeleteAsync(_key); _key = null; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_timer is not null) await _timer.DisposeAsync();
    }
}
