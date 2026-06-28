using System.ComponentModel.DataAnnotations;

namespace Ventagram.Models;

public class ApplicationUser
{
    public int Id { get; set; }

    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(160)]
    public string Email { get; set; } = string.Empty;

    [StringLength(32)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [StringLength(40)]
    public string AuthProvider { get; set; } = "Local";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
