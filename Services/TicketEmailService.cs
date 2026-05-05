using System.Net;
using System.Net.Mail;

namespace FilmAPI.Services;

public class TicketEmailService
{
	private readonly ILogger<TicketEmailService> _logger;

	public TicketEmailService(ILogger<TicketEmailService> logger)
	{
		_logger = logger;
	}

	public async Task<bool> SendTicketEmailAsync(string toEmail, string codiceAcquisto, byte[] pdfBytes, CancellationToken cancellationToken = default)
	{
		var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? string.Empty;
		var smtpPortRaw = Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587";
		var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER") ?? string.Empty;
		var smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? string.Empty;
		var smtpFrom = Environment.GetEnvironmentVariable("SMTP_FROM") ?? smtpUser;

		if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpFrom))
		{
			_logger.LogWarning("Email non inviata: SMTP non configurato (SMTP_HOST o SMTP_FROM mancanti)");
			return false;
		}

		if (!int.TryParse(smtpPortRaw, out var smtpPort) || smtpPort <= 0)
		{
			smtpPort = 587;
		}

		if (smtpPort == 465)
		{
			_logger.LogWarning("Porta 465 non supportata da System.Net.Mail.SmtpClient (richiede TLS implicito). Usare porta 587 con STARTTLS.");
			smtpPort = 587;
		}

		try
		{
			using var message = new MailMessage(smtpFrom, toEmail)
			{
				Subject = $"Noir Film Hub - Biglietto {codiceAcquisto}",
				Body = $"Pagamento completato con successo.\n\nCodice acquisto: {codiceAcquisto}\n\nIn allegato trovi il PDF dei biglietti.\n\nNoir Film Hub",
				IsBodyHtml = false
			};

			using var attachmentStream = new MemoryStream(pdfBytes);
			var attachment = new Attachment(attachmentStream, $"ticket-{codiceAcquisto}.pdf", "application/pdf");
			attachment.ContentStream.Position = 0;
			message.Attachments.Add(attachment);

			using var client = new SmtpClient(smtpHost, smtpPort)
			{
				EnableSsl = true,
				DeliveryMethod = SmtpDeliveryMethod.Network,
				UseDefaultCredentials = false,
				Credentials = string.IsNullOrWhiteSpace(smtpUser)
					? CredentialCache.DefaultNetworkCredentials
					: new NetworkCredential(smtpUser, smtpPassword)
			};

			cancellationToken.ThrowIfCancellationRequested();
			await client.SendMailAsync(message, cancellationToken);
			_logger.LogInformation("Email inviata con successo a {Email} per ordine {CodiceAcquisto}", toEmail, codiceAcquisto);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Errore nell'invio email a {Email} per ordine {CodiceAcquisto}", toEmail, codiceAcquisto);
			return false;
		}
	}
}
