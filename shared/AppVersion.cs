namespace AutoCo.Shared;

/// <summary>Versió de l'aplicació AutoCo. Actualitzar en cada canvi significatiu.</summary>
public static class AppVersion
{
    public const string Current = "1.6.2";
    public const string Name    = "AutoCo Avaluació";

    /// <summary>Descripció del canvi per al changelog intern.</summary>
    public const string ChangeLog = "Correccions de seguretat: IDOR CreateGroup/DeleteGroup/AddMember/RemoveMember, logging EvaluationService, N+1 ProfessorService, health check auth, límit criteris, BackupService error genèric";
}
