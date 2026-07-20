namespace Ventagram.ChatService.Models;

public class ChatConversation
{
    public int Id { get; set; }
    public int PublicationId { get; set; }
    public Publication? Publication { get; set; }
    public int BuyerUserId { get; set; }
    public ApplicationUser? BuyerUser { get; set; }
    public int SellerUserId { get; set; }
    public ApplicationUser? SellerUser { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAtUtc { get; set; }
    public string? LastMessagePreview { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];
}
