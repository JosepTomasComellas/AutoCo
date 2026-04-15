using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AutoCo.Api.Services;

public interface IEmailService
{
    bool IsEnabled { get; }
    Task<bool> SendPinAsync(string toEmail, string toName, string className, int studentId, string pin);
    Task<bool> SendProfessorCredentialsAsync(string toEmail, string toName, string username, string password);
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

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_host) && !string.IsNullOrWhiteSpace(_fromAddress);

    public async Task<bool> SendPinAsync(
        string toEmail, string toName, string className, int studentId, string pin)
    {
        if (!IsEnabled) return false;

        var accesUrl = string.IsNullOrWhiteSpace(_webUrl) ? "(URL no configurada)" : $"{_webUrl}/Auth/LoginAlumne";

        var body = $"""
            Hola, {toName}!

            T'enviem les teves credencials d'accés al sistema d'avaluació del
            Departament d'Informàtica de Salesians de Sarrià.

            ─────────────────────────────────────
             DADES D'ACCÉS
            ─────────────────────────────────────
             Nom complet:          {toName}
             Classe:               {className}
             Número d'alumne (ID): {studentId}
             PIN d'accés:          {pin}
            ─────────────────────────────────────

            Accedeix a la plataforma aquí:
            {accesUrl}

            Guarda bé aquestes dades. Si tens qualsevol problema, contacta amb
            el teu professor/a.

            ─────────────────────────────────────
            Departament d'Informàtica
            Salesians de Sarrià
            ─────────────────────────────────────
            """;

        return await SendAsync(toEmail, toName, $"Credencials d'accés – {className}", body);
    }

    public async Task<bool> SendProfessorCredentialsAsync(
        string toEmail, string toName, string username, string password)
    {
        if (!IsEnabled) return false;

        var accesUrl = string.IsNullOrWhiteSpace(_webUrl) ? "(URL no configurada)" : $"{_webUrl}/Auth/LoginProfessor";

        var body = $"""
            Hola, {toName}!

            T'enviem les teves credencials d'accés al sistema d'avaluació del
            Departament d'Informàtica de Salesians de Sarrià.

            ─────────────────────────────────────
             DADES D'ACCÉS
            ─────────────────────────────────────
             Nom complet:          {toName}
             Nom d'usuari:         {username}
             Contrasenya:          {password}
            ─────────────────────────────────────

            Accedeix a la plataforma aquí:
            {accesUrl}

            Es recomana canviar la contrasenya després del primer accés.
            Si tens qualsevol problema, contacta amb l'administrador del sistema.

            ─────────────────────────────────────
            Departament d'Informàtica
            Salesians de Sarrià
            ─────────────────────────────────────
            """;

        return await SendAsync(toEmail, toName, "Credencials d'accés – Plataforma d'Avaluació", body);
    }

    private async Task<bool> SendAsync(string toEmail, string toName, string subject, string body)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_fromName, _fromAddress));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body    = new TextPart("plain") { Text = body };

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
