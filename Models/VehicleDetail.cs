namespace Ventagram.Models;

public class VehicleDetail
{
    public int Id { get; set; }
    public int PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;
    public string VehicleType { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Kilometers { get; set; }
    public string Fuel { get; set; } = string.Empty;
    public string Transmission { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? Color { get; set; }
    public string? LicensePlate { get; set; }
    public string? Engine { get; set; }
    public string? Traction { get; set; }
    public int? Doors { get; set; }
    public int? OwnersCount { get; set; }
    public bool AcceptsTrade { get; set; }
    public bool FinancingAvailable { get; set; }
    public string? Equipment { get; set; }
    public string? GeneralCondition { get; set; }
}
