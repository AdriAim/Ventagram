using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;
using Ventagram.Services;

namespace Ventagram.Pages.Account;

[Authorize]
public class ChangeLocalityModel(VentagramDbContext db, CurrentUserAccessor currentUserAccessor) : PageModel
{
    private const string LocalityField = $"{nameof(Input)}.{nameof(InputModel.ArgentineLocalityId)}";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<ArgentineLocality> AvailableLocalities { get; private set; } = [];

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/ChangeLocality") });
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/ChangeLocality") });
        }

        Input.ArgentineLocalityId = user.ArgentineLocalityId;
        await LoadLocalitiesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/ChangeLocality") });
        }

        await LoadLocalitiesAsync();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var localityExists = await db.ArgentineLocalities
            .AnyAsync(x => x.Id == Input.ArgentineLocalityId && x.IsActive);
        if (!localityExists)
        {
            ModelState.AddModelError(LocalityField, "Selecciona una localidad valida.");
            return Page();
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/ChangeLocality") });
        }

        user.ArgentineLocalityId = Input.ArgentineLocalityId;
        await db.SaveChangesAsync();

        SuccessMessage = "Localidad actualizada.";
        return RedirectToPage();
    }

    private async Task LoadLocalitiesAsync()
    {
        AvailableLocalities = await db.ArgentineLocalities
            .Where(x => x.IsActive)
            .OrderBy(x => x.Province)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Locality)
            .ToListAsync();
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Selecciona tu localidad.")]
        public int? ArgentineLocalityId { get; set; }
    }
}
