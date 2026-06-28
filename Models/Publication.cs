using System.ComponentModel.DataAnnotations;

namespace Ventagram.Models;

public class Publication
{
    public int Id { get; set; }

    [StringLength(30)]
    public string Group { get; set; } = string.Empty;

    [StringLength(80)]
    public string Category { get; set; } = string.Empty;

    [StringLength(180)]
    public string Title { get; set; } = string.Empty;

    public decimal Price { get; set; }

    [StringLength(10)]
    public string Currency { get; set; } = "USD";

    [StringLength(120)]
    public string Locality { get; set; } = string.Empty;

    [StringLength(260)]
    public string ShortDescription { get; set; } = string.Empty;

    public string? LongDescription { get; set; }
    public string ImagesCsv { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [StringLength(120)]
    public string ContactName { get; set; } = string.Empty;

    [StringLength(32)]
    public string ContactPhone { get; set; } = string.Empty;

    [StringLength(160)]
    public string? ContactEmail { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = "Activa";

    public bool Featured { get; set; }

    [StringLength(400)]
    public string? VideoUrl { get; set; }

    public string? InternalNotes { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }

    [StringLength(128)]
    public string? AnonymousDeletePasswordHash { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public PropertyDetail? PropertyDetail { get; set; }
    public VehicleDetail? VehicleDetail { get; set; }
    public GeneralDetail? GeneralDetail { get; set; }
    public List<PublicationExtraAttribute> ExtraAttributes { get; set; } = [];
    public List<PublicationReport> Reports { get; set; } = [];

    public IReadOnlyList<string> ImageList =>
        ImagesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
