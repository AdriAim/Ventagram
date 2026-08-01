using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public class PublicationCategoryFieldService(VentagramDbContext db)
{
    public async Task<List<PublicationCategoryField>> GetRequiredActiveByGroupAsync(PublicationGroup group)
    {
        var groupId = (byte)group;

        return await db.PublicationCategoryFields
            .AsNoTracking()
            .Where(x => x.IsActive
                && x.Required
                && (x.GroupId == groupId || x.GroupId == null)
                && (x.CategoryId == null || (x.Category != null && x.Category.Group == group)))
            .OrderByDescending(x => x.Required)
            .ThenByDescending(x => x.ShowInBasicData)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToListAsync();
    }

    public async Task<List<PublicationCategoryField>> GetActiveByCategoryIdAsync(int categoryId)
    {
        var category = await db.PublicationCategories
            .AsNoTracking()
            .FirstAsync(x => x.Id == categoryId);

        return await db.PublicationCategoryFields
            .Where(x => x.IsActive
                && (x.GroupId == (byte)category.Group || x.GroupId == null)
                && (x.CategoryId == categoryId || x.CategoryId == null))
            .OrderBy(x => x.CategoryId == null ? 0 : 1)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToListAsync();
    }

    public Task<List<PublicationCategoryField>> GetActiveDefinitionsForCategoryAsync(int categoryId)
    {
        return GetActiveByCategoryIdAsync(categoryId);
    }
}
