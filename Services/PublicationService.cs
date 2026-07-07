using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;

namespace Ventagram.Services;

public class PublicationService(VentagramDbContext db)
{
    public async Task<List<Publication>> SearchActivePublicationsAsync(PublicationGroup? group, string? query)
    {
        var items = await GetActivePublicationsAsync();
        return items
            .Where(x => group is null || x.Group == group.Value)
            .Where(x => string.IsNullOrWhiteSpace(query)
                || x.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.ShortDescription.Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.Locality.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (x.Category?.Name ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.FieldValues.Any(v =>
                    (v.ValueText ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public async Task<List<Publication>> GetActivePublicationsAsync()
    {
        var now = DateTime.UtcNow;
        return await db.Publications
            .Include(x => x.Category)
            .Include(x => x.FieldValues)
                .ThenInclude(x => x.CategoryField)
            .Where(x => x.IsActive && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now))
            .OrderByDescending(x => x.Featured)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<Publication?> GetByIdAsync(int id)
    {
        return await db.Publications
            .Include(x => x.User)
            .Include(x => x.Category)
            .Include(x => x.FieldValues)
                .ThenInclude(x => x.CategoryField)
            .Include(x => x.Reports)
                .ThenInclude(x => x.Reason)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<Publication>> GetReportedPublicationsAsync()
    {
        return await db.Publications
            .Include(x => x.Category)
            .Include(x => x.FieldValues)
                .ThenInclude(x => x.CategoryField)
            .Include(x => x.Reports)
                .ThenInclude(x => x.Reason)
            .Where(x => x.Reports.Any())
            .OrderByDescending(x => x.Reports.Count)
            .ThenByDescending(x => x.Reports.Max(r => r.CreatedAtUtc))
            .ToListAsync();
    }

    public async Task<(Publication Publication, string? AnonymousPassword)> CreateAsync(PublicationCreateRequest input, int? userId)
    {
        var category = await db.PublicationCategories
            .AsNoTracking()
            .FirstAsync(x => x.Id == input.CategoryId);

        var categoryFields = await db.PublicationCategoryFields
            .Where(x => x.IsActive
                && (x.GroupId == (byte)category.Group || x.GroupId == null)
                && (x.CategoryId == input.CategoryId || x.CategoryId == null))
            .OrderBy(x => x.CategoryId == null ? 0 : 1)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var publication = new Publication
        {
            Group = input.Group,
            CategoryId = input.CategoryId,
            Title = input.Title,
            Price = input.Price,
            Currency = input.Currency,
            Locality = input.NoLocation ? string.Empty : input.Locality,
            ShortDescription = input.ShortDescription,
            LongDescription = input.LongDescription,
            ImagesCsv = NormalizeImagesCsv(input.ImagesCsv),
            ContactName = input.ContactName ?? string.Empty,
            ContactPhone = input.ContactPhone ?? string.Empty,
            ContactEmail = input.ContactEmail,
            Status = "Activa",
            Featured = input.Featured,
            VideoUrl = NormalizeOptionalUrl(input.VideoUrl),
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

        foreach (var fieldValue in BuildDynamicFieldValues(input, categoryFields))
        {
            publication.FieldValues.Add(fieldValue);
        }

        db.Publications.Add(publication);
        await db.SaveChangesAsync();
        return (publication, rawAnonymousPassword);
    }

    public async Task<List<object>> ValidateDynamicFieldsAsync(PublicationCreateRequest input)
    {
        var errors = new List<object>();
        var category = await db.PublicationCategories
            .AsNoTracking()
            .FirstAsync(x => x.Id == input.CategoryId);

        var definitions = await db.PublicationCategoryFields
            .Where(x => x.IsActive
                && (x.GroupId == (byte)category.Group || x.GroupId == null)
                && (x.CategoryId == input.CategoryId || x.CategoryId == null))
            .OrderBy(x => x.CategoryId == null ? 0 : 1)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var normalizedInputs = new Dictionary<int, PublicationDynamicFieldInput>();
        foreach (var item in MergeDynamicFieldInputs(input, definitions))
        {
            normalizedInputs[item.FieldId] = item;
        }

        foreach (var field in definitions)
        {
            normalizedInputs.TryGetValue(field.Id, out var value);
            if (field.Required && IsMissingDynamicValue(field.DataType, value))
            {
                errors.Add(new { field = field.InternalName, message = $"Completa {field.Label}." });
                continue;
            }

            if (value is null || IsMissingDynamicValue(field.DataType, value))
            {
                continue;
            }

            if (!IsValidDynamicValue(field.DataType, value))
            {
                errors.Add(new { field = field.InternalName, message = $"El valor de {field.Label} no coincide con el tipo esperado." });
            }
        }

        foreach (var unknownField in input.DynamicFields.Where(x => definitions.All(d => d.Id != x.FieldId)))
        {
            errors.Add(new { field = $"dynamicField:{unknownField.FieldId}", message = "El campo dinamico no pertenece a la categoria seleccionada." });
        }

        return errors;
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

    private static List<PublicationFieldValue> BuildDynamicFieldValues(
        PublicationCreateRequest input,
        IReadOnlyCollection<PublicationCategoryField> definitions)
    {
        return MergeDynamicFieldInputs(input, definitions)
            .Where(x => x.FieldId > 0)
            .Select(x => new PublicationFieldValue
            {
                CategoryFieldId = x.FieldId,
                ValueText = NormalizeValueText(x.ValueText),
                ValueNumber = x.ValueNumber,
                ValueBoolean = x.ValueBoolean
            })
            .Where(HasAnyValue)
            .ToList();
    }

    private static IEnumerable<PublicationDynamicFieldInput> MergeDynamicFieldInputs(
        PublicationCreateRequest input,
        IReadOnlyCollection<PublicationCategoryField> definitions)
    {
        var values = new Dictionary<int, PublicationDynamicFieldInput>();

        foreach (var item in input.DynamicFields)
        {
            if (item.FieldId <= 0)
            {
                continue;
            }

            values[item.FieldId] = item;
        }

        foreach (var legacy in BuildLegacyDynamicFieldInputs(input, definitions))
        {
            if (!values.ContainsKey(legacy.FieldId))
            {
                values[legacy.FieldId] = legacy;
            }
        }

        return values.Values;
    }

    private static IEnumerable<PublicationDynamicFieldInput> BuildLegacyDynamicFieldInputs(
        PublicationCreateRequest input,
        IReadOnlyCollection<PublicationCategoryField> definitions)
    {
        var byName = definitions.ToDictionary(x => x.InternalName, StringComparer.OrdinalIgnoreCase);

        PublicationDynamicFieldInput? CreateText(string name, string? value)
            => TryResolveField(byName, name, out var field) && !string.IsNullOrWhiteSpace(value)
                ? new PublicationDynamicFieldInput { FieldId = field.Id, ValueText = value.Trim() }
                : null;

        PublicationDynamicFieldInput? CreateNumber(string name, decimal? value)
            => TryResolveField(byName, name, out var field) && value.HasValue
                ? new PublicationDynamicFieldInput { FieldId = field.Id, ValueNumber = value.Value }
                : null;

        PublicationDynamicFieldInput? CreateBoolean(string name, bool value)
            => TryResolveField(byName, name, out var field) && value
                ? new PublicationDynamicFieldInput { FieldId = field.Id, ValueBoolean = value }
                : null;

        var candidates = new PublicationDynamicFieldInput?[]
        {
            CreateText("operacion", input.Operation),
            CreateText("zona", input.Zone),
            CreateNumber("superficie_total_m2", input.TotalAreaM2),
            CreateNumber("superficie_cubierta_m2", input.CoveredAreaM2),
            CreateText("ambientes", input.RoomsOrBedrooms),
            CreateNumber("banios", input.Bathrooms),
            CreateText("direccion", input.Address),
            CreateNumber("garage", input.GarageSpaces),
            CreateNumber("antiguedad_anios", input.AgeYears),
            CreateNumber("expensas", input.Expenses),
            CreateText("estado", input.Condition),
            CreateBoolean("apto_credito", input.MortgageEligible),
            CreateBoolean("uso_profesional", input.ProfessionalUseAllowed),
            CreateText("servicios", input.Services),
            CreateText("amenities", input.Amenities),
            CreateText("tipo_vehiculo", input.VehicleType),
            CreateText("marca", input.Brand),
            CreateText("modelo", input.Model),
            CreateNumber("anio", input.Year),
            CreateNumber("kilometros", input.Kilometers),
            CreateText("combustible", input.Fuel),
            CreateText("transmision", input.Transmission),
            CreateText("version", input.Version),
            CreateText("color", input.Color),
            CreateText("patente", input.LicensePlate),
            CreateText("motor", input.Engine),
            CreateText("traccion", input.Traction),
            CreateNumber("puertas", input.Doors),
            CreateNumber("titulares", input.OwnersCount),
            CreateBoolean("permuta", input.AcceptsTrade),
            CreateBoolean("financiacion", input.FinancingAvailable),
            CreateText("equipamiento", input.Equipment),
            CreateText("estado_general", input.GeneralCondition),
            CreateText("subcategoria", input.Subcategory),
            CreateText("estado_articulo", input.ItemCondition),
            CreateText("sku", input.Sku),
            CreateNumber("stock", input.Stock),
            CreateText("medida", input.Measure),
            CreateText("peso", input.Weight),
            CreateText("dimensiones", input.Dimensions),
            CreateText("garantia", input.Warranty),
            CreateText("envio", input.Shipping)
        };

        return candidates.Where(x => x is not null).Select(x => x!);
    }

    private static bool TryResolveField(
        IReadOnlyDictionary<string, PublicationCategoryField> byName,
        string internalName,
        out PublicationCategoryField field)
    {
        return byName.TryGetValue(internalName, out field!);
    }

    private static bool IsMissingDynamicValue(PublicationCategoryFieldDataType dataType, PublicationDynamicFieldInput? value)
    {
        if (value is null)
        {
            return true;
        }

        return dataType switch
        {
            PublicationCategoryFieldDataType.Numero => value.ValueNumber is null,
            PublicationCategoryFieldDataType.Booleano => value.ValueBoolean is null,
            _ => string.IsNullOrWhiteSpace(value.ValueText)
        };
    }

    private static bool IsValidDynamicValue(PublicationCategoryFieldDataType dataType, PublicationDynamicFieldInput value)
    {
        return dataType switch
        {
            PublicationCategoryFieldDataType.Numero => value.ValueNumber is not null,
            PublicationCategoryFieldDataType.Booleano => value.ValueBoolean is not null,
            PublicationCategoryFieldDataType.Texto or PublicationCategoryFieldDataType.Lista => !string.IsNullOrWhiteSpace(value.ValueText),
            _ => false
        };
    }

    private static bool HasAnyValue(PublicationFieldValue value)
    {
        return !string.IsNullOrWhiteSpace(value.ValueText)
            || value.ValueNumber is not null
            || value.ValueBoolean is not null;
    }

    private static string? NormalizeValueText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeImagesCsv(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return string.Join(",",
            raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => Uri.IsWellFormedUriString(x, UriKind.Absolute) || x.StartsWith("/", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(11));
    }

    private static string? NormalizeOptionalUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        return Uri.IsWellFormedUriString(value, UriKind.Absolute) || value.StartsWith("/", StringComparison.Ordinal)
            ? value
            : null;
    }

}
