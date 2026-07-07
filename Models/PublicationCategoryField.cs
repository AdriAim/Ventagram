using System.ComponentModel.DataAnnotations;

namespace Ventagram.Models;

public class PublicationCategoryField
{
    public int Id { get; set; }
    public byte? GroupId { get; set; }
    public int? CategoryId { get; set; }
    public PublicationCategory Category { get; set; } = null!;

    [StringLength(80)]
    public string InternalName { get; set; } = string.Empty;

    [StringLength(120)]
    public string Label { get; set; } = string.Empty;

    public PublicationCategoryFieldDataType DataType { get; set; } = PublicationCategoryFieldDataType.Texto;
    public bool Required { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    [StringLength(24)]
    public string? Unit { get; set; }

    [StringLength(1000)]
    public string? OptionsCsv { get; set; }

    public List<PublicationFieldValue> Values { get; set; } = [];
}
