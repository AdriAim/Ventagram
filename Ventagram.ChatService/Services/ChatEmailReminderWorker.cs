using Microsoft.EntityFrameworkCore;
using Ventagram.ChatService.Data;
using Ventagram.ChatService.Models;

namespace Ventagram.ChatService.Services;

public class ChatEmailReminderWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ChatEmailReminderWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ReminderDelay = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo el worker de recordatorios de chat.");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var threshold = DateTime.UtcNow - ReminderDelay;
        var candidates = await db.ChatMessages
            .Include(x => x.Conversation!)
                .ThenInclude(x => x.Publication)
            .Include(x => x.Conversation!)
                .ThenInclude(x => x.BuyerUser)
            .Include(x => x.Conversation!)
                .ThenInclude(x => x.SellerUser)
            .Include(x => x.SenderUser)
            .Where(x => x.EmailReminderSentAtUtc == null && x.CreatedAtUtc <= threshold)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var message in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var recipient = ResolveRecipient(message);
            if (recipient is null || !recipient.RespondsEmails || string.IsNullOrWhiteSpace(recipient.Email))
            {
                message.EmailReminderSentAtUtc = DateTime.UtcNow;
                continue;
            }

            var recipientReplied = await db.ChatMessages.AnyAsync(
                x => x.ConversationId == message.ConversationId
                    && x.SenderUserId == recipient.Id
                    && x.CreatedAtUtc > message.CreatedAtUtc,
                cancellationToken);

            if (recipientReplied)
            {
                message.EmailReminderSentAtUtc = DateTime.UtcNow;
                continue;
            }

            var publication = message.Conversation?.Publication;
            var senderName = message.SenderUser?.Name ?? "Usuario";
            var publicationTitle = publication?.Title ?? "un anuncio";
            var publicBaseUrl = (configuration["Chat:PublicBaseUrl"] ?? string.Empty).TrimEnd('/');
            var detailsUrl = string.IsNullOrWhiteSpace(publicBaseUrl)
                ? $"/Mensajes/{message.ConversationId}"
                : $"{publicBaseUrl}/Mensajes/{message.ConversationId}";

            var subject = $"Nuevo mensaje pendiente en Ventagram sobre {publicationTitle}";
            var htmlBody = $"""
                <p>Hola {recipient.Name},</p>
                <p>Hace una hora recibiste un mensaje en Ventagram y todavia no lo respondiste.</p>
                <p><strong>Anuncio:</strong> {System.Net.WebUtility.HtmlEncode(publicationTitle)}</p>
                <p><strong>De:</strong> {System.Net.WebUtility.HtmlEncode(senderName)}</p>
                <blockquote style="margin:16px 0;padding:12px 16px;border-left:4px solid #e3374e;background:#f9f8f7;">
                    {System.Net.WebUtility.HtmlEncode(message.Body).Replace("\n", "<br />")}
                </blockquote>
                <p><a href="{detailsUrl}">Abrir conversacion</a></p>
                """;
            var textBody = $"""
                Hola {recipient.Name},

                Hace una hora recibiste un mensaje en Ventagram y todavia no lo respondiste.

                Anuncio: {publicationTitle}
                De: {senderName}

                Mensaje:
                {message.Body}

                Abrir conversacion: {detailsUrl}
                """;

            var sent = await emailSender.SendAsync(recipient.Email, subject, htmlBody, textBody);
            if (sent)
            {
                message.EmailReminderSentAtUtc = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static ApplicationUser? ResolveRecipient(ChatMessage message)
    {
        var conversation = message.Conversation;
        if (conversation is null)
        {
            return null;
        }

        return message.SenderUserId == conversation.BuyerUserId
            ? conversation.SellerUser
            : conversation.BuyerUser;
    }
}
