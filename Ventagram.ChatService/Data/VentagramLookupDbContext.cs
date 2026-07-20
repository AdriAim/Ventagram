using Microsoft.EntityFrameworkCore;
using Ventagram.ChatService.Models;

namespace Ventagram.ChatService.Data;

public class VentagramLookupDbContext(DbContextOptions<VentagramLookupDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<PublicationMedia> PublicationMedia => Set<PublicationMedia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>().ToTable("Users");
        modelBuilder.Entity<Publication>().ToTable("Publications");
        modelBuilder.Entity<PublicationMedia>().ToTable("PublicationMedia");

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
    }
}
