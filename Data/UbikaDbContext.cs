using Microsoft.EntityFrameworkCore;
using Ubika.Models;

namespace Ubika.Data;

public class UbikaDbContext(DbContextOptions<UbikaDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<PropertyDetail> PropertyDetails => Set<PropertyDetail>();
    public DbSet<VehicleDetail> VehicleDetails => Set<VehicleDetail>();
    public DbSet<GeneralDetail> GeneralDetails => Set<GeneralDetail>();
    public DbSet<PublicationExtraAttribute> PublicationExtraAttributes => Set<PublicationExtraAttribute>();
    public DbSet<PublicationReport> PublicationReports => Set<PublicationReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<Publication>()
            .Property(x => x.Price)
            .HasPrecision(18, 2);

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
    }
}
