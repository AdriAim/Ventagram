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
}
