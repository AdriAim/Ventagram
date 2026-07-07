using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Ventagram.Data;
using Ventagram.Models;
using Ventagram.Services;
using Ventagram.ViewModels;

namespace Ventagram.Controllers;

[ApiController]
[Route("api/content")]
public class ContentController(
    PublicationService publicationService,
    PublicationGroupTypeService publicationGroupTypeService,
    PublicationCategoryService publicationCategoryService,
    ReportService reportService,
    CloudflareR2ImageStorageService imageStorageService,
    CurrentUserAccessor currentUserAccessor,
    VentagramDbContext db,
    ILogger<ContentController> logger,
    IConfiguration configuration) : Controller
{
    [HttpGet("home")]
    public async Task<IActionResult> Home([FromQuery] string? group = "Inmuebles", [FromQuery] string? mode = "Galeria", [FromQuery] string? query = null, [FromQuery] string? flash = null)
    {
        var selectedGroup = PublicationGroupExtensions.ParseOrDefault(group);
        var publications = await publicationService.SearchActivePublicationsAsync(selectedGroup, query);
        var model = new HomeContentViewModel
        {
            Group = selectedGroup.ToDisplayName(),
            GroupOptions = await publicationGroupTypeService.GetActiveAsync(),
            Mode = string.IsNullOrWhiteSpace(mode) ? "Galeria" : mode,
            Query = query,
            Publications = publications,
            MapTilerKey = configuration["MapTiler:ApiKey"] ?? string.Empty,
            FlashMessage = flash,
            MarkersJson = JsonSerializer.Serialize(publications
                .Where(x => x.Latitude.HasValue && x.Longitude.HasValue)
                .Select(x => new
                {
                    id = x.Id,
                    code = x.ToAdCode(),
                    videoUrl = x.VideoUrl,
                    title = x.Title,
                    image = x.ImageList.FirstOrDefault(),
                    images = x.ImageList.Take(11).ToList(),
                    detailsUrl = $"/Publications/Details/{x.Id}",
                    lat = x.Latitude,
                    lng = x.Longitude,
                    price = $"{x.Currency} {x.Price:N0}"
                }))
        };

        return PartialView("~/Views/Content/Home.cshtml", model);
    }

    [HttpGet("trash")]
    public async Task<IActionResult> Trash()
    {
        var model = new TrashContentViewModel
        {
            Publications = await publicationService.GetReportedPublicationsAsync()
        };

        return PartialView("~/Views/Content/Trash.cshtml", model);
    }

    [HttpGet("details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var model = new PublicationDetailsContentViewModel
        {
            Publication = await publicationService.GetByIdAsync(id),
            MapTilerKey = configuration["MapTiler:ApiKey"] ?? string.Empty
        };

        return PartialView("~/Views/Content/Details.cshtml", model);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        var user = await LoadCurrentUserAsync();
        var suggestedLabel = user?.ArgentineLocality is null
            ? null
            : $"{user.ArgentineLocality.Locality}, {user.ArgentineLocality.Province}, Argentina";
        var defaultGroup = PublicationGroup.Inmuebles;
        var categories = await publicationCategoryService.GetActiveByGroupAsync(defaultGroup);
        var input = new PublicationCreateRequest
        {
            Group = defaultGroup,
            CategoryId = categories.FirstOrDefault()?.Id ?? 0,
            Currency = "ARS",
            ContactName = user?.Name ?? string.Empty,
            ContactPhone = user?.Phone ?? string.Empty,
            ContactEmail = user?.Email,
            Locality = user?.ArgentineLocality?.Locality ?? string.Empty,
            Address = suggestedLabel,
            Latitude = user?.ArgentineLocality?.Latitude,
            Longitude = user?.ArgentineLocality?.Longitude,
            NoLocation = false
        };

        var model = new CreatePublicationContentViewModel
        {
            Input = input,
            GroupOptions = await publicationGroupTypeService.GetActiveAsync(),
            Categories = categories,
            IsAuthenticated = currentUserAccessor.IsAuthenticated,
            RequiresLogin = !currentUserAccessor.IsAuthenticated,
            CurrentUserName = user?.Name ?? User.Identity?.Name,
            CurrentUserEmail = user?.Email,
            CurrentUserPhone = user?.Phone,
            SuggestedLocalityLabel = suggestedLabel,
            MapTilerKey = configuration["MapTiler:ApiKey"] ?? string.Empty
        };

        return PartialView("~/Views/Content/Create.cshtml", model);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories([FromQuery] string? group = null)
    {
        var selectedGroup = PublicationGroupExtensions.ParseOrDefault(group);
        var categories = await publicationCategoryService.GetActiveByGroupAsync(selectedGroup);

        return Ok(categories.Select(x => new
        {
            id = x.Id,
            name = x.Name
        }));
    }

    [HttpPost("report")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Report([FromBody] ReportPublicationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = "Datos invalidos para la denuncia." });
        }

        var reasonExists = await db.PublicationReportReasons.AnyAsync(x => x.Id == request.ReasonId && x.IsActive);
        if (!reasonExists)
        {
            return BadRequest(new { message = "Selecciona un motivo de denuncia valido." });
        }

        await reportService.CreateAsync(request.PublicationId, request.ReasonId, request.Comment);
        return Ok(new { message = "La denuncia fue enviada para revision." });
    }

    [HttpPost("create")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreatePost([FromBody] CreatePublicationApiRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                message = "Revisa los datos del formulario.",
                errors = ModelStateToFieldErrors(ModelState)
            });
        }

        if (!currentUserAccessor.IsAuthenticated || currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para publicar." });
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return Unauthorized(new { message = "No se encontro el usuario autenticado." });
        }

        request.Currency = NormalizeCurrency(request.Currency);
        if (request.NoLocation)
        {
            request.Latitude = null;
            request.Longitude = null;
            request.Locality = string.Empty;
            request.Address = null;
        }
        request.ContactName = user.Name;
        request.ContactPhone = user.Phone;
        request.ContactEmail = user.Email;
        request.PublisherMode = "Account";
        request.VideoUrl = string.IsNullOrWhiteSpace(request.VideoUrl) ? null : request.VideoUrl.Trim();

        var category = await publicationCategoryService.GetActiveByIdAsync(request.CategoryId);
        request.Title = BuildPublicationTitle(category?.Name, request.Locality);

        var errors = await ValidateCreateRequestAsync(request);
        if (errors.Count > 0)
        {
            return BadRequest(new { message = "Revisa los datos del formulario.", errors });
        }

        var result = await publicationService.CreateAsync(request, user.Id);

        return Ok(new
        {
            message = "Publicacion creada.",
            redirectUrl = $"/Publications/Details/{result.Publication.Id}"
        });
    }

    [HttpPost("upload-images")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UploadImages([FromForm] List<IFormFile> files)
    {
        if (!currentUserAccessor.IsAuthenticated)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para subir imagenes." });
        }

        if (files is null || files.Count == 0)
        {
            logger.LogWarning("UploadImages rejected: no files bound for user {UserId}.", currentUserAccessor.UserId);
            return BadRequest(new { message = "Subi al menos una imagen." });
        }

        if (files.Count > 11)
        {
            logger.LogWarning("UploadImages rejected: too many files ({Count}) for user {UserId}.", files.Count, currentUserAccessor.UserId);
            return BadRequest(new { message = "Podes subir hasta 11 imagenes." });
        }

        var fileInfo = files.Select(file => new { file.FileName, file.Length, file.ContentType }).ToList();
        logger.LogInformation("UploadImages received {Count} files for user {UserId}: {@Files}.", files.Count, currentUserAccessor.UserId, fileInfo);

        try
        {
            var urls = await imageStorageService.UploadPublicationImagesAsync(files);
            if (urls.Count == 0)
            {
                logger.LogWarning("UploadImages produced no urls for user {UserId}. File info: {@Files}.", currentUserAccessor.UserId, fileInfo);
                return BadRequest(new { message = "No se pudieron guardar imagenes validas." });
            }

            return Ok(new { urls });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "UploadImages configuration/validation failure for user {UserId}. File info: {@Files}.", currentUserAccessor.UserId, fileInfo);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UploadImages unexpected failure for user {UserId}. File info: {@Files}.", currentUserAccessor.UserId, fileInfo);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("upload-video")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UploadVideo([FromForm] IFormFile? file)
    {
        if (!currentUserAccessor.IsAuthenticated)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para subir videos." });
        }

        if (file is null || file.Length <= 0)
        {
            return BadRequest(new { message = "Subi un video valido." });
        }

        var fileInfo = new { file.FileName, file.Length, file.ContentType };
        logger.LogInformation("UploadVideo received file for user {UserId}: {@File}.", currentUserAccessor.UserId, fileInfo);

        try
        {
            var url = await imageStorageService.UploadPublicationVideoAsync(file);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "UploadVideo validation failure for user {UserId}. File info: {@File}.", currentUserAccessor.UserId, fileInfo);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UploadVideo unexpected failure for user {UserId}. File info: {@File}.", currentUserAccessor.UserId, fileInfo);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    private async Task<ApplicationUser?> LoadCurrentUserAsync()
    {
        if (!currentUserAccessor.IsAuthenticated || currentUserAccessor.UserId is not int userId)
        {
            return null;
        }

        return await db.Users
            .Include(x => x.ArgentineLocality)
            .FirstOrDefaultAsync(x => x.Id == userId);
    }

    private async Task<List<object>> ValidateCreateRequestAsync(CreatePublicationApiRequest request)
    {
        var errors = new List<object>();

        void AddError(string field, string message)
        {
            errors.Add(new { field, message });
        }

        if (!await publicationGroupTypeService.ExistsAsync(request.Group)) AddError("group", "Selecciona el tipo de publicacion.");
        if (!await publicationCategoryService.ExistsAsync(request.Group, request.CategoryId))
        {
            AddError("category", "La categoria no corresponde al tipo elegido.");
        }

        foreach (var dynamicError in await publicationService.ValidateDynamicFieldsAsync(request))
        {
            errors.Add(dynamicError);
        }

        if (request.Price <= 0) AddError("price", "Ingresa un precio mayor a cero.");
        if (string.IsNullOrWhiteSpace(request.Currency)) AddError("currency", "Selecciona la moneda.");
        if (!request.NoLocation && string.IsNullOrWhiteSpace(request.Locality)) AddError("locationSearch", "Indica la ubicacion de la publicacion.");
        if (!request.NoLocation && (request.Latitude is null || request.Longitude is null))
        {
            AddError("locationSearch", "Marca un punto valido en el mapa.");
        }

        if (string.IsNullOrWhiteSpace(request.ShortDescription)) AddError("shortDescription", "Completa la descripcion corta.");
        if (string.IsNullOrWhiteSpace(request.LongDescription)) AddError("longDescription", "Completa la descripcion completa.");
        if (string.IsNullOrWhiteSpace(request.ImagesCsv)) AddError("imagesCsv", "Subi al menos una imagen.");
        if (!string.IsNullOrWhiteSpace(request.VideoUrl)
            && !Uri.IsWellFormedUriString(request.VideoUrl, UriKind.Absolute)
            && !request.VideoUrl.StartsWith("/", StringComparison.Ordinal))
        {
            AddError("videoUrl", "El video principal no tiene una URL valida.");
        }

        return errors;
    }

    private static List<object> ModelStateToFieldErrors(ModelStateDictionary modelState)
    {
        var errors = new List<object>();

        foreach (var entry in modelState)
        {
            if (entry.Value?.Errors.Count is not > 0)
            {
                continue;
            }

            var field = NormalizeModelStateFieldName(entry.Key);
            if (string.IsNullOrWhiteSpace(field))
            {
                continue;
            }

            foreach (var error in entry.Value.Errors)
            {
                var message = string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Campo inválido." : error.ErrorMessage;
                errors.Add(new { field, message });
            }
        }

        return errors;
    }

    private static string NormalizeModelStateFieldName(string field)
    {
        var raw = field.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        if (raw.StartsWith("$."))
        {
            raw = raw[2..];
        }

        if (raw.Contains('.'))
        {
            raw = raw[(raw.LastIndexOf('.') + 1)..];
        }

        return raw switch
        {
            "Group" => "group",
            "CategoryId" => "category",
            "Category" => "category",
            "Price" => "price",
            "Currency" => "currency",
            "Locality" => "locationSearch",
            "Latitude" => "locationSearch",
            "Longitude" => "locationSearch",
            "ShortDescription" => "shortDescription",
            "LongDescription" => "longDescription",
            "ImagesCsv" => "imagesCsv",
            "VideoUrl" => "videoUrl",
            _ when raw.Length == 1 => raw.ToLowerInvariant(),
            _ => char.ToLowerInvariant(raw[0]) + raw[1..]
        };
    }

    private static string NormalizeCurrency(string? currency)
    {
        return string.Equals(currency, "ARS", StringComparison.OrdinalIgnoreCase) ? "ARS" : "USD";
    }

    private static string BuildPublicationTitle(string? categoryName, string? locality)
    {
        var category = categoryName?.Trim();
        var city = locality?.Trim();

        if (!string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(city))
        {
            return $"{category} en {city}";
        }

        return category ?? "Nueva publicacion";
    }
}
