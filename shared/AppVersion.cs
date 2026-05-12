namespace AutoCo.Shared;

/// <summary>Versió de l'aplicació AutoCo. Actualitzar en cada canvi significatiu.</summary>
public static class AppVersion
{
    public const string Current = "2.6.4";
    public const string Name    = "AutoCo Avaluació";

    /// <summary>Descripció del canvi per al changelog intern.</summary>
    public const string ChangeLog = "v2.6.4: tests cobertura serveis nous (ArchiveAsync ×6, GetAllAsync includeArchived ×3, DefaultCriteria ×7) → total 55 tests";
}
