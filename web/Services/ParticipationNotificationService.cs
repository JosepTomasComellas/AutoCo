using AutoCo.Shared.DTOs;
using System.Collections.Concurrent;

namespace AutoCo.Web.Services;

/// <summary>
/// Servei singleton que actua de bus intern de notificacions de participació.
/// Els components Blazor (ActivityCard) s'hi subscriuen i reben actualitzacions
/// en temps real sense polling, gràcies al Redis subscriber.
/// </summary>
public class ParticipationNotificationService
{
    // activityId → llista de handlers dels components subscrits
    private readonly ConcurrentDictionary<int, List<Func<ParticipationDto, Task>>> _handlers = new();
    private readonly object _lock = new();

    public void Subscribe(int activityId, Func<ParticipationDto, Task> handler)
    {
        lock (_lock)
        {
            var list = _handlers.GetOrAdd(activityId, _ => []);
            list.Add(handler);
        }
    }

    public void Unsubscribe(int activityId, Func<ParticipationDto, Task> handler)
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(activityId, out var list))
                list.Remove(handler);
        }
    }

    public async Task NotifyAsync(int activityId, ParticipationDto dto)
    {
        List<Func<ParticipationDto, Task>> snapshot;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(activityId, out var list) || list.Count == 0) return;
            snapshot = [.. list]; // còpia per no mantenir el lock durant l'await
        }

        var tasks = snapshot.Select(h => InvokeSafe(h, dto));
        await Task.WhenAll(tasks);
    }

    private static async Task InvokeSafe(Func<ParticipationDto, Task> handler, ParticipationDto dto)
    {
        try { await handler(dto); }
        catch { /* el handler pot fallar si el circuit Blazor ja ha tancat */ }
    }
}
