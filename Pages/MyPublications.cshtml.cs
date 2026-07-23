using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Ventagram.Models;
using Ventagram.Services;
using Ventagram.Data;

namespace Ventagram.Pages;

[Authorize]
public class MyPublicationsModel(
    PublicationService publicationService,
    CurrentUserAccessor currentUserAccessor,
    VentagramDbContext db) : PageModel
{
    public List<Publication> Publications { get; private set; } = [];
    public List<PublicationReportReason> ReportReasons { get; private set; } = [];

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/MyPublications") });
        }

        Publications = await publicationService.GetOwnedPublicationsAsync(userId);
        ReportReasons = await db.PublicationReportReasons
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/MyPublications") });
        }

        var success = await publicationService.DeactivateOwnedAsync(id, userId);
        SuccessMessage = success
            ? "La publicacion fue dada de baja."
            : "No se pudo dar de baja la publicacion indicada.";

        return RedirectToPage();
    }
}
