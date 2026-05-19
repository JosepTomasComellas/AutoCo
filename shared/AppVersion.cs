namespace AutoCo.Shared;

/// <summary>Versió de l'aplicació AutoCo. Actualitzar en cada canvi significatiu.</summary>
public static class AppVersion
{
    public const string Current = "2.6.37";
    public const string Name    = "AutoCo Avaluació";

    /// <summary>Descripció del canvi per al changelog intern.</summary>
    public const string ChangeLog = "v2.6.37: Tancament sessió — docs CLAUDE.md i README actualitzats; v2.6.33–36: timeout sessió 1h, compartir activitats (ActivityShares), fix backup ZIP, fix Groups.OrderIndex EXEC(), antiforgery log None";
}
