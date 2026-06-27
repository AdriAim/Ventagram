namespace Ubika.Models;

public class PropertyDetail
{
    public int Id { get; set; }
    public int PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;
    public string PropertyType { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public decimal TotalAreaM2 { get; set; }
    public decimal? CoveredAreaM2 { get; set; }
    public string RoomsOrBedrooms { get; set; } = string.Empty;
    public int Bathrooms { get; set; }
    public string? Address { get; set; }
    public int? GarageSpaces { get; set; }
    public int? AgeYears { get; set; }
    public decimal? Expenses { get; set; }
    public string? Condition { get; set; }
    public bool MortgageEligible { get; set; }
    public bool ProfessionalUseAllowed { get; set; }
    public string? Services { get; set; }
    public string? Amenities { get; set; }
}
