using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public class PublicationGroupTypeService(VentagramDbContext db)
{
    public Task<List<PublicationGroupType>> GetActiveAsync()
    {
        return db.PublicationGroupTypes
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public Task<bool> ExistsAsync(PublicationGroup group)
    {
        var id = (byte)group;
        return db.PublicationGroupTypes.AnyAsync(x => x.IsActive && x.Id == id);
    }
}
