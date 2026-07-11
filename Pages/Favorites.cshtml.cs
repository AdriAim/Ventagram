using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;
using Ventagram.Services;
using Ventagram.ViewModels;

namespace Ventagram.Pages;

[Authorize]
public class FavoritesModel(
    FavoriteService favoriteService,
    CurrentUserAccessor currentUserAccessor,
    VentagramDbContext db) : PageModel
{
    public List<FavoriteListSummaryViewModel> FavoriteLists { get; private set; } = [];

    public List<PublicationReportReason> ReportReasons { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Favorites") });
        }

        FavoriteLists = await favoriteService.GetListSummariesAsync(userId);
        ReportReasons = await db.PublicationReportReasons
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return Page();
    }
}
