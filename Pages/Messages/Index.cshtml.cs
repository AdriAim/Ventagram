using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ventagram.Pages.Messages;

[Authorize]
public class IndexModel(IConfiguration configuration) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    public IActionResult OnGet()
    {
        var chatBaseUrl = (configuration["Chat:BaseUrl"] ?? string.Empty).TrimEnd('/');
        if (chatBaseUrl.Contains(".example.com", StringComparison.OrdinalIgnoreCase))
        {
            chatBaseUrl = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(chatBaseUrl))
        {
            return RedirectToPage("/Index");
        }

        var targetUrl = Id.HasValue
            ? $"{chatBaseUrl}/Mensajes/{Id.Value}"
            : $"{chatBaseUrl}/Mensajes";

        return Redirect(targetUrl);
    }
}
