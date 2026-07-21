using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Services;

namespace Ventagram.Pages.Account;

[Authorize]
public class SettingsModel(VentagramDbContext db, CurrentUserAccessor currentUserAccessor) : PageModel
{
    private const string PhoneField = $"{nameof(Input)}.{nameof(InputModel.Phone)}";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/Settings") });
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/Settings") });
        }

        Input.Phone = user.Phone;
        Input.PhoneCountry = user.Phone.StartsWith("+54 9 ", StringComparison.Ordinal) ? "AR" : "INT";
        Input.PublishEmail = user.RespondsEmails;
        Input.PublishPhone = user.AcceptsCalls || user.RespondsWhatsApp;
        Input.AllowSiteChat = user.AllowsSiteChat;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/Settings") });
        }

        if (Input.PhoneCountry == "AR" && !Regex.IsMatch(Input.Phone.Trim(), @"^\+54 9 \d{10}$"))
        {
            ModelState.AddModelError(PhoneField, "Ingresa el telefono argentino como +54 9 seguido de 10 digitos.");
        }

        if (!Input.PublishEmail && !Input.PublishPhone && !Input.AllowSiteChat)
        {
            ModelState.AddModelError(string.Empty, "Selecciona al menos un canal de contacto para publicar.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Account/Settings") });
        }

        user.Phone = NormalizePhone(Input.Phone, Input.PhoneCountry);
        user.RespondsEmails = Input.PublishEmail;
        user.AcceptsCalls = Input.PublishPhone;
        user.RespondsWhatsApp = Input.PublishPhone;
        user.AllowsSiteChat = Input.AllowSiteChat;
        user.ContactPreference = BuildContactPreference(user.RespondsEmails, user.AcceptsCalls, user.RespondsWhatsApp, user.AllowsSiteChat);
        await db.SaveChangesAsync();

        SuccessMessage = "Configuraciones actualizadas.";
        return RedirectToPage();
    }

    private static string NormalizePhone(string phone, string phoneCountry)
    {
        var trimmed = phone.Trim();
        if (phoneCountry != "AR")
        {
            return trimmed;
        }

        var digits = Regex.Replace(trimmed, @"\D", "");
        if (digits.StartsWith("549", StringComparison.Ordinal))
        {
            digits = digits[3..];
        }
        else if (digits.StartsWith("54", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.StartsWith("9", StringComparison.Ordinal) && digits.Length == 11)
        {
            digits = digits[1..];
        }

        return digits.Length == 10 ? $"+54 9 {digits}" : trimmed;
    }

    private static string BuildContactPreference(bool publishEmail, bool publishPhone, bool publishWhatsApp, bool allowSiteChat)
    {
        var preferences = new List<string>();
        if (publishEmail) preferences.Add("Email");
        if (publishPhone) preferences.Add("Calls");
        if (publishWhatsApp) preferences.Add("WhatsApp");
        if (allowSiteChat) preferences.Add("SiteChat");
        return preferences.Count == 0 ? "None" : string.Join(",", preferences);
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Ingresa tu telefono.")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string PhoneCountry { get; set; } = "AR";

        public bool PublishEmail { get; set; }

        public bool PublishPhone { get; set; }

        public bool AllowSiteChat { get; set; } = true;
    }
}
