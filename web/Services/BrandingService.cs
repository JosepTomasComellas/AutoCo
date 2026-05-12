namespace AutoCo.Web.Services;

/// <summary>
/// Propietats de marca configurables sense recompilar (variables BRAND_* al .env).
/// El logo es configura posant un fitxer a ./config/branding/logo.png (muntat al volum).
/// </summary>
public sealed class BrandingService
{
    public string AppName      { get; }
    public string AppShortName { get; }
    public string OrgName      { get; }
    public string OrgDept      { get; }
    public string PrimaryColor { get; }
    public string NavColor     { get; }
    public string LogoUrl      { get; }

    public BrandingService(IConfiguration config, IWebHostEnvironment env)
    {
        AppName      = config["BRAND_APP_NAME"]       ?? "AutoCo Avaluació";
        AppShortName = config["BRAND_APP_SHORT_NAME"] ?? "AutoCo";
        OrgName      = config["BRAND_ORG_NAME"]       ?? "Salesians de Sarrià";
        OrgDept      = config["BRAND_ORG_DEPT"]       ?? "Dept. d'Informàtica";
        PrimaryColor = config["BRAND_PRIMARY_COLOR"]  ?? "#CC0000";
        NavColor     = config["BRAND_NAV_COLOR"]      ?? "#1e293b";

        var logoPath = Path.Combine(env.WebRootPath ?? "", "branding", "logo.png");
        LogoUrl = File.Exists(logoPath) ? "/branding/logo.png" : "/images/logo2.png";
    }
}
