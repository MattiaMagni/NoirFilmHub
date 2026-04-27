using System.Net;
using System.Net.Mail;

namespace FilmAPI.Services;

public class TicketEmailService
{
    public async Task<bool> SendTicketEmailAsync(string toEmail, string codiceAcquisto, byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? string.Empty;
        var smtpPortRaw = Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587";
        var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER") ?? string.Empty;
        var smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? string.Empty;
        var smtpFrom = Environment.GetEnvironmentVariable("SMTP_FROM") ?? smtpUser;

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpFrom))
        {
            return false;
        }

        if (!int.TryParse(smtpPortRaw, out var smtpPort) || smtpPort <= 0)
        {
            smtpPort = 587;
        }

        using var message = new MailMessage(smtpFrom, toEmail)
        {
            Subject = $"Noir Film Hub - Biglietto {codiceAcquisto}",
            Body = $"Pagamento completato con successo. Codice acquisto: {codiceAcquisto}. In allegato trovi il PDF dei biglietti.",
            IsBodyHtml = false
        };

        var attachmentStream = new MemoryStream(pdfBytes);
        var attachment = new Attachment(attachmentStream, $"ticket-{codiceAcquisto}.pdf", "application/pdf");
        attachment.ContentStream.Position = 0;
        message.Attachments.Add(attachment);

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = smtpPort == 465 || smtpPort == 587,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(smtpUser)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(smtpUser, smtpPassword)
        };

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message, cancellationToken);
        return true;
    }
}
