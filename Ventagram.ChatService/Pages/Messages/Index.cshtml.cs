using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ventagram.ChatService.Pages.Messages;

[Authorize]
public class IndexModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    public void OnGet()
    {
    }
}
