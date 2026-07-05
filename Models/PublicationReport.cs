namespace Ventagram.Models;

public class PublicationReport
{
    public int Id { get; set; }
    public int PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;
    public int ReasonId { get; set; }
    public PublicationReportReason Reason { get; set; } = null!;
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
