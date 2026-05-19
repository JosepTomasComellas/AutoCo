namespace AutoCo.Shared;

/// <summary>Versió de l'aplicació AutoCo. Actualitzar en cada canvi significatiu.</summary>
public static class AppVersion
{
    public const string Current = "2.6.33";
    public const string Name    = "AutoCo Avaluació";

    /// <summary>Descripció del canvi per al changelog intern.</summary>
    public const string ChangeLog = "v2.6.33: Timeout sessió 1h d'inactivitat (JWT_EXPIRY_HOURS + JWT_REFRESH_EXPIRY_HOURS configurables); visibilitat activitats a professors assignats via ProfessorClass";
}
