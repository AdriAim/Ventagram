using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ubika.Services;

namespace Ubika.Pages.Account;

public class RegisterModel(AuthService authService, IConfiguration configuration) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool IsGoogleEnabled => !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]);

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await authService.RegisterAsync(Input.Name, Input.Email, Input.Phone, Input.Password);
        if (!result.Success || result.User is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "No se pudo crear la cuenta.");
            return Page();
        }

        await authService.SignInAsync(result.User);
        return RedirectToPage("/Index");
    }

    public IActionResult OnPostGoogle()
    {
        if (!IsGoogleEnabled)
        {
            ModelState.AddModelError(string.Empty, "Google no está configurado.");
            return Page();
        }

        var properties = new AuthenticationProperties { RedirectUri = "/" };
        return Challenge(properties, "Google");
    }

    public class InputModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
