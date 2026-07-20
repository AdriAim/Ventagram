using Microsoft.EntityFrameworkCore;
using Ventagram.ChatService.Models;

namespace Ventagram.ChatService.Data;

public class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<PublicationMedia> PublicationMedia => Set<PublicationMedia>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>().ToTable("Users");
        modelBuilder.Entity<Publication>().ToTable("Publications");
        modelBuilder.Entity<PublicationMedia>().ToTable("PublicationMedia");
        modelBuilder.Entity<ChatConversation>().ToTable("ChatConversations");
        modelBuilder.Entity<ChatMessage>().ToTable("ChatMessages");

        modelBuilder.Entity<Publication>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<PublicationMedia>()
            .HasOne(x => x.Publication)
            .WithMany(x => x.MediaItems)
            .HasForeignKey(x => x.PublicationId);

        modelBuilder.Entity<PublicationMedia>()
            .HasIndex(x => new { x.PublicationId, x.SortOrder })
            .IsUnique();

        modelBuilder.Entity<ChatConversation>()
            .HasOne(x => x.Publication)
            .WithMany(x => x.ChatConversations)
            .HasForeignKey(x => x.PublicationId);

        modelBuilder.Entity<ChatConversation>()
            .HasOne(x => x.BuyerUser)
            .WithMany()
            .HasForeignKey(x => x.BuyerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatConversation>()
            .HasOne(x => x.SellerUser)
            .WithMany()
            .HasForeignKey(x => x.SellerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatConversation>()
            .HasIndex(x => new { x.PublicationId, x.BuyerUserId, x.SellerUserId })
            .IsUnique();

        modelBuilder.Entity<ChatConversation>()
            .HasIndex(x => new { x.BuyerUserId, x.LastMessageAtUtc });

        modelBuilder.Entity<ChatConversation>()
            .HasIndex(x => new { x.SellerUserId, x.LastMessageAtUtc });

        modelBuilder.Entity<ChatMessage>()
            .HasOne(x => x.Conversation)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ConversationId);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(x => x.SenderUser)
            .WithMany()
            .HasForeignKey(x => x.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(x => new { x.ConversationId, x.CreatedAtUtc });

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(x => new { x.ConversationId, x.ReadAtUtc });

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(x => new { x.EmailReminderSentAtUtc, x.CreatedAtUtc });
    }
}
