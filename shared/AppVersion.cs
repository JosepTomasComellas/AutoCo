namespace AutoCo.Shared;

/// <summary>Versió de l'aplicació AutoCo. Actualitzar en cada canvi significatiu.</summary>
public static class AppVersion
{
    public const string Current = "1.6.1";
    public const string Name    = "AutoCo Avaluació";

    /// <summary>Descripció del canvi per al changelog intern.</summary>
    public const string ChangeLog = "Correccions de seguretat: IDOR grups, N+1 passwords, límit importació CSV";
}
