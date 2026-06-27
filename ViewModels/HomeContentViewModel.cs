using Ubika.Models;

namespace Ubika.ViewModels;

public class HomeContentViewModel
{
    public string Group { get; set; } = "Inmuebles";
    public string Mode { get; set; } = "Galeria";
    public string? Query { get; set; }
    public string MapTilerKey { get; set; } = string.Empty;
    public string MarkersJson { get; set; } = "[]";
    public List<Publication> Publications { get; set; } = [];
    public string? FlashMessage { get; set; }
}
