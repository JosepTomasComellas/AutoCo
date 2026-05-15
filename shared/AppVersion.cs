namespace AutoCo.Shared;

/// <summary>Versió de l'aplicació AutoCo. Actualitzar en cada canvi significatiu.</summary>
public static class AppVersion
{
    public const string Current = "2.6.31";
    public const string Name    = "AutoCo Avaluació";

    /// <summary>Descripció del canvi per al changelog intern.</summary>
    public const string ChangeLog = "v2.6.31: CreatedByProfessorId — professors assignats veuen les seves activitats al tauler; DuplicateAsync/DuplicateCrossAsync també assignen; fix startup SQL Server race + Redis shutdown";
}
