using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Ubika.Services;

namespace Ubika.Pages.Account;

public class LoginModel(AuthService authService, IConfiguration configuration) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

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

        var user = await authService.ValidateUserAsync(Input.Email, Input.Password);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Email o contraseña inválidos.");
            return Page();
        }

        await authService.SignInAsync(user);
        return LocalRedirect(ReturnUrl ?? "/");
    }

    public IActionResult OnPostGoogle()
    {
        if (!IsGoogleEnabled)
        {
            ModelState.AddModelError(string.Empty, "Google no está configurado.");
            return Page();
        }

        var redirect = Url.Page("/Account/Login", values: new { returnUrl = ReturnUrl }) ?? "/";
        var properties = new AuthenticationProperties { RedirectUri = ReturnUrl ?? "/" };
        return Challenge(properties, "Google");
    }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
