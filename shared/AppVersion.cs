namespace AutoCo.Shared;

/// <summary>Versió de l'aplicació AutoCo. Actualitzar en cada canvi significatiu.</summary>
public static class AppVersion
{
    public const string Current = "2.6.35";
    public const string Name    = "AutoCo Avaluació";

    /// <summary>Descripció del canvi per al changelog intern.</summary>
    public const string ChangeLog = "v2.6.35: Fix importació backup ZIP — accept .json,.zip; ImportZipAsync a IBackupService; POST /api/admin/backup/import-zip; ImportZipBackupAsync a ApiClient; ZIP importat com a bytes bruts amb fotos incloses";
}
