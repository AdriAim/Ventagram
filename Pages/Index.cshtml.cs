using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ubika.Pages;

public class IndexModel(IConfiguration configuration) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Group { get; set; } = "Inmuebles";

    [BindProperty(SupportsGet = true)]
    public string Mode { get; set; } = "Galeria";

    [BindProperty(SupportsGet = true)]
    public string? Query { get; set; }

    public string MapTilerKey => configuration["MapTiler:ApiKey"] ?? string.Empty;

    public void OnGet()
    {
    }
}
