using Microsoft.EntityFrameworkCore;
using Ventagram.Models;

namespace Ventagram.Data;

public class VentagramDbContext(DbContextOptions<VentagramDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<PublicationMedia> PublicationMedia => Set<PublicationMedia>();
    public DbSet<ArgentineLocality> ArgentineLocalities => Set<ArgentineLocality>();
    public DbSet<PublicationGroupType> PublicationGroupTypes => Set<PublicationGroupType>();
    public DbSet<PublicationCategory> PublicationCategories => Set<PublicationCategory>();
    public DbSet<PublicationCategoryField> PublicationCategoryFields => Set<PublicationCategoryField>();
    public DbSet<PublicationFieldValue> PublicationFieldValues => Set<PublicationFieldValue>();
    public DbSet<PublicationReportReason> PublicationReportReasons => Set<PublicationReportReason>();
    public DbSet<PublicationReport> PublicationReports => Set<PublicationReport>();
    public DbSet<FavoriteList> FavoriteLists => Set<FavoriteList>();
    public DbSet<FavoriteListItem> FavoriteListItems => Set<FavoriteListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<ApplicationUser>()
            .HasOne(x => x.ArgentineLocality)
            .WithMany()
            .HasForeignKey(x => x.ArgentineLocalityId);

        modelBuilder.Entity<ArgentineLocality>()
            .HasIndex(x => new { x.Province, x.Locality })
            .IsUnique();

        modelBuilder.Entity<PublicationGroupType>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<PublicationGroupType>()
            .Property(x => x.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<PublicationGroupType>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<Publication>()
            .Property(x => x.Group)
            .HasConversion<byte>()
            .HasColumnType("tinyint unsigned");

        modelBuilder.Entity<Publication>()
            .HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId);

        modelBuilder.Entity<Publication>()
            .Property(x => x.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PublicationMedia>()
            .Property(x => x.MediaType)
            .HasConversion<byte>()
            .HasColumnType("tinyint unsigned");

        modelBuilder.Entity<PublicationMedia>()
            .HasOne(x => x.Publication)
            .WithMany(x => x.MediaItems)
            .HasForeignKey(x => x.PublicationId);

        modelBuilder.Entity<PublicationMedia>()
            .HasIndex(x => new { x.PublicationId, x.SortOrder })
            .IsUnique();

        modelBuilder.Entity<PublicationMedia>()
            .HasIndex(x => new { x.PublicationId, x.MediaType, x.IsPrimary });

        modelBuilder.Entity<PublicationCategory>()
            .Property(x => x.Group)
            .HasConversion<byte>()
            .HasColumnType("tinyint unsigned");

        modelBuilder.Entity<PublicationCategory>()
            .HasIndex(x => new { x.Group, x.Name })
            .IsUnique();

        modelBuilder.Entity<PublicationCategoryField>()
            .Property(x => x.DataType)
            .HasConversion<byte>()
            .HasColumnType("tinyint unsigned");

        modelBuilder.Entity<PublicationCategoryField>()
            .Property(x => x.GroupId)
            .HasConversion<byte?>()
            .HasColumnType("tinyint unsigned")
            .HasColumnName("GroupId");

        modelBuilder.Entity<PublicationCategoryField>()
            .HasOne(x => x.Category)
            .WithMany(x => x.Fields)
            .HasForeignKey(x => x.CategoryId);

        modelBuilder.Entity<PublicationCategoryField>()
            .HasIndex(x => new { x.GroupId, x.CategoryId, x.InternalName })
            .IsUnique();

        modelBuilder.Entity<PublicationCategoryField>()
            .HasIndex(x => new { x.GroupId, x.CategoryId, x.IsActive, x.SortOrder });

        modelBuilder.Entity<PublicationFieldValue>()
            .Property(x => x.ValueNumber)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PublicationFieldValue>()
            .HasOne(x => x.Publication)
            .WithMany(x => x.FieldValues)
            .HasForeignKey(x => x.PublicationId);

        modelBuilder.Entity<PublicationFieldValue>()
            .HasOne(x => x.CategoryField)
            .WithMany(x => x.Values)
            .HasForeignKey(x => x.CategoryFieldId);

        modelBuilder.Entity<PublicationFieldValue>()
            .HasIndex(x => new { x.PublicationId, x.CategoryFieldId })
            .IsUnique();

        modelBuilder.Entity<PublicationFieldValue>()
            .HasIndex(x => new { x.CategoryFieldId, x.ValueText });

        modelBuilder.Entity<PublicationFieldValue>()
            .HasIndex(x => new { x.CategoryFieldId, x.ValueNumber });

        modelBuilder.Entity<PublicationFieldValue>()
            .HasIndex(x => new { x.CategoryFieldId, x.ValueBoolean });

        modelBuilder.Entity<PublicationReportReason>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<PublicationReportReason>()
            .Property(x => x.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<PublicationReportReason>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<PublicationReport>()
            .HasOne(x => x.Reason)
            .WithMany(x => x.Reports)
            .HasForeignKey(x => x.ReasonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FavoriteList>()
            .HasOne(x => x.User)
            .WithMany(x => x.FavoriteLists)
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<FavoriteList>()
            .HasIndex(x => new { x.UserId, x.Name })
            .IsUnique();

        modelBuilder.Entity<FavoriteListItem>()
            .HasOne(x => x.FavoriteList)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.FavoriteListId);

        modelBuilder.Entity<FavoriteListItem>()
            .HasOne(x => x.Publication)
            .WithMany(x => x.FavoriteListItems)
            .HasForeignKey(x => x.PublicationId);

        modelBuilder.Entity<FavoriteListItem>()
            .HasIndex(x => new { x.FavoriteListId, x.PublicationId })
            .IsUnique();
    }
}
