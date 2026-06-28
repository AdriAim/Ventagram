namespace Ventagram.Models;

public class PublicationExtraAttribute
{
    public int Id { get; set; }
    public int PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
