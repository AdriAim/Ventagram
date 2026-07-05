using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public class PublicationCategoryService(VentagramDbContext db)
{
    public Task<List<PublicationCategory>> GetActiveByGroupAsync(PublicationGroup group)
    {
        return db.PublicationCategories
            .Where(x => x.IsActive && x.Group == group)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public Task<bool> ExistsAsync(PublicationGroup group, string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return Task.FromResult(false);
        }

        var normalized = categoryName.Trim();
        return db.PublicationCategories
            .AnyAsync(x => x.IsActive
                && x.Group == group
                && x.Name == normalized);
    }
}
