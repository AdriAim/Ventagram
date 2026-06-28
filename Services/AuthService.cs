using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public class AuthService(VentagramDbContext db, IHttpContextAccessor httpContextAccessor)
{
    public static string HashPassword(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    public static string GenerateAnonymousPassword()
    {
        return Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
    }

    public async Task<(bool Success, string? Error, ApplicationUser? User)> RegisterAsync(string name, string email, string phone, string password, string provider = "Local")
    {
        email = email.Trim().ToLowerInvariant();
        var exists = await db.Users.AnyAsync(x => x.Email == email);
        if (exists)
        {
            return (false, "Ya existe un usuario con ese email.", null);
        }

        var user = new ApplicationUser
        {
            Name = name.Trim(),
            Email = email,
            Phone = phone.Trim(),
            PasswordHash = provider == "Google" ? string.Empty : HashPassword(password),
            AuthProvider = provider
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (true, null, user);
    }

    public async Task<ApplicationUser?> ValidateUserAsync(string email, string password)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var hash = HashPassword(password);
        return await db.Users.FirstOrDefaultAsync(x => x.Email == normalized && x.PasswordHash == hash);
    }

    public async Task<ApplicationUser> FindOrCreateGoogleUserAsync(string name, string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var existing = await db.Users.FirstOrDefaultAsync(x => x.Email == normalized);
        if (existing is not null)
        {
            return existing;
        }

        var created = await RegisterAsync(name, normalized, string.Empty, string.Empty, "Google");
        return created.User!;
    }

    public async Task SignInAsync(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new("phone", user.Phone ?? string.Empty),
            new("provider", user.AuthProvider)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContextAccessor.HttpContext!.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });
    }

    public async Task SignOutAsync()
    {
        await httpContextAccessor.HttpContext!.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
