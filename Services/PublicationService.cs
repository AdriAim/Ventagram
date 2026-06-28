using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public class PublicationService(VentagramDbContext db)
{
    public async Task<List<Publication>> SearchActivePublicationsAsync(string? group, string? query)
    {
        var items = await GetActivePublicationsAsync();
        return items
            .Where(x => string.IsNullOrWhiteSpace(group) || x.Group == group)
            .Where(x => string.IsNullOrWhiteSpace(query)
                || x.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.ShortDescription.Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.Locality.Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<List<Publication>> GetActivePublicationsAsync()
    {
        var now = DateTime.UtcNow;
        return await db.Publications
            .Include(x => x.PropertyDetail)
            .Include(x => x.VehicleDetail)
            .Include(x => x.GeneralDetail)
            .Include(x => x.ExtraAttributes)
            .Where(x => x.IsActive && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now))
            .OrderByDescending(x => x.Featured)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<Publication?> GetByIdAsync(int id)
    {
        return await db.Publications
            .Include(x => x.PropertyDetail)
            .Include(x => x.VehicleDetail)
            .Include(x => x.GeneralDetail)
            .Include(x => x.ExtraAttributes)
            .Include(x => x.Reports)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<Publication>> GetReportedPublicationsAsync()
    {
        return await db.Publications
            .Include(x => x.PropertyDetail)
            .Include(x => x.VehicleDetail)
            .Include(x => x.GeneralDetail)
            .Include(x => x.ExtraAttributes)
            .Include(x => x.Reports)
            .Where(x => x.Reports.Any())
            .OrderByDescending(x => x.Reports.Count)
            .ThenByDescending(x => x.Reports.Max(r => r.CreatedAtUtc))
            .ToListAsync();
    }

    public async Task<(Publication Publication, string? AnonymousPassword)> CreateAsync(PublicationCreateRequest input, int? userId)
    {
        var publication = new Publication
        {
            Group = input.Group,
            Category = input.Category,
            Title = input.Title,
            Price = input.Price,
            Currency = input.Currency,
            Locality = input.Locality,
            ShortDescription = input.ShortDescription,
            LongDescription = input.LongDescription,
            ImagesCsv = NormalizeImagesCsv(input.ImagesCsv),
            ContactName = input.ContactName,
            ContactPhone = input.ContactPhone,
            ContactEmail = input.ContactEmail,
            Status = "Activa",
            Featured = input.Featured,
            VideoUrl = input.VideoUrl,
            InternalNotes = input.InternalNotes,
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            UserId = userId
        };

        string? rawAnonymousPassword = null;
        if (input.PublisherMode == "Anonymous")
        {
            rawAnonymousPassword = AuthService.GenerateAnonymousPassword();
            publication.IsAnonymous = true;
            publication.ExpiresAtUtc = DateTime.UtcNow.AddDays(30);
            publication.AnonymousDeletePasswordHash = AuthService.HashPassword(rawAnonymousPassword);
        }

        if (input.Group == "Inmuebles")
        {
            publication.PropertyDetail = new PropertyDetail
            {
                PropertyType = input.PropertyType ?? string.Empty,
                Operation = input.Operation ?? string.Empty,
                Zone = input.Zone ?? string.Empty,
                TotalAreaM2 = input.TotalAreaM2 ?? 0,
                CoveredAreaM2 = input.CoveredAreaM2,
                RoomsOrBedrooms = input.RoomsOrBedrooms ?? string.Empty,
                Bathrooms = input.Bathrooms ?? 0,
                Address = input.Address,
                GarageSpaces = input.GarageSpaces,
                AgeYears = input.AgeYears,
                Expenses = input.Expenses,
                Condition = input.Condition,
                MortgageEligible = input.MortgageEligible,
                ProfessionalUseAllowed = input.ProfessionalUseAllowed,
                Services = input.Services,
                Amenities = input.Amenities
            };
        }
        else if (input.Group == "Rodados")
        {
            publication.VehicleDetail = new VehicleDetail
            {
                VehicleType = input.VehicleType ?? string.Empty,
                Brand = input.Brand ?? string.Empty,
                Model = input.Model ?? string.Empty,
                Year = input.Year ?? 0,
                Kilometers = input.Kilometers ?? 0,
                Fuel = input.Fuel ?? string.Empty,
                Transmission = input.Transmission ?? string.Empty,
                Version = input.Version,
                Color = input.Color,
                LicensePlate = input.LicensePlate,
                Engine = input.Engine,
                Traction = input.Traction,
                Doors = input.Doors,
                OwnersCount = input.OwnersCount,
                AcceptsTrade = input.AcceptsTrade,
                FinancingAvailable = input.FinancingAvailable,
                Equipment = input.Equipment,
                GeneralCondition = input.GeneralCondition
            };
        }
        else
        {
            publication.GeneralDetail = new GeneralDetail
            {
                Subcategory = input.Subcategory ?? string.Empty,
                ItemCondition = input.ItemCondition ?? string.Empty,
                Brand = input.Brand,
                Model = input.Model,
                Sku = input.Sku,
                Stock = input.Stock,
                Color = input.Color,
                Measure = input.Measure,
                Weight = input.Weight,
                Dimensions = input.Dimensions,
                Warranty = input.Warranty,
                Shipping = input.Shipping,
                AcceptsTrade = input.AcceptsTrade
            };
        }

        foreach (var pair in ParseExtraAttributes(input.ExtraAttributesRaw))
        {
            publication.ExtraAttributes.Add(new PublicationExtraAttribute { Key = pair.Key, Value = pair.Value });
        }

        db.Publications.Add(publication);
        await db.SaveChangesAsync();
        return (publication, rawAnonymousPassword);
    }

    public async Task<bool> DeactivateAnonymousAsync(int publicationId, string password)
    {
        var publication = await db.Publications.FirstOrDefaultAsync(x => x.Id == publicationId && x.IsAnonymous && x.IsActive);
        if (publication is null || string.IsNullOrWhiteSpace(publication.AnonymousDeletePasswordHash))
        {
            return false;
        }

        if (publication.AnonymousDeletePasswordHash != AuthService.HashPassword(password))
        {
            return false;
        }

        publication.IsActive = false;
        publication.Status = "Baja solicitada";
        await db.SaveChangesAsync();
        return true;
    }

    private static Dictionary<string, string> ParseExtraAttributes(string? raw)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }

        var lines = raw.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            var pieces = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (pieces.Length == 2 && !string.IsNullOrWhiteSpace(pieces[0]) && !string.IsNullOrWhiteSpace(pieces[1]))
            {
                result[pieces[0]] = pieces[1];
            }
        }

        return result;
    }

    private static string NormalizeImagesCsv(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return string.Join(",",
            raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => Uri.IsWellFormedUriString(x, UriKind.Absolute))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(11));
    }
}
