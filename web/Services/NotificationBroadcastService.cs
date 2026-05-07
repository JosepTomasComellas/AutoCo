using AutoCo.Shared.DTOs;

namespace AutoCo.Web.Services;

/// <summary>
/// Bus intern de notificacions in-app. Els components Blazor s'hi subscriuen
/// per rebre alertes en temps real via el NotificationRedisSubscriber.
/// </summary>
public class NotificationBroadcastService
{
    private readonly List<(int ProfessorId, Func<NotificationDto, Task> Handler)> _handlers = [];
    private readonly object _lock = new();

    public void Subscribe(int professorId, Func<NotificationDto, Task> handler)
    {
        lock (_lock) _handlers.Add((professorId, handler));
    }

    public void Unsubscribe(int professorId, Func<NotificationDto, Task> handler)
    {
        lock (_lock) _handlers.RemoveAll(x => x.ProfessorId == professorId && x.Handler == handler);
    }

    public async Task NotifyAsync(int professorId, NotificationDto dto)
    {
        List<Func<NotificationDto, Task>> snapshot;
        lock (_lock)
            snapshot = _handlers.Where(x => x.ProfessorId == professorId).Select(x => x.Handler).ToList();

        await Task.WhenAll(snapshot.Select(h => InvokeSafe(h, dto)));
    }

    private static async Task InvokeSafe(Func<NotificationDto, Task> h, NotificationDto dto)
    {
        try { await h(dto); }
        catch { }
    }
}
