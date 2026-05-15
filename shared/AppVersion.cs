namespace AutoCo.Shared;

/// <summary>Versió de l'aplicació AutoCo. Actualitzar en cada canvi significatiu.</summary>
public static class AppVersion
{
    public const string Current = "2.6.28";
    public const string Name    = "AutoCo Avaluació";

    /// <summary>Descripció del canvi per al changelog intern.</summary>
    public const string ChangeLog = "v2.6.28: Fix creació activitats per professors assignats via ProfessorClass; retry migració SQL Server; captura RedisConnectionException al shutdown";
}
