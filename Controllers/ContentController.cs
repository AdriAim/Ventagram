using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ubika.Data;
using Ubika.Models;
using Ubika.Services;
using Ubika.ViewModels;

namespace Ubika.Controllers;

[ApiController]
[Route("api/content")]
public class ContentController(
    PublicationService publicationService,
    ReportService reportService,
    AuthService authService,
    CurrentUserAccessor currentUserAccessor,
    UbikaDbContext db,
    IConfiguration configuration) : Controller
{
    [HttpGet("home")]
    public async Task<IActionResult> Home([FromQuery] string? group = "Inmuebles", [FromQuery] string? mode = "Galeria", [FromQuery] string? query = null, [FromQuery] string? flash = null)
    {
        var publications = await publicationService.SearchActivePublicationsAsync(group, query);
        var model = new HomeContentViewModel
        {
            Group = string.IsNullOrWhiteSpace(group) ? "Inmuebles" : group,
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
                    title = x.Title,
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
            Publication = await publicationService.GetByIdAsync(id)
        };

        return PartialView("~/Views/Content/Details.cshtml", model);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create([FromQuery] string? group = "Inmuebles", [FromQuery] string? publisherMode = "Anonymous")
    {
        var input = new PublicationCreateRequest
        {
            Group = string.IsNullOrWhiteSpace(group) ? "Inmuebles" : group,
            PublisherMode = string.IsNullOrWhiteSpace(publisherMode) ? "Anonymous" : publisherMode,
            Currency = "USD"
        };

        if (currentUserAccessor.IsAuthenticated && currentUserAccessor.UserId is int userId)
        {
            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (user is not null)
            {
                input.ContactName = user.Name;
                input.ContactPhone = user.Phone;
                input.ContactEmail = user.Email;
            }
        }

        var model = new CreatePublicationContentViewModel
        {
            Input = input,
            IsGoogleEnabled = !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]),
            IsAuthenticated = currentUserAccessor.IsAuthenticated,
            CurrentUserName = User.Identity?.Name
        };

        return PartialView("~/Views/Content/Create.cshtml", model);
    }

    [HttpPost("report")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Report([FromBody] ReportPublicationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { message = "Datos inválidos para la denuncia." });
        }

        await reportService.CreateAsync(request.PublicationId, request.Reason);
        return Ok(new { message = "La denuncia fue enviada para revisión." });
    }

    [HttpPost("create")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreatePost([FromBody] CreatePublicationApiRequest request)
    {
        int? userId = currentUserAccessor.UserId;
        if (request.PublisherMode == "Account" && !currentUserAccessor.IsAuthenticated)
        {
            var register = await authService.RegisterAsync(
                request.AccountName!,
                request.AccountEmail!,
                request.AccountPhone!,
                request.AccountPassword!);

            if (!register.Success || register.User is null)
            {
                return BadRequest(new { message = register.Error ?? "No se pudo crear el usuario." });
            }

            await authService.SignInAsync(register.User);
            userId = register.User.Id;
            request.ContactName = register.User.Name;
            request.ContactPhone = register.User.Phone;
            request.ContactEmail = register.User.Email;
        }
        else if (request.PublisherMode == "Google" && !currentUserAccessor.IsAuthenticated)
        {
            return BadRequest(new { message = "Primero iniciá sesión con Google o email." });
        }
        else if (request.PublisherMode == "Google" && currentUserAccessor.UserId is int googleUserId)
        {
            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == googleUserId);
            if (user is not null)
            {
                request.ContactName = user.Name;
                request.ContactPhone = user.Phone;
                request.ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? user.Email : request.ContactEmail;
            }
        }

        var errors = ValidateCreateRequest(request);
        if (errors.Count > 0)
        {
            return BadRequest(new { message = "Revisá los datos del formulario.", errors });
        }

        var result = await publicationService.CreateAsync(request, userId);
        if (result.AnonymousPassword is not null)
        {
            return Ok(new
            {
                message = "Publicación creada.",
                anonymousPassword = result.AnonymousPassword,
                publicationId = result.Publication.Id
            });
        }

        return Ok(new
        {
            message = "Publicación creada.",
            redirectUrl = $"/Publications/Details/{result.Publication.Id}"
        });
    }

    private static List<string> ValidateCreateRequest(CreatePublicationApiRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Category)) errors.Add("La categoría es obligatoria.");
        if (string.IsNullOrWhiteSpace(request.Title)) errors.Add("El título es obligatorio.");
        if (request.Price <= 0) errors.Add("El precio debe ser mayor a cero.");
        if (string.IsNullOrWhiteSpace(request.Locality)) errors.Add("La localidad es obligatoria.");
        if (string.IsNullOrWhiteSpace(request.ShortDescription)) errors.Add("La descripción corta es obligatoria.");
        if (string.IsNullOrWhiteSpace(request.ImagesCsv)) errors.Add("Cargá al menos una imagen.");

        if (request.PublisherMode != "Account" && string.IsNullOrWhiteSpace(request.ContactName))
        {
            errors.Add("El nombre de contacto es obligatorio.");
        }

        if (request.PublisherMode != "Account" && string.IsNullOrWhiteSpace(request.ContactPhone))
        {
            errors.Add("El teléfono de contacto es obligatorio.");
        }

        if (request.PublisherMode == "Account")
        {
            if (string.IsNullOrWhiteSpace(request.AccountName)) errors.Add("Ingresá un nombre para la cuenta.");
            if (string.IsNullOrWhiteSpace(request.AccountEmail)) errors.Add("Ingresá un email para la cuenta.");
            if (string.IsNullOrWhiteSpace(request.AccountPhone)) errors.Add("Ingresá un teléfono para la cuenta.");
            if (string.IsNullOrWhiteSpace(request.AccountPassword)) errors.Add("Ingresá una contraseña para la cuenta.");
        }

        if (request.Group == "Inmuebles")
        {
            if (string.IsNullOrWhiteSpace(request.PropertyType)) errors.Add("El tipo de inmueble es obligatorio.");
            if (string.IsNullOrWhiteSpace(request.Operation)) errors.Add("La operación es obligatoria.");
            if (string.IsNullOrWhiteSpace(request.Zone)) errors.Add("La zona es obligatoria.");
            if (request.TotalAreaM2 is null) errors.Add("La superficie total es obligatoria.");
            if (string.IsNullOrWhiteSpace(request.RoomsOrBedrooms)) errors.Add("Ambientes o dormitorios es obligatorio.");
            if (request.Bathrooms is null) errors.Add("Baños es obligatorio.");
        }
        else if (request.Group == "Rodados")
        {
            if (string.IsNullOrWhiteSpace(request.VehicleType)) errors.Add("El tipo de rodado es obligatorio.");
            if (string.IsNullOrWhiteSpace(request.Brand)) errors.Add("La marca es obligatoria.");
            if (string.IsNullOrWhiteSpace(request.Model)) errors.Add("El modelo es obligatorio.");
            if (request.Year is null) errors.Add("El año es obligatorio.");
            if (request.Kilometers is null) errors.Add("Los kilómetros son obligatorios.");
            if (string.IsNullOrWhiteSpace(request.Fuel)) errors.Add("El combustible es obligatorio.");
            if (string.IsNullOrWhiteSpace(request.Transmission)) errors.Add("La transmisión es obligatoria.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Subcategory)) errors.Add("La subcategoría es obligatoria.");
            if (string.IsNullOrWhiteSpace(request.ItemCondition)) errors.Add("El estado del artículo es obligatorio.");
        }

        return errors;
    }
}
