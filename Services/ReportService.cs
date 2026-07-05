using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public class ReportService(VentagramDbContext db)
{
    public async Task CreateAsync(int publicationId, int reasonId, string? comment)
    {
        db.PublicationReports.Add(new PublicationReport
        {
            PublicationId = publicationId,
            ReasonId = reasonId,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()
        });

        await db.SaveChangesAsync();
    }
}
