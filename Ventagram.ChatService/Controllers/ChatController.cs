using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ventagram.ChatService.Services;

namespace Ventagram.ChatService.Controllers;

[Authorize]
[ApiController]
[Route("api/chat")]
public class ChatController(
    ChatAppService chatService,
    CurrentUserAccessor currentUserAccessor,
    IConfiguration configuration) : Controller
{
    [HttpGet("page/{conversationId:int?}")]
    public async Task<IActionResult> Page(int? conversationId = null)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized();
        }

        var model = await chatService.GetPageAsync(userId, conversationId);
        return PartialView("~/Views/Chat/Page.cshtml", model);
    }

    [HttpGet("inbox/{conversationId:int?}")]
    public async Task<IActionResult> Inbox(int? conversationId = null)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized();
        }

        var model = await chatService.GetPageAsync(userId, conversationId);
        return PartialView("~/Views/Chat/_Inbox.cshtml", model);
    }

    [HttpGet("thread/{conversationId:int?}")]
    public async Task<IActionResult> Thread(int? conversationId = null)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized();
        }

        var model = await chatService.GetPageAsync(userId, conversationId);
        return PartialView("~/Views/Chat/_Thread.cshtml", model);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized();
        }

        return Ok(new { unreadCount = await chatService.GetUnreadCountAsync(userId) });
    }

    [HttpPost("conversations")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> StartConversation([FromBody] StartConversationRequest request)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para usar el chat." });
        }

        if (request.PublicationId <= 0)
        {
            return BadRequest(new { message = "La publicacion indicada no es valida." });
        }

        try
        {
            var conversation = await chatService.GetOrCreateConversationAsync(request.PublicationId, userId);
            var publicBaseUrl = (configuration["Chat:PublicBaseUrl"] ?? string.Empty).TrimEnd('/');
            if (publicBaseUrl.Contains(".example.com", StringComparison.OrdinalIgnoreCase))
            {
                publicBaseUrl = string.Empty;
            }

            var redirectUrl = string.IsNullOrWhiteSpace(publicBaseUrl)
                ? $"/Mensajes/{conversation.Id}"
                : $"{publicBaseUrl}/Mensajes/{conversation.Id}";
            return Ok(new
            {
                conversationId = conversation.Id,
                redirectUrl
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public class StartConversationRequest
{
    public int PublicationId { get; set; }
}
