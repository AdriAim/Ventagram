using System.Net;
using System.Net.Mail;

namespace Ventagram.ChatService.Services;

public class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendAsync(string toEmail, string subject, string htmlBody, string textBody)
    {
        var smtpHost = configuration["Email:SmtpHost"];
        var smtpPort = configuration.GetValue<int?>("Email:SmtpPort") ?? 587;
        var smtpUser = configuration["Email:SmtpUser"];
        var smtpPassword = configuration["Email:SmtpPassword"];
        var fromEmail = configuration["Email:FromEmail"];
        var fromName = configuration["Email:FromName"] ?? "Ventagram";
        var useSsl = configuration.GetValue("Email:UseSsl", true);

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(fromEmail))
        {
            logger.LogWarning("SMTP no configurado. No se envio el correo a {ToEmail}.", toEmail);
            return false;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        message.To.Add(toEmail);
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain"));

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = useSsl,
            Credentials = string.IsNullOrWhiteSpace(smtpUser)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(smtpUser, smtpPassword)
        };

        try
        {
            await client.SendMailAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo enviar el correo a {ToEmail}.", toEmail);
            return false;
        }
    }
}
