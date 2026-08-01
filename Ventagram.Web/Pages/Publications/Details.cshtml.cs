using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ventagram.Pages.Publications;

public class DetailsModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    public void OnGet()
    {
    }
}
