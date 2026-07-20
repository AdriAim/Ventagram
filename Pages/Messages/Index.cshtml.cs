using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ventagram.Pages.Messages;

[Authorize]
public class IndexModel(IConfiguration configuration) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    public string ChatBaseUrl { get; private set; } = string.Empty;

    public void OnGet()
    {
        ChatBaseUrl = (configuration["Chat:BaseUrl"] ?? string.Empty).TrimEnd('/');
        if (ChatBaseUrl.Contains(".example.com", StringComparison.OrdinalIgnoreCase))
        {
            ChatBaseUrl = string.Empty;
        }
    }
}
