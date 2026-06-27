using Ubika.Models;

namespace Ubika.ViewModels;

public class CreatePublicationContentViewModel
{
    public PublicationCreateRequest Input { get; set; } = new();
    public bool IsGoogleEnabled { get; set; }
    public bool IsAuthenticated { get; set; }
    public string? CurrentUserName { get; set; }
}
