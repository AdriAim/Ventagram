using System.Globalization;
using System.Text;
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
    PublicationCategoryFieldService publicationCategoryFieldService,
    ReportService reportService,
    FavoriteService favoriteService,
    SuggestionService suggestionService,
    CloudflareR2ImageStorageService imageStorageService,
    CurrentUserAccessor currentUserAccessor,
    NavigationLocalityService navigationLocalityService,
    VentagramDbContext db,
    ILogger<ContentController> logger,
    IConfiguration configuration) : Controller
{
    private const int MaxMapPublications = 250;

    [HttpGet("home")]
    public async Task<IActionResult> Home([FromQuery] string? group = "Inmuebles", [FromQuery] string? mode = "Galeria", [FromQuery] string? query = null, [FromQuery] string? flash = null, [FromQuery] decimal? priceFrom = null, [FromQuery] decimal? priceTo = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var selectedGroup = ParseGroupFilter(group);
        var selectedGroupName = selectedGroup?.ToDisplayName() ?? "Todos";
        var selectedMode = NormalizeBrowseMode(mode);
        var safePageSize = NormalizeTextPageSize(pageSize);
        var safePage = Math.Max(1, page);
        var publications = new List<Publication>();
        var totalResults = 0;
        var effectiveLocality = await navigationLocalityService.GetEffectiveLocalityAsync(HttpContext);
        var userLocalityLabel = effectiveLocality?.DisplayLabel;
        var userLocalityLatitude = effectiveLocality?.Latitude;
        var userLocalityLongitude = effectiveLocality?.Longitude;
        var favoritePublicationIds = new HashSet<int>();
        var favoriteLists = new List<FavoriteListSummaryViewModel>();
        var filters = BuildSearchFilters(priceFrom, priceTo, Request.Query);
        var priceSliderMax = NormalizePriceSliderMax(await publicationService.GetActiveMaxPriceAsync(selectedGroup), filters);
        List<PublicationCategoryField> requiredFields = selectedGroup is null
            ? []
            : await publicationCategoryFieldService.GetRequiredActiveByGroupAsync(selectedGroup.Value);

        if (selectedMode == "Texto")
        {
            totalResults = await publicationService.CountActivePublicationsAsync(selectedGroup, query, filters);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalResults / (double)safePageSize));
            safePage = Math.Min(safePage, totalPages);
            publications = await publicationService.SearchActivePublicationsPageAsync(
                selectedGroup,
                query,
                (safePage - 1) * safePageSize,
                safePageSize,
                userLocalityLatitude,
                userLocalityLongitude,
                filters);
        }
        else if (selectedMode == "Mapa")
        {
            publications = await publicationService.SearchActivePublicationsAsync(
                selectedGroup,
                query,
                userLocalityLatitude,
                userLocalityLongitude,
                filters);
            totalResults = publications.Count;
            publications = LimitMapPublications(publications, MaxMapPublications);
        }

        if (currentUserAccessor.UserId is int currentUserId)
        {
            favoritePublicationIds = await favoriteService.GetFavoritePublicationIdsAsync(currentUserId, publications.Select(x => x.Id));
            favoriteLists = await favoriteService.GetListSummariesAsync(currentUserId);
        }

        var computedTotalPages = selectedMode == "Texto"
            ? Math.Max(1, (int)Math.Ceiling(totalResults / (double)safePageSize))
            : (publications.Count > 0 ? 1 : 0);
        var model = new HomeContentViewModel
        {
            Group = selectedGroupName,
            GroupOptions = await GetBrowseGroupOptionsAsync(),
            Mode = selectedMode,
            Query = query,
            PriceFrom = filters.PriceFrom,
            PriceTo = filters.PriceTo,
            PriceSliderMax = priceSliderMax,
            RequiredFilterFields = requiredFields,
            SelectedFieldFilters = filters.FieldFilters,
            Publications = publications,
            Page = safePage,
            PageSize = safePageSize,
            TotalResults = totalResults,
            TotalPages = computedTotalPages,
            UserLocalityLabel = userLocalityLabel,
            UserLocalityLatitude = userLocalityLatitude,
            UserLocalityLongitude = userLocalityLongitude,
            CanManageFavorites = currentUserAccessor.IsAuthenticated,
            FavoritePublicationIds = favoritePublicationIds,
            FavoriteLists = favoriteLists,
            MapTilerKey = configuration["MapTiler:ApiKey"] ?? string.Empty,
            FlashMessage = flash,
            GalleryApiEndpoint = BuildGalleryApiEndpoint(selectedGroupName, query, filters),
            MarkersJson = JsonSerializer.Serialize(publications
                .Where(x => x.Latitude.HasValue && x.Longitude.HasValue)
                .Select(x => new
                {
                    id = x.Id,
                    code = x.ToAdCode(),
                    videoUrl = x.PrimaryVideoUrl,
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

    [HttpGet("browse")]
    public async Task<IActionResult> Browse([FromQuery] string? group = "Inmuebles", [FromQuery] string? mode = "Galeria", [FromQuery] string? query = null, [FromQuery] string? flash = null, [FromQuery] decimal? priceFrom = null, [FromQuery] decimal? priceTo = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var selectedGroup = ParseGroupFilter(group);
        var selectedGroupName = selectedGroup?.ToDisplayName() ?? "Todos";
        var selectedMode = NormalizeBrowseMode(mode);
        var safePageSize = NormalizeTextPageSize(pageSize);
        var safePage = Math.Max(1, page);
        var publications = new List<Publication>();
        var totalResults = 0;
        var effectiveLocality = await navigationLocalityService.GetEffectiveLocalityAsync(HttpContext);
        var userLocalityLabel = effectiveLocality?.DisplayLabel;
        var userLocalityLatitude = effectiveLocality?.Latitude;
        var userLocalityLongitude = effectiveLocality?.Longitude;
        var favoritePublicationIds = new HashSet<int>();
        var favoriteLists = new List<FavoriteListSummaryViewModel>();
        var filters = BuildSearchFilters(priceFrom, priceTo, Request.Query);
        var priceSliderMax = NormalizePriceSliderMax(await publicationService.GetActiveMaxPriceAsync(selectedGroup), filters);
        List<PublicationCategoryField> requiredFields = selectedGroup is null
            ? []
            : await publicationCategoryFieldService.GetRequiredActiveByGroupAsync(selectedGroup.Value);

        if (selectedMode == "Texto")
        {
            totalResults = await publicationService.CountActivePublicationsAsync(selectedGroup, query, filters);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalResults / (double)safePageSize));
            safePage = Math.Min(safePage, totalPages);
            publications = await publicationService.SearchActivePublicationsPageAsync(
                selectedGroup,
                query,
                (safePage - 1) * safePageSize,
                safePageSize,
                userLocalityLatitude,
                userLocalityLongitude,
                filters);
        }
        else if (selectedMode == "Mapa")
        {
            publications = await publicationService.SearchActivePublicationsAsync(
                selectedGroup,
                query,
                userLocalityLatitude,
                userLocalityLongitude,
                filters);
            totalResults = publications.Count;
            publications = LimitMapPublications(publications, MaxMapPublications);
        }

        if (currentUserAccessor.UserId is int currentUserId)
        {
            favoritePublicationIds = await favoriteService.GetFavoritePublicationIdsAsync(currentUserId, publications.Select(x => x.Id));
            favoriteLists = await favoriteService.GetListSummariesAsync(currentUserId);
        }

        var computedTotalPages = selectedMode == "Texto"
            ? Math.Max(1, (int)Math.Ceiling(totalResults / (double)safePageSize))
            : (publications.Count > 0 ? 1 : 0);
        var model = new HomeContentViewModel
        {
            Group = selectedGroupName,
            GroupOptions = await GetBrowseGroupOptionsAsync(),
            Mode = selectedMode,
            Query = query,
            PriceFrom = filters.PriceFrom,
            PriceTo = filters.PriceTo,
            PriceSliderMax = priceSliderMax,
            RequiredFilterFields = requiredFields,
            SelectedFieldFilters = filters.FieldFilters,
            Publications = publications,
            Page = safePage,
            PageSize = safePageSize,
            TotalResults = totalResults,
            TotalPages = computedTotalPages,
            UserLocalityLabel = userLocalityLabel,
            UserLocalityLatitude = userLocalityLatitude,
            UserLocalityLongitude = userLocalityLongitude,
            CanManageFavorites = currentUserAccessor.IsAuthenticated,
            FavoritePublicationIds = favoritePublicationIds,
            FavoriteLists = favoriteLists,
            MapTilerKey = configuration["MapTiler:ApiKey"] ?? string.Empty,
            FlashMessage = flash,
            GalleryApiEndpoint = BuildGalleryApiEndpoint(selectedGroupName, query, filters),
            MarkersJson = JsonSerializer.Serialize(publications
                .Where(x => x.Latitude.HasValue && x.Longitude.HasValue)
                .Select(x => new
                {
                    id = x.Id,
                    code = x.ToAdCode(),
                    videoUrl = x.PrimaryVideoUrl,
                    title = x.Title,
                    image = x.ImageList.FirstOrDefault(),
                    images = x.ImageList.Take(11).ToList(),
                    detailsUrl = $"/Publications/Details/{x.Id}",
                    lat = x.Latitude,
                    lng = x.Longitude,
                    price = $"{x.Currency} {x.Price:N0}"
                }))
        };

        return PartialView("~/Views/Content/Browse.cshtml", model);
    }

    [HttpGet("gallery-items")]
    public async Task<IActionResult> GalleryItems([FromQuery] string? group = "Inmuebles", [FromQuery] string? query = null, [FromQuery] decimal? priceFrom = null, [FromQuery] decimal? priceTo = null, [FromQuery] int offset = 0, [FromQuery] int limit = 20)
    {
        var effectiveLocality = await navigationLocalityService.GetEffectiveLocalityAsync(HttpContext);
        var selectedGroup = ParseGroupFilter(group);
        var safeOffset = Math.Max(0, offset);
        var safeLimit = Math.Clamp(limit, 1, 60);
        var filters = BuildSearchFilters(priceFrom, priceTo, Request.Query);
        var items = await publicationService.SearchActivePublicationsPageAsync(
            selectedGroup,
            query,
            safeOffset,
            safeLimit + 1,
            effectiveLocality?.Latitude,
            effectiveLocality?.Longitude,
            filters);
        var hasMore = items.Count > safeLimit;
        var payloadItems = items.Take(safeLimit).ToList();
        var favoritePublicationIds = currentUserAccessor.UserId is int currentUserId
            ? await favoriteService.GetFavoritePublicationIdsAsync(currentUserId, payloadItems.Select(x => x.Id))
            : [];
        var payload = payloadItems
            .Select(item => MapGalleryItem(item, favoritePublicationIds.Contains(item.Id)))
            .ToList();

        return Ok(new
        {
            items = payload,
            hasMore,
            nextOffset = safeOffset + payload.Count
        });
    }

    [HttpGet("favorite-lists")]
    public async Task<IActionResult> FavoriteLists()
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para usar favoritos." });
        }

        var lists = await favoriteService.GetListSummariesAsync(userId);
        return Ok(new { lists });
    }

    [HttpGet("favorite-lists/{listId:int}")]
    public async Task<IActionResult> FavoriteListItems(int listId)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para usar favoritos." });
        }

        var result = await favoriteService.GetListContentAsync(userId, listId);
        if (result is null)
        {
            return NotFound(new { message = "La lista no existe." });
        }

        var (summary, publications) = result.Value;
        return Ok(new
        {
            list = summary,
            items = publications.Select(item => MapGalleryItem(item, true)).ToList()
        });
    }

    [HttpPost("favorites")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AddFavorite([FromBody] AddFavoriteRequest request)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized(new { message = "Tenes que iniciar sesion para guardar favoritos." });
        }

        if (request.PublicationId <= 0)
        {
            return BadRequest(new { message = "El anuncio indicado no es valido." });
        }

        try
        {
            var result = await favoriteService.AddFavoriteAsync(
                userId,
                request.PublicationId,
                request.ListId,
                request.NewListName,
                request.SuggestedListName);

            return Ok(new
            {
                message = result.Added
                    ? $"Guardado en {result.List.Name}."
                    : $"El anuncio ya estaba en {result.List.Name}.",
                listId = result.List.Id,
                listName = result.List.Name
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("suggestions")]
    public async Task<IActionResult> SubmitSuggestion([FromBody] SubmitSuggestionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = "Escribe una sugerencia válida." });
        }

        ApplicationUser? user = null;
        if (currentUserAccessor.UserId is int userId)
        {
            user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == userId);
        }

        var result = await suggestionService.SubmitAsync(
            user?.Id,
            user?.Name,
            user?.Email,
            request.Message);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new { message = result.Message });
    }

    [HttpGet("trash")]
    public async Task<IActionResult> Trash()
    {
        var model = new TrashContentViewModel
        {
            Publications = await publicationService.GetModerationQueueAsync()
        };

        return PartialView("~/Views/Content/Trash.cshtml", model);
    }

    [HttpGet("details/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var model = new PublicationDetailsContentViewModel
        {
            Publication = await publicationService.GetByIdAsync(id),
            MapTilerKey = configuration["MapTiler:ApiKey"] ?? string.Empty,
            IsAuthenticated = currentUserAccessor.IsAuthenticated,
            CurrentUserId = currentUserAccessor.UserId
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
            CategoryId = 0,
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
            PublishingBlocked = user is not null && !user.CanPublish,
            PublishingBlockedMessage = user is not null && !user.CanPublish
                ? "Tu cuenta no puede publicar nuevos anuncios hasta que un administrador revise el anuncio denunciado."
                : null,
            MapTilerKey = configuration["MapTiler:ApiKey"] ?? string.Empty
        };

        return PartialView("~/Views/Content/Create.cshtml", model);
    }

    [HttpGet("edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        if (!currentUserAccessor.IsAuthenticated || currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized();
        }

        var user = await LoadCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var publication = await publicationService.GetOwnedByIdAsync(id, userId);
        if (publication is null)
        {
            return NotFound();
        }

        var categories = await publicationCategoryService.GetActiveByGroupAsync(publication.Group);
        var input = new PublicationCreateRequest
        {
            Group = publication.Group,
            CategoryId = publication.CategoryId,
            Title = publication.Title,
            Price = publication.Price,
            Currency = publication.Currency,
            Locality = publication.Locality,
            ShortDescription = publication.ShortDescription,
            LongDescription = publication.LongDescription,
            ImagesCsv = string.Join(",", publication.ImageList),
            VideoUrl = publication.PrimaryVideoUrl,
            ContactName = publication.ContactName,
            ContactPhone = publication.ContactPhone,
            ContactEmail = publication.ContactEmail,
            Featured = publication.Featured,
            InternalNotes = publication.InternalNotes,
            Latitude = publication.Latitude,
            Longitude = publication.Longitude,
            NoLocation = !publication.Latitude.HasValue || !publication.Longitude.HasValue,
            DynamicFields = publication.FieldValues
                .Select(x => new PublicationDynamicFieldInput
                {
                    FieldId = x.CategoryFieldId,
                    ValueText = x.ValueText,
                    ValueNumber = x.ValueNumber,
                    ValueBoolean = x.ValueBoolean
                })
                .ToList()
        };

        var hasTechnicalValues = publication.FieldValues.Any(x =>
            x.CategoryField is not null
            && !(x.CategoryField.ShowInBasicData || x.CategoryField.Required)
            && (x.ValueBoolean.HasValue || x.ValueNumber.HasValue || !string.IsNullOrWhiteSpace(x.ValueText)));

        var model = new CreatePublicationContentViewModel
        {
            Input = input,
            GroupOptions = await publicationGroupTypeService.GetActiveAsync(),
            Categories = categories,
            IsAuthenticated = true,
            RequiresLogin = false,
            CurrentUserName = user.Name,
            CurrentUserEmail = user.Email,
            CurrentUserPhone = user.Phone,
            SuggestedLocalityLabel = string.IsNullOrWhiteSpace(publication.Locality) ? null : publication.Locality,
            MapTilerKey = configuration["MapTiler:ApiKey"] ?? string.Empty,
            FormEyebrow = "Mis anuncios",
            FormTitle = "Editar anuncio",
            FormDescription = "Modifica los mismos datos que usas al crear un anuncio, incluyendo imagenes, video, ubicacion y ficha tecnica.",
            SubmitButtonText = "Guardar cambios",
            CancelUrl = "/MisPublicaciones",
            SubmitEndpoint = $"/api/content/edit/{publication.Id}",
            ShowLocationSection = publication.Latitude.HasValue && publication.Longitude.HasValue,
            ShowTechnicalSection = hasTechnicalValues,
            InitialDynamicFieldValues = publication.FieldValues
                .Select(x => new CreatePublicationDynamicFieldValueSeed
                {
                    InternalName = x.CategoryField?.InternalName ?? string.Empty,
                    Value = x.ValueBoolean.HasValue
                        ? x.ValueBoolean.Value.ToString().ToLowerInvariant()
                        : x.ValueNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? x.ValueText ?? string.Empty
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.InternalName))
                .ToList()
        };

        return PartialView("~/Views/Content/Create.cshtml", model);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories([FromQuery] string? group = null)
    {
        var selectedGroup = ParseGroupFilter(group) ?? PublicationGroup.Inmuebles;
        var categories = await publicationCategoryService.GetActiveByGroupAsync(selectedGroup);

        return Ok(categories.Select(x => new
        {
            id = x.Id,
            name = x.Name
        }));
    }

    [HttpGet("required-filter-fields")]
    public async Task<IActionResult> RequiredFilterFields([FromQuery] string? group = null)
    {
        var selectedGroup = ParseGroupFilter(group);
        if (selectedGroup is null)
        {
            return Ok(Array.Empty<object>());
        }

        var fields = await publicationCategoryFieldService.GetRequiredActiveByGroupAsync(selectedGroup.Value);
        return Ok(fields.Select(x => new
        {
            id = x.Id,
            label = x.Label,
            internalName = x.InternalName,
            dataType = SplitCsvOptions(x.OptionsCsv).Length > 0
                ? PublicationCategoryFieldDataType.Lista.ToString().ToLowerInvariant()
                : x.DataType.ToString().ToLowerInvariant(),
            required = x.Required,
            options = SplitCsvOptions(x.OptionsCsv)
        }));
    }

    [HttpPost("report")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Report([FromBody] ReportPublicationRequest request)
    {
        if (currentUserAccessor.UserId is not int userId)
        {
            return Unauthorized(new { message = "Debes iniciar sesión para denunciar un anuncio." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = "Datos invalidos para la denuncia." });
        }

        var reasonExists = await db.PublicationReportReasons.AnyAsync(x => x.Id == request.ReasonId && x.IsActive);
        if (!reasonExists)
        {
            return BadRequest(new { message = "Selecciona un motivo de denuncia valido." });
        }

        var result = await reportService.CreateAsync(request.PublicationId, userId, request.ReasonId, request.Comment);
        if (!result.Success)
        {
            return StatusCode(result.StatusCode, new { message = result.Message });
        }

        return Ok(new { message = result.Message });
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

        if (!user.CanPublish)
        {
            return StatusCode(403, new { message = "No puedes publicar nuevos anuncios hasta que un administrador revise el anuncio denunciado." });
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
            message = "Anuncio creado.",
            redirectUrl = $"/Publications/Details/{result.Publication.Id}"
        });
    }

    [HttpPost("edit/{id:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> EditPost(int id, [FromBody] CreatePublicationApiRequest request)
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
            return Unauthorized(new { message = "Tenes que iniciar sesion para editar." });
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return Unauthorized(new { message = "No se encontro el usuario autenticado." });
        }

        var publication = await publicationService.GetOwnedByIdAsync(id, userId);
        if (publication is null)
        {
            return NotFound(new { message = "El anuncio no existe o no te pertenece." });
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

        var updated = await publicationService.UpdateOwnedAsync(id, userId, request);
        if (!updated)
        {
            return BadRequest(new { message = "No se pudo actualizar el anuncio." });
        }

        return Ok(new
        {
            message = "Anuncio actualizado.",
            redirectUrl = $"/Publications/Details/{id}"
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

        if (!await publicationGroupTypeService.ExistsAsync(request.Group)) AddError("group", "Selecciona el tipo de anuncio.");

        var categoryExists = false;
        if (request.CategoryId <= 0)
        {
            AddError("category", "Selecciona una categoria.");
        }
        else
        {
            categoryExists = await publicationCategoryService.ExistsAsync(request.Group, request.CategoryId);
            if (!categoryExists)
            {
                AddError("category", "La categoria no corresponde al tipo elegido.");
            }
        }

        if (categoryExists)
        {
            foreach (var dynamicError in await publicationService.ValidateDynamicFieldsAsync(request))
            {
                errors.Add(dynamicError);
            }
        }

        if (request.Price <= 0) AddError("price", "Ingresa un precio mayor a cero.");
        if (string.IsNullOrWhiteSpace(request.Currency)) AddError("currency", "Selecciona la moneda.");
        if (!request.NoLocation && string.IsNullOrWhiteSpace(request.Locality)) AddError("locationSearch", "Indica la ubicacion del anuncio.");
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

    private static PublicationSearchFilters BuildSearchFilters(decimal? priceFrom, decimal? priceTo, IQueryCollection query)
    {
        var filters = new PublicationSearchFilters
        {
            PriceFrom = priceFrom is >= 0 ? priceFrom : null,
            PriceTo = priceTo is >= 0 ? priceTo : null
        };

        var fieldIds = query["filterFieldId"];
        var values = query["filterValue"];
        var count = Math.Max(fieldIds.Count, values.Count);
        for (var i = 0; i < count; i++)
        {
            var rawFieldId = i < fieldIds.Count ? fieldIds[i] : null;
            var rawValue = i < values.Count ? values[i] : null;
            if (!int.TryParse(rawFieldId, out var fieldId) || fieldId <= 0 || string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            filters.FieldFilters.Add(new PublicationFieldSearchFilter
            {
                FieldId = fieldId,
                Value = rawValue.Trim()
            });
        }

        return filters;
    }

    private static decimal NormalizePriceSliderMax(decimal activeMaxPrice, PublicationSearchFilters filters)
    {
        var effectiveMax = new[]
        {
            activeMaxPrice,
            filters.PriceFrom ?? 0m,
            filters.PriceTo ?? 0m,
            100000m
        }.Max();

        return Math.Ceiling(effectiveMax / 10000m) * 10000m;
    }

    private static string BuildGalleryApiEndpoint(string group, string? query, PublicationSearchFilters filters)
    {
        var parts = new List<string>
        {
            $"group={Uri.EscapeDataString(group)}",
            $"query={Uri.EscapeDataString(query ?? string.Empty)}"
        };

        AddFilterQueryParts(parts, filters);
        return $"/api/content/gallery-items?{string.Join("&", parts)}";
    }

    private async Task<List<PublicationGroupType>> GetBrowseGroupOptionsAsync()
    {
        var groups = await publicationGroupTypeService.GetActiveAsync();
        if (groups.Any(x => string.Equals(x.Name, "Todos", StringComparison.OrdinalIgnoreCase)))
        {
            return groups;
        }

        return
        [
            new PublicationGroupType
            {
                Id = 0,
                Name = "Todos",
                SortOrder = int.MinValue,
                IsActive = true
            },
            .. groups
        ];
    }

    private static void AddFilterQueryParts(List<string> parts, PublicationSearchFilters filters)
    {
        if (filters.PriceFrom is decimal priceFrom)
        {
            parts.Add($"priceFrom={Uri.EscapeDataString(priceFrom.ToString(CultureInfo.InvariantCulture))}");
        }

        if (filters.PriceTo is decimal priceTo)
        {
            parts.Add($"priceTo={Uri.EscapeDataString(priceTo.ToString(CultureInfo.InvariantCulture))}");
        }

        foreach (var filter in filters.FieldFilters)
        {
            if (filter.FieldId <= 0 || string.IsNullOrWhiteSpace(filter.Value))
            {
                continue;
            }

            parts.Add($"filterFieldId={filter.FieldId.ToString(CultureInfo.InvariantCulture)}");
            parts.Add($"filterValue={Uri.EscapeDataString(filter.Value)}");
        }
    }

    private static string[] SplitCsvOptions(string? optionsCsv)
    {
        return string.IsNullOrWhiteSpace(optionsCsv)
            ? []
            : optionsCsv.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string BuildPublicationTitle(string? categoryName, string? locality)
    {
        var category = categoryName?.Trim();
        var city = locality?.Trim();

        if (!string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(city))
        {
            return $"{category} en {city}";
        }

        return category ?? "Nuevo anuncio";
    }

    private static int NormalizeTextPageSize(int pageSize)
    {
        return pageSize switch
        {
            100 => 100,
            200 => 200,
            _ => 50
        };
    }

    private static string NormalizeBrowseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Galeria";
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var buffer = new char[normalized.Length];
        var length = 0;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            buffer[length++] = char.ToLowerInvariant(character);
        }

        var compactValue = new string(buffer, 0, length);
        return compactValue switch
        {
            "mapa" => "Mapa",
            "texto" => "Texto",
            "galeria" => "Galeria",
            _ => "Galeria"
        };
    }

    private static PublicationGroup? ParseGroupFilter(string? value)
    {
        return string.Equals(value?.Trim(), "Todos", StringComparison.OrdinalIgnoreCase)
            ? null
            : PublicationGroupExtensions.ParseOrDefault(value);
    }

    /// <summary>
    /// Limita la cantidad de publicaciones que salen en la vista de mapa.
    ///
    /// Qué resuelve:
    /// - Evita entregar cientos o miles de markers de una sola vez.
    /// - Mantiene más liviana la serialización del JSON del mapa.
    /// - Reduce trabajo de render del navegador y evita que el mapa se vuelva tosco al moverlo.
    ///
    /// Criterio aplicado:
    /// - Solo conserva publicaciones con coordenadas válidas.
    /// - Prioriza destacadas primero.
    /// - Dentro de ese grupo, prioriza las más recientes.
    /// - Finalmente corta en un máximo fijo.
    ///
    /// La idea es que el mapa no intente mostrar "todo", sino una muestra útil y estable.
    /// Si en el futuro se quiere una política mejor, este es el único punto a reemplazar
    /// por lógica de bounding box, zoom, clustering o relevancia geográfica.
    /// </summary>
    private static List<Publication> LimitMapPublications(IEnumerable<Publication> publications, int maxItems)
    {
        return publications
            .Where(x => x.Latitude.HasValue && x.Longitude.HasValue)
            .OrderByDescending(x => x.Featured)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(Math.Max(1, maxItems))
            .ToList();
    }

    private static object MapGalleryItem(Publication item, bool isFavorite)
    {
        var images = item.ImageList
            .Take(11)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return new
        {
            id = item.Id,
            title = item.Title,
            galleryTitle = item.Title.Split(" - oportunidad", StringSplitOptions.TrimEntries)[0],
            publicationCode = item.ToAdCode(),
            price = $"{item.Currency} {item.Price:N0}",
            detailsUrl = $"/Publications/Details/{item.Id}",
            videoUrl = item.PrimaryVideoUrl,
            images,
            groupName = item.Group.ToDisplayName(),
            isFavorite
        };
    }
}
