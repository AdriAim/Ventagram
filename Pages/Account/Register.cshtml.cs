using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;
using Ventagram.Services;

namespace Ventagram.Pages.Account;

public class RegisterModel(AuthService authService, IConfiguration configuration, VentagramDbContext db) : PageModel
{
    private const string EmailField = $"{nameof(Input)}.{nameof(InputModel.Email)}";
    private const string PhoneField = $"{nameof(Input)}.{nameof(InputModel.Phone)}";
    private const string LocalityField = $"{nameof(Input)}.{nameof(InputModel.ArgentineLocalityId)}";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<ArgentineLocality> AvailableLocalities { get; private set; } = [];

    public bool IsGoogleEnabled => !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]);

    public async Task OnGetAsync()
    {
        await LoadLocalitiesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
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

        if (Input.PhoneCountry == "AR" && !Regex.IsMatch(Input.Phone.Trim(), @"^\+54 9 \d{10}$"))
        {
            ModelState.AddModelError(PhoneField, "Ingresa el telefono argentino como +54 9 seguido de 10 digitos.");
            return Page();
        }

        if (!Input.RespondsEmails && !Input.AcceptsCalls && !Input.RespondsWhatsApp)
        {
            ModelState.AddModelError(string.Empty, "Marca al menos una forma de contacto.");
            return Page();
        }

        var result = await authService.RegisterAsync(
            Input.Name,
            Input.Email,
            Input.PhoneCountry == "AR" ? Input.Phone : Input.Phone.Trim(),
            Input.Password,
            Input.PhoneCountry,
            Input.RespondsEmails,
            Input.AcceptsCalls,
            Input.RespondsWhatsApp,
            Input.ArgentineLocalityId);
        if (!result.Success || result.User is null)
        {
            if (!string.IsNullOrWhiteSpace(result.Error) && result.Error.Contains("email", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(EmailField, result.Error);
            }
            else
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "No se pudo crear la cuenta.");
            }

            return Page();
        }

        await authService.SignInAsync(result.User);
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostGoogle()
    {
        await LoadLocalitiesAsync();

        if (!IsGoogleEnabled)
        {
            ModelState.AddModelError(string.Empty, "Google no esta configurado.");
            return Page();
        }

        var properties = new AuthenticationProperties { RedirectUri = "/" };
        return Challenge(properties, "Google");
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
        [Required(ErrorMessage = "Ingresa tu nombre.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu email.")]
        [EmailAddress(ErrorMessage = "Ingresa un email valido.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingresa tu telefono.")]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string PhoneCountry { get; set; } = "AR";

        [Required(ErrorMessage = "Selecciona tu localidad.")]
        public int? ArgentineLocalityId { get; set; }

        public bool RespondsEmails { get; set; }

        public bool AcceptsCalls { get; set; }

        public bool RespondsWhatsApp { get; set; }

        [Required(ErrorMessage = "Ingresa una contrasena.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repite la contrasena.")]
        [Compare(nameof(Password), ErrorMessage = "Las contrasenas no coinciden.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
