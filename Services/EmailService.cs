using MailKit.Net.Smtp;
using MimeKit;

namespace FilmAPI.Services;

public class EmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetEmail(string toEmail, string token, string nome)
    {
        var link = BuildLink("reimposta-password.html", token, toEmail);
        var subject = "Recupero Password - CineBase";
        var body = $@"
<h2>Ciao {EscapeHtml(nome)},</h2>
<p>Hai richiesto il recupero della password per il tuo account CineBase.</p>
<p>Clicca il pulsante qui sotto per impostare una nuova password:</p>
<p><a href=""{link}"" style=""display:inline-block;padding:12px 24px;background:#dc2626;color:white;text-decoration:none;border-radius:6px;"">Imposta Nuova Password</a></p>
<p>Oppure copia e incolla questo link nel browser:</p>
<p>{link}</p>
<p><strong>Questo link scade tra 1 ora.</strong></p>
<p>Se non hai richiesto tu questa operazione, ignora questa email.</p>
<hr>
<p style=""color:#888;font-size:12px;"">Questa e un'email automatica, non rispondere. Se non hai richiesto questa azione, contatta l'amministratore.</p>";
        return await SendEmail(toEmail, subject, body);
    }

    public async Task<bool> SendPasswordSetupEmail(string toEmail, string token, string nome)
    {
        var link = BuildLink("setup-password.html", token, toEmail);
        var subject = "Imposta la tua Password - CineBase";
        var body = $@"
<h2>Ciao {EscapeHtml(nome)},</h2>
<p>Il tuo account CineBase non ha ancora una password impostata.</p>
<p>Clicca il pulsante qui sotto per impostare la tua password:</p>
<p><a href=""{link}"" style=""display:inline-block;padding:12px 24px;background:#dc2626;color:white;text-decoration:none;border-radius:6px;"">Imposta Password</a></p>
<p>Oppure copia e incolla questo link nel browser:</p>
<p>{link}</p>
<p><strong>Questo link scade tra 24 ore.</strong></p>
<hr>
<p style=""color:#888;font-size:12px;"">Questa e un'email automatica, non rispondere. Se non hai richiesto questa azione, contatta l'amministratore.</p>";
        return await SendEmail(toEmail, subject, body);
    }

    public async Task<bool> SendAdminInviteEmail(string toEmail, string token, string nome, string ruolo)
    {
        var link = BuildLink("setup-password.html", token, toEmail);
        var ruoloLabel = ruolo == "admin" ? "Amministratore" : "Operatore";
        var subject = $"Invito come {ruoloLabel} - CineBase";
        var body = $@"
<h2>Ciao {EscapeHtml(nome)},</h2>
<p>Sei stato invitato come <strong>{ruoloLabel}</strong> sulla piattaforma CineBase.</p>
<p>Clicca il pulsante qui sotto per impostare la tua password e attivare l'account:</p>
<p><a href=""{link}"" style=""display:inline-block;padding:12px 24px;background:#dc2626;color:white;text-decoration:none;border-radius:6px;"">Attiva Account</a></p>
<p>Oppure copia e incolla questo link nel browser:</p>
<p>{link}</p>
<p><strong>Questo link scade tra 72 ore.</strong></p>
<hr>
<p style=""color:#888;font-size:12px;"">Questa e un'email automatica, non rispondere.</p>";
        return await SendEmail(toEmail, subject, body);
    }

    public async Task<bool> SendRoleChangedEmail(string toEmail, string nome, string nuovoRuolo)
    {
        var ruoloLabel = nuovoRuolo switch
        {
            "admin" => "Amministratore",
            "power_user" => "Operatore",
            _ => "Utente"
        };
        var subject = "Ruolo Aggiornato - CineBase";
        var body = $@"
<h2>Ciao {EscapeHtml(nome)},</h2>
<p>Il tuo ruolo sulla piattaforma CineBase e stato aggiornato a <strong>{ruoloLabel}</strong>.</p>
<p>Per motivi di sicurezza, tutte le sessioni attive sono state terminate. Effettua nuovamente il login.</p>
<hr>
<p style=""color:#888;font-size:12px;"">Questa e un'email automatica, non rispondere.</p>";
        return await SendEmail(toEmail, subject, body);
    }

    public async Task<bool> SendPasswordChangedEmail(string toEmail, string nome)
    {
        var subject = "Password Cambiata - CineBase";
        var body = $@"
<h2>Ciao {EscapeHtml(nome)},</h2>
<p>La tua password e stata cambiata con successo.</p>
<p>Se non hai effettuato tu questa modifica, contatta immediatamente l'amministratore.</p>
<hr>
<p style=""color:#888;font-size:12px;"">Questa e un'email automatica, non rispondere.</p>";
        return await SendEmail(toEmail, subject, body);
    }

    public async Task<bool> SendSecurityAlertEmail(string toEmail, string nome, string alertType, string details)
    {
        var subject = "Alert Sicurezza - CineBase";
        var body = $@"
<h2>Ciao {EscapeHtml(nome)},</h2>
<p>Rilevata attivita sospetta sul tuo account CineBase.</p>
<p><strong>Tipo:</strong> {EscapeHtml(alertType)}</p>
<p><strong>Dettagli:</strong> {EscapeHtml(details)}</p>
<p>Se non riconosci questa attivita, contatta immediatamente l'amministratore e cambia la tua password.</p>
<hr>
<p style=""color:#888;font-size:12px;"">Questa e un'email automatica, non rispondere.</p>";
        return await SendEmail(toEmail, subject, body);
    }

    public async Task<bool> SendGiftCardEmail(string toEmail, string codice, decimal importo, string? messaggio)
    {
        var subject = "Hai ricevuto una Gift Card - CineBase";
        var body = $@"
<h2>Hai ricevuto una Gift Card!</h2>
<p>Qualcuno ti ha inviato una Gift Card CineBase del valore di <strong>{importo:C}</strong>.</p>
<p>Il tuo codice e: <strong style=""font-size:1.2rem;letter-spacing:2px;"">{codice}</strong></p>
{(string.IsNullOrWhiteSpace(messaggio) ? "" : $"<p><em>Messaggio: {EscapeHtml(messaggio)}</em></p>")}
<p>Usa questo codice nel carrello per scalare il saldo dal tuo ordine.</p>
<hr>
<p style=""color:#888;font-size:12px;"">Questa e un'email automatica, non rispondere.</p>";
        return await SendEmail(toEmail, subject, body);
    }

    public async Task<bool> SendGiftCardBalanceEmail(string toEmail, string codice, decimal saldoResiduo)
    {
        var subject = "Saldo Gift Card Aggiornato - CineBase";
        var body = $@"
<h2>Saldo Gift Card Aggiornato</h2>
<p>La tua Gift Card <strong>{codice}</strong> e stata utilizzata per un acquisto.</p>
<p>Saldo residuo: <strong>{saldoResiduo:C}</strong></p>
<p>Puoi continuare a usare il codice per acquisti futuri fino a esaurimento del saldo.</p>
<hr>
<p style=""color:#888;font-size:12px;"">Questa e un'email automatica, non rispondere.</p>";
        return await SendEmail(toEmail, subject, body);
    }

    public async Task<bool> SendOrderConfirmationEmail(string toEmail, int cartId, decimal subtotale, decimal sconto, decimal importoGiftCard, decimal totale, int giftCardCount, int ticketCount)
    {
        var subject = $"Conferma Ordine #CART-{cartId} - CineBase";
        var items = new List<string>();
        if (ticketCount > 0) items.Add($"{ticketCount} biglietto/i");
        if (giftCardCount > 0) items.Add($"{giftCardCount} gift card");
        var itemList = string.Join(", ", items);
        var body = $@"
<h2>Grazie per il tuo ordine!</h2>
<p>Il tuo ordine <strong>#CART-{cartId}</strong> e stato confermato.</p>
<p>Riepilogo:</p>
<ul>
<li>Articoli: {itemList}</li>
<li>Subtotale: {subtotale:C}</li>
{(sconto > 0 ? $"<li>Sconto: -{sconto:C}</li>" : "")}
{(importoGiftCard > 0 ? $"<li>Saldo gift card: -{importoGiftCard:C}</li>" : "")}
<li><strong>Totale addebitato: {totale:C}</strong></li>
</ul>
<p>Per i dettagli dei biglietti e i codici gift card, controlla le email separate inviate per ciascun articolo.</p>
<hr>
<p style=""color:#888;font-size:12px;"">Questa e un'email automatica, non rispondere.</p>";
        return await SendEmail(toEmail, subject, body);
    }

    private string BuildLink(string page, string token, string email)
    {
        var baseUrl = _configuration["APP_BASE_URL"] ?? "http://localhost:5001";
        return $"{baseUrl.TrimEnd('/')}/{page}?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(email)}";
    }

    private async Task<bool> SendEmail(string toEmail, string subject, string htmlBody)
    {
        try
        {
            var smtpHost = _configuration["SMTP_HOST"];
            if (string.IsNullOrWhiteSpace(smtpHost))
            {
                _logger.LogWarning("SMTP not configured. Email to {Email} (subject: {Subject}) would have been sent.", toEmail, subject);
                return false;
            }

            var message = new MimeMessage();
            var fromEmail = _configuration["SMTP_FROM"] ?? "noreply@cinebase.local";
            var fromName = _configuration["SMTP_FROM_NAME"] ?? "CineBase";
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = WrapHtml(htmlBody),
                TextBody = StripHtml(htmlBody)
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
            var port = int.TryParse(_configuration["SMTP_PORT"], out var p) ? p : 587;
            var username = _configuration["SMTP_USER"];
            var password = _configuration["SMTP_PASSWORD"];

            await client.ConnectAsync(smtpHost, port, MailKit.Security.SecureSocketOptions.StartTls);
            if (!string.IsNullOrEmpty(username))
            {
                await client.AuthenticateAsync(username, password ?? "");
            }
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {Email}, subject: {Subject}", toEmail, subject);
            return true;
        }
catch (Exception ex)
            {
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                _logger.LogError(ex, "Failed to send email to {Email}, subject: {Subject}", toEmail, subject);
                return false;
            }
    }

    private static string WrapHtml(string body)
    {
        return $@"<!DOCTYPE html>
<html><head><meta charset=""utf-8""></head>
<body style=""font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px;color:#333;"">
<div style=""background:#1a1a2e;color:white;padding:16px;border-radius:8px 8px 0 0;"">
<h1 style=""margin:0;font-size:24px;"">CineBase</h1>
</div>
<div style=""border:1px solid #ddd;border-top:none;padding:24px;border-radius:0 0 8px 8px;"">
{body}
</div>
</body></html>";
    }

    private static string StripHtml(string html)
    {
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", " ")
            .Replace("&nbsp;", " ")
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">");
    }

    private static string EscapeHtml(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text ?? "");
    }
}
