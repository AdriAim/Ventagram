using System.ComponentModel.DataAnnotations;

namespace Ubika.ViewModels;

public class ReportPublicationRequest
{
    [Required]
    public int PublicationId { get; set; }

    [Required]
    public string Reason { get; set; } = "No corresponde";
}
