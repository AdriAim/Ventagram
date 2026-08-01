namespace Ventagram.Models;

public class PublicationSearchFilters
{
    public decimal? PriceFrom { get; set; }
    public decimal? PriceTo { get; set; }
    public List<PublicationFieldSearchFilter> FieldFilters { get; set; } = [];
}

public class PublicationFieldSearchFilter
{
    public int FieldId { get; set; }
    public string? Value { get; set; }
}
