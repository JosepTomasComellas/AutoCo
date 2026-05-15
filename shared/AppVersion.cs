namespace AutoCo.Shared;

/// <summary>Versió de l'aplicació AutoCo. Actualitzar en cada canvi significatiu.</summary>
public static class AppVersion
{
    public const string Current = "2.6.30";
    public const string Name    = "AutoCo Avaluació";

    /// <summary>Descripció del canvi per al changelog intern.</summary>
    public const string ChangeLog = "v2.6.30: Fix visibilitat i edició activitats per professors assignats — ActivityDto.CanEdit; revertida visibilitat a mòduls propis; _canEdit frontend via CanEdit";
}
