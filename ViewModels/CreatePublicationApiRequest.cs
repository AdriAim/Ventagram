using System.ComponentModel.DataAnnotations;
using Ventagram.Models;

namespace Ventagram.ViewModels;

public class CreatePublicationApiRequest : PublicationCreateRequest
{
    [EmailAddress]
    public string? AccountEmail { get; set; }
    public string? AccountName { get; set; }
    public string? AccountPhone { get; set; }
    public string? AccountPassword { get; set; }
}
