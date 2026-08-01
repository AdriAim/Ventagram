using System.ComponentModel.DataAnnotations;

namespace Ventagram.Models;

public class SubmitSuggestionRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 5)]
    public string Message { get; set; } = string.Empty;
}
