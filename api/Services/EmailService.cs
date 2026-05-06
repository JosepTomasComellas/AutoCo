using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AutoCo.Api.Services;

public interface IEmailService
{
    bool IsEnabled { get; }
    Task<bool> SendStudentPasswordAsync(string toEmail, string toName, string className, string password);
    Task<bool> SendProfessorCredentialsAsync(string toEmail, string toName, string password);
    Task<bool> SendReminderAsync(string toEmail, string toName, string activityName, string className);
    Task<bool> SendInvitationAsync(string toEmail, string toName, string activityName, string className, bool includePassword, string? password);
    Task<bool> SendActivityCompletedAsync(string toEmail, string toName, string activityName, string className, int total);
    Task<bool> SendPasswordResetAsync(string toEmail, string toName, string code);
}

public class EmailService(IConfiguration config, ILogger<EmailService> logger) : IEmailService
{
    private readonly string _host        = config["Smtp:Host"]        ?? "";
    private readonly int    _port        = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
    private readonly string _username    = config["Smtp:Username"]    ?? "";
    private readonly string _password    = config["Smtp:Password"]    ?? "";
    private readonly string _fromAddress = config["Smtp:FromAddress"] ?? "";
    private readonly string _fromName    = config["Smtp:FromName"]    ?? "Salesians de Sarrià";
    private readonly string _webUrl      = config["App:WebUrl"]       ?? "";

    private static readonly string _logoPath =
        Path.Combine(AppContext.BaseDirectory, "resources", "logo2.png");

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_host) && !string.IsNullOrWhiteSpace(_fromAddress);

    public async Task<bool> SendStudentPasswordAsync(string toEmail, string toName, string className, string password)
    {
        if (!IsEnabled) return false;
        var url = LoginUrl("alumne");
        var content = $"""
            <p style="margin:0 0 16px;font-size:16px;font-weight:600;color:#1e293b">Hola, {H(toName)}!</p>
            <p style="margin:0 0 20px;color:#475569">T'enviem les teves credencials d'accés al sistema d'avaluació.</p>
            {CredentialBlock([("Classe", H(className), false), ("Correu", H(toEmail), false), ("Contrasenya", H(password), true)])}
            <p style="margin:20px 0 0;color:#64748b;font-size:13px">Si tens qualsevol problema, contacta amb el teu professor/a.</p>
            """;
        var plain = $"""
            Hola, {toName}!

            T'enviem les teves credencials d'accés al sistema d'avaluació.

            Classe:      {className}
            Correu:      {toEmail}
            Contrasenya: {password}

            Accedeix aquí: {url}

            Departament d'Informàtica · Salesians de Sarrià
            """;
        return await SendAsync(toEmail, toName, $"Credencials d'accés – {className}",
            WrapHtml(content, url, "Accedeix ara"), plain);
    }

    public async Task<bool> SendProfessorCredentialsAsync(string toEmail, string toName, string password)
    {
        if (!IsEnabled) return false;
        var url = LoginUrl("professor");
        var content = $"""
            <p style="margin:0 0 16px;font-size:16px;font-weight:600;color:#1e293b">Hola, {H(toName)}!</p>
            <p style="margin:0 0 20px;color:#475569">T'enviem les teves credencials d'accés al sistema d'avaluació.</p>
            {CredentialBlock([("Correu", H(toEmail), false), ("Contrasenya", H(password), true)])}
            <p style="margin:20px 0 0;color:#64748b;font-size:13px">Es recomana canviar la contrasenya després del primer accés.</p>
            """;
        var plain = $"""
            Hola, {toName}!

            T'enviem les teves credencials d'accés al sistema d'avaluació.

            Correu:      {toEmail}
            Contrasenya: {password}

            Accedeix aquí: {url}

            Es recomana canviar la contrasenya després del primer accés.

            Departament d'Informàtica · Salesians de Sarrià
            """;
        return await SendAsync(toEmail, toName, "Credencials d'accés – AutoCo",
            WrapHtml(content, url, "Accedeix ara"), plain);
    }

    public async Task<bool> SendReminderAsync(string toEmail, string toName, string activityName, string className)
    {
        if (!IsEnabled) return false;
        var url = LoginUrl("alumne");
        var content = $"""
            <p style="margin:0 0 16px;font-size:16px;font-weight:600;color:#1e293b">Hola, {H(toName)}!</p>
            <p style="margin:0 0 8px;color:#475569">Tens pendent l'avaluació de l'activitat:</p>
            <table cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:16px 0;background:#f8fafc;border-radius:8px;border:1px solid #e2e8f0">
              <tr><td style="padding:16px 20px">
                <span style="display:block;font-size:15px;font-weight:700;color:#1e293b">{H(activityName)}</span>
                <span style="display:block;font-size:13px;color:#64748b;margin-top:4px">{H(className)}</span>
              </td></tr>
            </table>
            <p style="margin:0;color:#64748b;font-size:13px">Si ja has completat l'avaluació, ignora aquest missatge.</p>
            """;
        var plain = $"""
            Hola, {toName}!

            Tens pendent l'avaluació de l'activitat «{activityName}» a la classe {className}.

            Accedeix aquí: {url}

            Si ja has completat l'avaluació, ignora aquest missatge.

            Departament d'Informàtica · Salesians de Sarrià
            """;
        return await SendAsync(toEmail, toName, $"Recordatori: avaluació pendent – {activityName}",
            WrapHtml(content, url, "Avalua ara"), plain);
    }

    public async Task<bool> SendInvitationAsync(string toEmail, string toName,
        string activityName, string className, bool includePassword, string? password)
    {
        if (!IsEnabled) return false;
        var url = LoginUrl("alumne");
        var credBlock = includePassword && password is not null
            ? CredentialBlock([("Correu", H(toEmail), false), ("Contrasenya", H(password), true)])
            : "";
        var content = $"""
            <p style="margin:0 0 16px;font-size:16px;font-weight:600;color:#1e293b">Hola, {H(toName)}!</p>
            <p style="margin:0 0 8px;color:#475569">El professor/a us convida a participar en l'avaluació de l'activitat:</p>
            <table cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:16px 0;background:#f8fafc;border-radius:8px;border:1px solid #e2e8f0">
              <tr><td style="padding:16px 20px">
                <span style="display:block;font-size:15px;font-weight:700;color:#1e293b">{H(activityName)}</span>
                <span style="display:block;font-size:13px;color:#64748b;margin-top:4px">{H(className)}</span>
              </td></tr>
            </table>
            {credBlock}
            <p style="margin:0;color:#64748b;font-size:13px">Si tens qualsevol problema, contacta amb el teu professor/a.</p>
            """;
        var pwdLine = includePassword && password is not null
            ? $"\nCorreu:      {toEmail}\nContrasenya: {password}\n" : "";
        var plain = $"""
            Hola, {toName}!

            El professor/a us convida a participar en l'avaluació de «{activityName}» ({className}).
            {pwdLine}
            Accedeix aquí: {url}

            Si tens qualsevol problema, contacta amb el teu professor/a.

            Departament d'Informàtica · Salesians de Sarrià
            """;
        return await SendAsync(toEmail, toName, $"Convit: avaluació «{activityName}»",
            WrapHtml(content, url, "Accedeix ara"), plain);
    }

    public async Task<bool> SendActivityCompletedAsync(string toEmail, string toName,
        string activityName, string className, int total)
    {
        if (!IsEnabled) return false;
        var url = string.IsNullOrWhiteSpace(_webUrl) ? "#" : $"{_webUrl}/professor/resultats";
        var content = $"""
            <p style="margin:0 0 16px;font-size:16px;font-weight:600;color:#1e293b">Hola, {H(toName)}!</p>
            <p style="margin:0 0 8px;color:#475569">Tots els alumnes han completat l'avaluació de l'activitat:</p>
            <table cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:16px 0;background:#f8fafc;border-radius:8px;border:1px solid #e2e8f0">
              <tr><td style="padding:16px 20px">
                <span style="display:block;font-size:15px;font-weight:700;color:#1e293b">{H(activityName)}</span>
                <span style="display:block;font-size:13px;color:#64748b;margin-top:4px">{H(className)}</span>
                <span style="display:inline-block;margin-top:10px;padding:3px 10px;background:#dcfce7;color:#166534;border-radius:20px;font-size:12px;font-weight:600">{total} avaluacions completades</span>
              </td></tr>
            </table>
            """;
        var plain = $"""
            Hola, {toName}!

            Tots els {total} alumnes de l'activitat «{activityName}» ({className}) han completat la seva avaluació.

            Consulta els resultats: {url}

            Departament d'Informàtica · Salesians de Sarrià
            """;
        return await SendAsync(toEmail, toName, $"Avaluació completada – {activityName}",
            WrapHtml(content, url, "Consulta els resultats"), plain);
    }

    public async Task<bool> SendPasswordResetAsync(string toEmail, string toName, string code)
    {
        if (!IsEnabled) return false;
        var content = $"""
            <p style="margin:0 0 16px;font-size:16px;font-weight:600;color:#1e293b">Hola, {H(toName)}!</p>
            <p style="margin:0 0 20px;color:#475569">Has sol·licitat restablir la teva contrasenya d'AutoCo.</p>
            <table cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:8px 0 20px">
              <tr>
                <td align="center" style="background:#f8fafc;border-radius:8px;border:1px solid #e2e8f0;padding:24px 20px">
                  <span style="display:block;font-size:11px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:1px;margin-bottom:12px">Codi de verificació</span>
                  <span style="display:block;font-size:34px;font-weight:700;letter-spacing:10px;color:#1e293b;font-family:monospace,monospace">{H(code)}</span>
                  <span style="display:block;font-size:12px;color:#94a3b8;margin-top:12px">Vàlid durant 15 minuts</span>
                </td>
              </tr>
            </table>
            <p style="margin:0;color:#64748b;font-size:13px">Si no has sol·licitat aquest canvi, ignora aquest missatge.</p>
            """;
        var plain = $"""
            Hola, {toName}!

            Has sol·licitat restablir la teva contrasenya d'AutoCo.

            Codi de verificació: {code}

            Aquest codi és vàlid durant 15 minuts.
            Si no has sol·licitat aquest canvi, ignora aquest missatge.

            Departament d'Informàtica · Salesians de Sarrià
            """;
        return await SendAsync(toEmail, toName, "Restabliment de contrasenya – AutoCo",
            WrapHtml(content), plain);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string H(string s) => WebUtility.HtmlEncode(s);

    private string LoginUrl(string role) =>
        string.IsNullOrWhiteSpace(_webUrl) ? "#" : $"{_webUrl}/auth/login-{role}";

    private static string CredentialBlock((string Label, string Value, bool Highlight)[] rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("""
            <table cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:16px 0;background:#f8fafc;border-radius:8px;border:1px solid #e2e8f0">
              <tr><td style="padding:16px 20px">
                <span style="display:block;font-size:11px;font-weight:700;color:#94a3b8;text-transform:uppercase;letter-spacing:1px;margin-bottom:12px">Dades d'accés</span>
                <table cellpadding="0" cellspacing="0" border="0" width="100%">
            """);
        foreach (var (label, value, highlight) in rows)
        {
            var valStyle = highlight
                ? "color:#CC0000;font-weight:700;font-family:monospace,monospace;font-size:15px"
                : "color:#1e293b;font-weight:600";
            sb.Append($"""
                  <tr>
                    <td style="color:#64748b;font-size:13px;padding:4px 0;width:110px;vertical-align:top">{label}</td>
                    <td style="{valStyle};font-size:14px;padding:4px 0">{value}</td>
                  </tr>
                """);
        }
        sb.Append("</table></td></tr></table>");
        return sb.ToString();
    }

    private static string WrapHtml(string content, string? buttonUrl = null, string? buttonText = null)
    {
        var btn = buttonUrl is not null && buttonUrl != "#" ? $"""
            <table cellpadding="0" cellspacing="0" border="0" width="100%" style="margin-top:28px">
              <tr>
                <td align="center">
                  <a href="{buttonUrl}" style="display:inline-block;padding:12px 36px;background:#CC0000;color:#ffffff;text-decoration:none;border-radius:6px;font-size:15px;font-weight:600;letter-spacing:.2px">{buttonText}</a>
                </td>
              </tr>
            </table>
            """ : "";

        return $"""
            <!DOCTYPE html>
            <html lang="ca">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
            </head>
            <body style="margin:0;padding:0;background:#f1f5f9;font-family:'Segoe UI',Arial,sans-serif">
              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                <tr>
                  <td align="center" style="padding:40px 16px">
                    <table width="100%" style="max-width:480px;border-radius:12px;overflow:hidden;background:#ffffff;box-shadow:0 4px 24px rgba(0,0,0,.10)">
                      <tr>
                        <td style="background:#1e293b;padding:20px 32px">
                          <img src="cid:autoco-logo" alt="Salesians Sarrià" height="42" style="display:block;border:0;margin-bottom:12px" />
                          <span style="display:block;font-size:20px;font-weight:700;color:#ffffff;letter-spacing:-.3px">AutoCo</span>
                          <span style="display:block;font-size:12px;color:rgba(255,255,255,.5);margin-top:3px">Avaluació entre iguals</span>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:28px 32px 32px;color:#1e293b;font-size:15px;line-height:1.65">
                          {content}
                          {btn}
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:14px 32px;border-top:1px solid #e2e8f0;background:#f8fafc;text-align:center">
                          <span style="color:#94a3b8;font-size:12px">Departament d'Informàtica · Salesians de Sarrià</span>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private async Task<bool> SendAsync(string toEmail, string toName, string subject, string html, string plain)
    {
        try
        {
            var builder = new BodyBuilder { TextBody = plain };

            if (File.Exists(_logoPath))
            {
                var logo = await builder.LinkedResources.AddAsync(_logoPath);
                logo.ContentId = "autoco-logo";
                logo.ContentDisposition = new MimeKit.ContentDisposition(MimeKit.ContentDisposition.Inline);
            }

            builder.HtmlBody = html;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_fromName, _fromAddress));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body    = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_host, _port, SecureSocketOptions.StartTls);
            if (!string.IsNullOrWhiteSpace(_username))
                await client.AuthenticateAsync(_username, _password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enviant correu a {Email}", toEmail);
            return false;
        }
    }
}
