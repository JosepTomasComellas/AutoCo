using AutoCo.Api.Data.Models;
using AutoCo.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AutoCo.Api.Data;

/// <summary>Obté els criteris efectius d'una activitat (customs si en té, globals si no).</summary>
public static class CriteriaHelper
{
    public static async Task<List<(string Key, string Label, int Weight)>> GetForActivityAsync(
        AppDbContext db, int activityId)
    {
        var custom = await db.ActivityCriteria
            .Where(ac => ac.ActivityId == activityId)
            .OrderBy(ac => ac.OrderIndex)
            .ToListAsync();

        return custom.Any()
            ? custom.Select(c => (c.Key, c.Label, c.Weight)).ToList()
            : Criteria.All.Select(c => (c.Key, c.Label, 1)).ToList();
    }

    public static async Task<List<ActivityCriterionDto>> GetDtosAsync(
        AppDbContext db, int activityId)
    {
        var custom = await db.ActivityCriteria
            .Where(ac => ac.ActivityId == activityId)
            .OrderBy(ac => ac.OrderIndex)
            .ToListAsync();

        if (custom.Any())
            return custom.Select(c => new ActivityCriterionDto(c.Id, c.Key, c.Label, c.OrderIndex, c.Weight)).ToList();

        return Criteria.All.Select((c, i) => new ActivityCriterionDto(0, c.Key, c.Label, i, 1)).ToList();
    }
}
