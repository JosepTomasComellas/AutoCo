namespace AutoCo.Web.Services;

/// <summary>
/// Propietats de marca configurables sense recompilar (variables BRAND_* al .env).
/// Logo: ./config/branding/logo.png — Fons: ./config/branding/background.png (o .jpg)
/// </summary>
public sealed class BrandingService
{
    public string AppName         { get; }
    public string AppShortName    { get; }
    public string OrgName         { get; }
    public string OrgDept         { get; }
    public string PrimaryColor    { get; }
    public string PrimaryColorDark { get; }
    public string NavColor        { get; }
    public string NavColorDark    { get; }
    public string LogoUrl         { get; }
    /// <summary>Valor CSS per a background-image (url sense cometes).</summary>
    public string BgImageCssValue { get; }

    public BrandingService(IConfiguration config, IWebHostEnvironment env)
    {
        AppName      = config["BRAND_APP_NAME"]       ?? "AutoCo Avaluació";
        AppShortName = config["BRAND_APP_SHORT_NAME"] ?? "AutoCo";
        OrgName      = config["BRAND_ORG_NAME"]       ?? "Salesians de Sarrià";
        OrgDept      = config["BRAND_ORG_DEPT"]       ?? "Dept. d'Informàtica";
        PrimaryColor = config["BRAND_PRIMARY_COLOR"]  ?? "#CC0000";
        NavColor     = config["BRAND_NAV_COLOR"]      ?? "#1e293b";

        PrimaryColorDark = DarkenHex(PrimaryColor, 0.15);
        NavColorDark     = DarkenHex(NavColor, 0.35);

        var root = env.WebRootPath ?? "";
        LogoUrl = File.Exists(Path.Combine(root, "branding", "logo.png"))
            ? "/branding/logo.png"
            : "/images/logo2.png";

        BgImageCssValue = File.Exists(Path.Combine(root, "branding", "background.png")) ? "url(/branding/background.png)"
                        : File.Exists(Path.Combine(root, "branding", "background.jpg")) ? "url(/branding/background.jpg)"
                        : "url(/images/fons-salesians.png)";
    }

    public static string DarkenHex(string hex, double amount)
    {
        try
        {
            hex = hex.TrimStart('#');
            var r = (int)(Convert.ToInt32(hex[..2], 16) * (1 - amount));
            var g = (int)(Convert.ToInt32(hex[2..4], 16) * (1 - amount));
            var b = (int)(Convert.ToInt32(hex[4..6], 16) * (1 - amount));
            return $"#{r:X2}{g:X2}{b:X2}";
        }
        catch { return '#' + hex; }
    }
}
