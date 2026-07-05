using Microsoft.EntityFrameworkCore;
using Ventagram.Models;

namespace Ventagram.Data;

public class VentagramDbContext(DbContextOptions<VentagramDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<PropertyDetail> PropertyDetails => Set<PropertyDetail>();
    public DbSet<VehicleDetail> VehicleDetails => Set<VehicleDetail>();
    public DbSet<GeneralDetail> GeneralDetails => Set<GeneralDetail>();
    public DbSet<ArgentineLocality> ArgentineLocalities => Set<ArgentineLocality>();
    public DbSet<PublicationGroupType> PublicationGroupTypes => Set<PublicationGroupType>();
    public DbSet<PublicationCategory> PublicationCategories => Set<PublicationCategory>();
    public DbSet<PublicationExtraAttribute> PublicationExtraAttributes => Set<PublicationExtraAttribute>();
    public DbSet<PublicationReportReason> PublicationReportReasons => Set<PublicationReportReason>();
    public DbSet<PublicationReport> PublicationReports => Set<PublicationReport>();

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
            .Property(x => x.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PublicationCategory>()
            .Property(x => x.Group)
            .HasConversion<byte>()
            .HasColumnType("tinyint unsigned");

        modelBuilder.Entity<PublicationCategory>()
            .HasIndex(x => new { x.Group, x.Name })
            .IsUnique();

        modelBuilder.Entity<PublicationReportReason>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<PublicationReportReason>()
            .Property(x => x.Id)
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<PublicationReportReason>()
            .HasIndex(x => x.Name)
            .IsUnique();

        modelBuilder.Entity<PropertyDetail>()
            .Property(x => x.Expenses)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Publication>()
            .HasOne(x => x.PropertyDetail)
            .WithOne(x => x.Publication)
            .HasForeignKey<PropertyDetail>(x => x.PublicationId);

        modelBuilder.Entity<Publication>()
            .HasOne(x => x.VehicleDetail)
            .WithOne(x => x.Publication)
            .HasForeignKey<VehicleDetail>(x => x.PublicationId);

        modelBuilder.Entity<Publication>()
            .HasOne(x => x.GeneralDetail)
            .WithOne(x => x.Publication)
            .HasForeignKey<GeneralDetail>(x => x.PublicationId);

        modelBuilder.Entity<PublicationReport>()
            .HasOne(x => x.Reason)
            .WithMany(x => x.Reports)
            .HasForeignKey(x => x.ReasonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
