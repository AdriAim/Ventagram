using Ubika.Data;
using Ubika.Models;

namespace Ubika.Services;

public class ReportService(UbikaDbContext db)
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
