using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public class ReportService(VentagramDbContext db)
{
    public async Task CreateAsync(int publicationId, string reason)
    {
        db.PublicationReports.Add(new PublicationReport
        {
            PublicationId = publicationId,
            Reason = reason
        });

        await db.SaveChangesAsync();
    }
}
