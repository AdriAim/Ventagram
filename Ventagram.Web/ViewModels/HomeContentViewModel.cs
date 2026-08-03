using Ventagram.Models;

namespace Ventagram.ViewModels;

public class HomeContentViewModel
{
    public string MapStyleUrl { get; set; } = string.Empty;
    public string Group { get; set; } = "Inmuebles";
    public List<PublicationGroupType> GroupOptions { get; set; } = [];
    public string Mode { get; set; } = "Galeria";
    public string? Query { get; set; }
    public decimal? PriceFrom { get; set; }
    public decimal? PriceTo { get; set; }
    public decimal PriceSliderMax { get; set; } = 1000000m;
    public List<PublicationCategoryField> RequiredFilterFields { get; set; } = [];
    public List<PublicationFieldSearchFilter> SelectedFieldFilters { get; set; } = [];
    public string MapTilesUrlTemplate { get; set; } = string.Empty;
    public string MapAttributionHtml { get; set; } = string.Empty;
    public string MapGeocodingSearchUrlTemplate { get; set; } = string.Empty;
    public string MapReverseGeocodingUrlTemplate { get; set; } = string.Empty;
    public string MarkersJson { get; set; } = "[]";
    public List<Publication> Publications { get; set; } = [];
    public string? FlashMessage { get; set; }
    public string GalleryApiEndpoint { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalResults { get; set; }
    public int TotalPages { get; set; }
    public string? UserLocalityLabel { get; set; }
    public double? UserLocalityLatitude { get; set; }
    public double? UserLocalityLongitude { get; set; }
    public bool CanManageFavorites { get; set; }
    public HashSet<int> FavoritePublicationIds { get; set; } = [];
    public List<FavoriteListSummaryViewModel> FavoriteLists { get; set; } = [];
}
