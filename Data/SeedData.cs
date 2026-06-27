using Ubika.Models;
using Ubika.Services;

namespace Ubika.Data;

public static class SeedData
{
    public static async Task InitializeAsync(UbikaDbContext db)
    {
        if (db.Publications.Any())
        {
            return;
        }

        var user = new ApplicationUser
        {
            Name = "Ubika Demo",
            Email = "demo@ubika.local",
            Phone = "3515550101",
            PasswordHash = AuthService.HashPassword("Demo1234!")
        };

        db.Users.Add(user);

        var property = new Publication
        {
            Group = "Inmuebles",
            Category = "Departamentos",
            Title = "Depto 2 ambientes con balcon en Nueva Cordoba",
            Price = 93000,
            Currency = "USD",
            Locality = "Cordoba",
            ShortDescription = "Luminoso, reciclado y listo para entrar.",
            LongDescription = "A 4 cuadras del Patio Olmos, cocina integrada y balcon al frente.",
            ImagesCsv = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=1200&q=80",
            ContactName = user.Name,
            ContactPhone = user.Phone,
            ContactEmail = user.Email,
            User = user,
            Featured = true,
            Status = "Activa",
            Latitude = -31.4201,
            Longitude = -64.1888,
            PropertyDetail = new PropertyDetail
            {
                PropertyType = "Departamento",
                Operation = "Venta",
                Zone = "Nueva Cordoba",
                TotalAreaM2 = 58,
                CoveredAreaM2 = 52,
                RoomsOrBedrooms = "2 ambientes",
                Bathrooms = 1,
                GarageSpaces = 0,
                Condition = "Muy bueno",
                Amenities = "Balcon, Ascensor, Terraza",
                Services = "Gas natural, Internet"
            }
        };

        var vehicle = new Publication
        {
            Group = "Rodados",
            Category = "Autos",
            Title = "Ford Fiesta SE 2018",
            Price = 12500,
            Currency = "USD",
            Locality = "Rosario",
            ShortDescription = "Service al dia, unico dueño.",
            LongDescription = "Nafta, manual, 89.000 km, sensores de estacionamiento y pantalla.",
            ImagesCsv = "https://images.unsplash.com/photo-1494976388531-d1058494cdd8?auto=format&fit=crop&w=1200&q=80",
            ContactName = user.Name,
            ContactPhone = user.Phone,
            ContactEmail = user.Email,
            User = user,
            Featured = true,
            Status = "Activa",
            Latitude = -32.9442,
            Longitude = -60.6505,
            VehicleDetail = new VehicleDetail
            {
                VehicleType = "Auto",
                Brand = "Ford",
                Model = "Fiesta",
                Version = "SE",
                Year = 2018,
                Kilometers = 89000,
                Fuel = "Nafta",
                Transmission = "Manual",
                Color = "Gris",
                GeneralCondition = "Excelente",
                Equipment = "Pantalla, Sensores, Llantas"
            }
        };

        var general = new Publication
        {
            Group = "Generales",
            Category = "Celulares",
            Title = "iPhone 13 128GB",
            Price = 780,
            Currency = "USD",
            Locality = "Buenos Aires",
            ShortDescription = "Bateria 88%, caja y cable original.",
            LongDescription = "Sin golpes, libre de fabrica. Se prueba al retirar.",
            ImagesCsv = "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=1200&q=80",
            ContactName = "Laura Martinez",
            ContactPhone = "1140047788",
            ContactEmail = "laura@ubika.local",
            User = user,
            Status = "Activa",
            Latitude = -34.6037,
            Longitude = -58.3816,
            GeneralDetail = new GeneralDetail
            {
                Subcategory = "Celulares",
                ItemCondition = "Usado",
                Brand = "Apple",
                Model = "iPhone 13",
                Warranty = "7 dias de prueba",
                Shipping = "Retiro o envio a coordinar"
            }
        };

        general.ExtraAttributes.Add(new PublicationExtraAttribute { Key = "Memoria", Value = "128GB" });
        general.ExtraAttributes.Add(new PublicationExtraAttribute { Key = "Bateria", Value = "88%" });
        vehicle.ExtraAttributes.Add(new PublicationExtraAttribute { Key = "Sensores", Value = "Si" });
        property.ExtraAttributes.Add(new PublicationExtraAttribute { Key = "Apto credito", Value = "Si" });

        db.Publications.AddRange(property, vehicle, general);
        await db.SaveChangesAsync();
    }
}
