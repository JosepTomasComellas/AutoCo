namespace AutoCo.Shared;

/// <summary>Versió de l'aplicació AutoCo. Actualitzar en cada canvi significatiu.</summary>
public static class AppVersion
{
    public const string Current = "1.6.5";
    public const string Name    = "AutoCo Avaluació";

    /// <summary>Descripció del canvi per al changelog intern.</summary>
    public const string ChangeLog = "i18n: fix selector idioma (async/await InvokeVoidAsync); fix recursos localitzador (registre explícit)";
}
