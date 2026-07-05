using Ventagram.Models;

namespace Ventagram.ViewModels;

public class PublicationDetailsContentViewModel
{
    public Publication? Publication { get; set; }
    public string MapTilerKey { get; set; } = string.Empty;
}
