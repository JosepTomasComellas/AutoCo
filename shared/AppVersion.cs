namespace AutoCo.Shared;

/// <summary>Versió de l'aplicació AutoCo. Actualitzar en cada canvi significatiu.</summary>
public static class AppVersion
{
    public const string Current = "2.6.3";
    public const string Name    = "AutoCo Avaluació";

    /// <summary>Descripció del canvi per al changelog intern.</summary>
    public const string ChangeLog = "v2.6.3: migració MudBlazor v8→v9.4.0; ActivatorContent→CustomContent+OpenFilePickerAsync, ShowMessageBox→ShowMessageBoxAsync, ChartOptions→BarChartOptions; targetes dashboard en una sola fila";
}
