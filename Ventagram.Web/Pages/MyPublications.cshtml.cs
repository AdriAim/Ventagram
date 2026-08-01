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
    public static readonly IReadOnlyList<string> DeactivationReasons =
    [
        "Ya se vendió",
        "Ya no está disponible",
        "Quiero corregir el anuncio",
        "Publiqué por error",
        "Otro motivo"
    ];

    public List<Publication> Publications { get; private set; } = [];
    public List<PublicationReportReason> ReportReasons { get; private set; } = [];
    public bool PublishingBlocked { get; private set; }
    public bool ReportingBlocked { get; private set; }
    public bool IsAdmin { get; private set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/MyPublications") });
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/MyPublications") });
        }

        PublishingBlocked = !user.CanPublish;
        ReportingBlocked = !user.CanReport;
        IsAdmin = user.IsAdmin;
        Publications = await publicationService.GetOwnedPublicationsAsync(userId);
        ReportReasons = await db.PublicationReportReasons
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id, string reason, string? comment)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/MyPublications") });
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            ErrorMessage = "Selecciona un motivo para dar de baja el anuncio.";
            return RedirectToPage();
        }

        var success = await publicationService.DeactivateOwnedAsync(id, userId, reason, comment);
        SuccessMessage = success
            ? "El anuncio fue dado de baja."
            : "No se pudo dar de baja el anuncio indicado.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRepublishAsync(int id)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/MyPublications") });
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/MyPublications") });
        }

        if (!user.CanPublish)
        {
            ErrorMessage = "No puedes republicar anuncios hasta que un administrador revise tu cuenta.";
            return RedirectToPage();
        }

        var success = await publicationService.RepublishOwnedAsync(id, userId);
        if (success)
        {
            SuccessMessage = "El anuncio fue republicado por 30 días más.";
        }
        else
        {
            ErrorMessage = "No se pudo republicar el anuncio indicado.";
        }

        return RedirectToPage();
    }
}
