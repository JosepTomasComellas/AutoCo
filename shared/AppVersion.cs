namespace AutoCo.Shared;

/// <summary>Versió de l'aplicació AutoCo. Actualitzar en cada canvi significatiu.</summary>
public static class AppVersion
{
    public const string Current = "1.6.7";
    public const string Name    = "AutoCo Avaluació";

    /// <summary>Descripció del canvi per al changelog intern.</summary>
    public const string ChangeLog = "fix: crea ActivityCriteria/ActivityLogs/ProfessorNotes/ActivityTemplates si no existeixen (IF NOT EXISTS a l'arrencada); i18n parcial de la UI";
}
