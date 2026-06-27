namespace Ubika.Models;

public class GeneralDetail
{
    public int Id { get; set; }
    public int PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;
    public string Subcategory { get; set; } = string.Empty;
    public string ItemCondition { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? Sku { get; set; }
    public int? Stock { get; set; }
    public string? Color { get; set; }
    public string? Measure { get; set; }
    public string? Weight { get; set; }
    public string? Dimensions { get; set; }
    public string? Warranty { get; set; }
    public string? Shipping { get; set; }
    public bool AcceptsTrade { get; set; }
}
