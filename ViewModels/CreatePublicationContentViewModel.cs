using Ventagram.Models;

namespace Ventagram.ViewModels;

public class CreatePublicationContentViewModel
{
    public PublicationCreateRequest Input { get; set; } = new();
    public List<PublicationGroupType> GroupOptions { get; set; } = [];
    public List<PublicationCategory> Categories { get; set; } = [];
    public bool IsAuthenticated { get; set; }
    public bool RequiresLogin { get; set; }
    public string? CurrentUserName { get; set; }
    public string? CurrentUserEmail { get; set; }
    public string? CurrentUserPhone { get; set; }
    public string? SuggestedLocalityLabel { get; set; }
    public string MapTilerKey { get; set; } = string.Empty;
    public string FormEyebrow { get; set; } = "Publicacion nueva";
    public string FormTitle { get; set; } = "Crear publicacion";
    public string FormDescription { get; set; } = "Completando los datos minimos, y subiendo al menos una foto ya puedes publicar tu anuncio.";
    public string SubmitButtonText { get; set; } = "Publicar";
    public string CancelUrl { get; set; } = "/";
    public string SubmitEndpoint { get; set; } = "/api/content/create";
    public bool ShowLocationSection { get; set; }
    public bool ShowTechnicalSection { get; set; }
    public List<CreatePublicationDynamicFieldValueSeed> InitialDynamicFieldValues { get; set; } = [];
}

public class CreatePublicationDynamicFieldValueSeed
{
    public string InternalName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
