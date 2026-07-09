using Microsoft.EntityFrameworkCore;
using Ventagram.Models;
using Ventagram.Services;

namespace Ventagram.Data;

public static class SeedData
{
    private const int TargetPublicationsPerGroup = 50;
    private static readonly PublicationGroupType[] PublicationGroupCatalog =
    [
        new() { Id = (byte)PublicationGroup.Inmuebles, Name = "Inmuebles", SortOrder = 1, IsActive = true },
        new() { Id = (byte)PublicationGroup.Rodados, Name = "Rodados", SortOrder = 2, IsActive = true },
        new() { Id = (byte)PublicationGroup.Generales, Name = "Generales", SortOrder = 3, IsActive = true },
        new() { Id = (byte)PublicationGroup.Embarcaciones, Name = "Embarcaciones", SortOrder = 4, IsActive = true }
    ];
    private static readonly PublicationReportReason[] PublicationReportReasonCatalog =
    [
        new() { Id = 1, Name = "Spam/Publicidad", SortOrder = 1, IsActive = true },
        new() { Id = 2, Name = "Fraude", SortOrder = 2, IsActive = true },
        new() { Id = 3, Name = "Precio no corresponde", SortOrder = 3, IsActive = true },
        new() { Id = 4, Name = "Foto no corresponde", SortOrder = 4, IsActive = true },
        new() { Id = 5, Name = "Ubicacion erronea", SortOrder = 5, IsActive = true }
    ];
    private static readonly (PublicationGroup Group, string[] Categories)[] PublicationCategoryCatalog =
    [
        (PublicationGroup.Inmuebles,
        [
            "Departamento",
            "Casa",
            "PH",
            "Terreno",
            "Local comercial",
            "Campo",
            "Quinta vacacional",
            "Oficina comercial",
            "Garage",
            "Bodega-Galpón",
            "Fondo de comercio",
            "Hotel",
            "Depósito",
            "Bóveda, nicho o parcela",
            "Cama náutica",
            "Consultorio",
            "Edificio",
            "Desarrollo horizontal",
            "Desarrollo vertical"
        ]),
        (PublicationGroup.Rodados,
        [
            "Autos",
            "Utilitarios y Camionetas",
            "Motos",
            "Planes",
            "Cuatris",
            "Camiones",
            "Otros"
        ]),
        (PublicationGroup.Generales,
        [
            "Celular",
            "Notebook",
            "Electrodoméstico",
            "Mueble",
            "Herramienta",
            "Ropa",
            "Deporte"
        ]),
        (PublicationGroup.Embarcaciones,
        [
            "Lanchas",
            "Veleros",
            "Motos de agua",
            "Botes y semirrigidos",
            "Accesorios nauticos"
        ])
    ];
    private static readonly Dictionary<PublicationGroup, CategoryFieldSeed[]> CategoryFieldCatalog = new()
    {
        [PublicationGroup.Inmuebles] =
        [
            new("operacion", "Tipo de operacion", PublicationCategoryFieldDataType.Lista, true, 1,null,"Venta,Alquiler,Temporario"),
            new("zona", "Zona", PublicationCategoryFieldDataType.Texto, false, 2),
            new("superficie_total_m2", "Superficie total", PublicationCategoryFieldDataType.Numero, false, 3, "m2"),
            new("superficie_cubierta_m2", "Superficie cubierta", PublicationCategoryFieldDataType.Numero, false, 4, "m2"),
            new("ambientes", "Ambientes", PublicationCategoryFieldDataType.Texto, false, 5),
            new("banios", "Banios", PublicationCategoryFieldDataType.Numero, false, 6),
            new("direccion", "Direccion", PublicationCategoryFieldDataType.Texto, false, 7),
            new("garage", "Garage", PublicationCategoryFieldDataType.Numero, false, 8),
            new("antiguedad_anios", "Antiguedad", PublicationCategoryFieldDataType.Numero, false, 9, "anios"),
            new("expensas", "Expensas", PublicationCategoryFieldDataType.Numero, false, 10, "ARS"),
            new("estado", "Estado", PublicationCategoryFieldDataType.Texto, false, 11),
            new("apto_credito", "Apto credito", PublicationCategoryFieldDataType.Booleano, false, 12),
            new("uso_profesional", "Uso profesional", PublicationCategoryFieldDataType.Booleano, false, 13),
            new("servicios", "Servicios", PublicationCategoryFieldDataType.Texto, false, 14),
            new("amenities", "Amenities", PublicationCategoryFieldDataType.Texto, false, 15)
        ],
        [PublicationGroup.Rodados] =
        [
            new("tipo_vehiculo", "Tipo de vehiculo", PublicationCategoryFieldDataType.Lista, true, 1),
            new("marca", "Marca", PublicationCategoryFieldDataType.Texto, true, 2),
            new("modelo", "Modelo", PublicationCategoryFieldDataType.Texto, true, 3),
            new("anio", "Anio", PublicationCategoryFieldDataType.Numero, true, 4, "anio"),
            new("kilometros", "Kilometros", PublicationCategoryFieldDataType.Numero, false, 5, "km"),
            new("combustible", "Combustible", PublicationCategoryFieldDataType.Texto, false, 6),
            new("transmision", "Transmision", PublicationCategoryFieldDataType.Texto, false, 7),
            new("version", "Version", PublicationCategoryFieldDataType.Texto, false, 8),
            new("color", "Color", PublicationCategoryFieldDataType.Texto, false, 9),
            new("patente", "Patente", PublicationCategoryFieldDataType.Texto, false, 10),
            new("motor", "Motor", PublicationCategoryFieldDataType.Texto, false, 11),
            new("traccion", "Traccion", PublicationCategoryFieldDataType.Texto, false, 12),
            new("puertas", "Puertas", PublicationCategoryFieldDataType.Numero, false, 13),
            new("titulares", "Titulares", PublicationCategoryFieldDataType.Numero, false, 14),
            new("permuta", "Permuta", PublicationCategoryFieldDataType.Booleano, false, 15),
            new("financiacion", "Financiacion", PublicationCategoryFieldDataType.Booleano, false, 16),
            new("equipamiento", "Equipamiento", PublicationCategoryFieldDataType.Texto, false, 17),
            new("estado_general", "Estado general", PublicationCategoryFieldDataType.Texto, false, 18)
        ],
        [PublicationGroup.Generales] =
        [
            new("subcategoria", "Subcategoria", PublicationCategoryFieldDataType.Texto, true, 1),
            new("estado_articulo", "Estado", PublicationCategoryFieldDataType.Texto, false, 2),
            new("marca", "Marca", PublicationCategoryFieldDataType.Texto, false, 3),
            new("modelo", "Modelo", PublicationCategoryFieldDataType.Texto, false, 4),
            new("sku", "SKU", PublicationCategoryFieldDataType.Texto, false, 5),
            new("stock", "Stock", PublicationCategoryFieldDataType.Numero, false, 6, "unid"),
            new("color", "Color", PublicationCategoryFieldDataType.Texto, false, 7),
            new("medida", "Medida", PublicationCategoryFieldDataType.Texto, false, 8),
            new("peso", "Peso", PublicationCategoryFieldDataType.Texto, false, 9),
            new("dimensiones", "Dimensiones", PublicationCategoryFieldDataType.Texto, false, 10),
            new("garantia", "Garantia", PublicationCategoryFieldDataType.Texto, false, 11),
            new("envio", "Envio", PublicationCategoryFieldDataType.Texto, false, 12),
            new("permuta", "Permuta", PublicationCategoryFieldDataType.Booleano, false, 13)
        ],
        [PublicationGroup.Embarcaciones] =
        [
            new("tipo_embarcacion", "Tipo de embarcacion", PublicationCategoryFieldDataType.Texto, true, 1),
            new("marca", "Marca", PublicationCategoryFieldDataType.Texto, false, 2),
            new("modelo", "Modelo", PublicationCategoryFieldDataType.Texto, false, 3),
            new("anio", "Anio", PublicationCategoryFieldDataType.Numero, false, 4, "anio"),
            new("eslora", "Eslora", PublicationCategoryFieldDataType.Texto, false, 5),
            new("motor", "Motor", PublicationCategoryFieldDataType.Texto, false, 6)
        ]
    };
    private static readonly string[] PropertyGalleryPool =
    [
        "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1564013799919-ab600027ffc6?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1484154218962-a197022b5858?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1600585154526-990dced4db0d?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1512917774080-9991f1c4c750?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1494526585095-c41746248156?auto=format&fit=crop&w=1200&q=80"
    ];
    private static readonly string[] VehicleGalleryPool =
    [
        "https://images.unsplash.com/photo-1494976388531-d1058494cdd8?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1544636331-e26879cd4d9b?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1558981806-ec527fa84c39?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1553440569-bcc63803a83d?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1511919884226-fd3cad34687c?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1485463611174-f302f6a5c1c9?auto=format&fit=crop&w=1200&q=80"
    ];
    private static readonly string[] GeneralGalleryPool =
    [
        "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1541625602330-2277a4c46182?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1606813907291-d86efa9b94db?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1584568694244-14fbdf83bd30?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1516035069371-29a1b244cc32?auto=format&fit=crop&w=1200&q=80",
        "https://images.unsplash.com/photo-1504148455328-c376907d081c?auto=format&fit=crop&w=1200&q=80"
    ];

    public static async Task InitializeAsync(VentagramDbContext db)
    {
        await EnsurePublicationGroupsAsync(db);
        await EnsurePublicationCategoriesAsync(db);
        await EnsurePublicationReportReasonsAsync(db);

        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == "demo@ventagram.local");
        if (user is null)
        {
            user = new ApplicationUser
            {
                Name = "Ventagram Demo",
                Email = "demo@ventagram.local",
                Phone = "3515550101",
                PasswordHash = AuthService.HashPassword("Demo1234!")
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var existingTitles = new HashSet<string>(await db.Publications
            .Select(x => x.Title)
            .ToListAsync(), StringComparer.OrdinalIgnoreCase);
        var existingCounts = await db.Publications
            .GroupBy(x => x.Group)
            .Select(x => new { Group = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Group, x => x.Count);

        var categoryLookup = await BuildCategoryLookupAsync(db);
        var catalog = BuildCatalog(user, categoryLookup);
        var available = catalog
            .Where(x => !existingTitles.Contains(x.Title))
            .ToList();

        var missing = new List<Publication>();
        foreach (var group in new[] { PublicationGroup.Inmuebles, PublicationGroup.Rodados, PublicationGroup.Generales })
        {
            var current = existingCounts.TryGetValue(group, out var count) ? count : 0;
            var needed = Math.Max(0, TargetPublicationsPerGroup - current);
            if (needed == 0)
            {
                continue;
            }

            var selected = available
                .Where(x => x.Group == group)
                .Take(needed)
                .ToList();

            if (selected.Count < needed)
            {
                throw new InvalidOperationException(
                    $"No hay suficientes publicaciones candidatas para completar {TargetPublicationsPerGroup} registros del grupo {group}.");
            }

            missing.AddRange(selected);
            available.RemoveAll(x => selected.Contains(x));
        }

        if (missing.Count > 0)
        {
            db.Publications.AddRange(missing);
            await db.SaveChangesAsync();
        }

        await BackfillGalleryImagesAsync(db, user.Id);
    }

    private static async Task EnsurePublicationGroupsAsync(VentagramDbContext db)
    {
        var existingIds = await db.PublicationGroupTypes
            .Select(x => x.Id)
            .ToListAsync();

        var missing = PublicationGroupCatalog
            .Where(x => !existingIds.Contains(x.Id))
            .Select(x => new PublicationGroupType
            {
                Id = x.Id,
                Name = x.Name,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive
            })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        db.PublicationGroupTypes.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static async Task EnsurePublicationCategoriesAsync(VentagramDbContext db)
    {
        var existingKeys = new HashSet<string>(
            await db.PublicationCategories
                .Select(x => $"{(byte)x.Group}|{x.Name}")
                .ToListAsync(),
            StringComparer.OrdinalIgnoreCase);

        var missing = new List<PublicationCategory>();
        foreach (var (group, categories) in PublicationCategoryCatalog)
        {
            for (var index = 0; index < categories.Length; index++)
            {
                var name = categories[index];
                var key = $"{(byte)group}|{name}";
                if (existingKeys.Contains(key))
                {
                    continue;
                }

                missing.Add(new PublicationCategory
                {
                    Group = group,
                    Name = name,
                    SortOrder = index + 1,
                    IsActive = true
                });
            }
        }

        if (missing.Count == 0)
        {
            return;
        }

        db.PublicationCategories.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static async Task EnsurePublicationReportReasonsAsync(VentagramDbContext db)
    {
        var existingIds = await db.PublicationReportReasons
            .Select(x => x.Id)
            .ToListAsync();

        var missing = PublicationReportReasonCatalog
            .Where(x => !existingIds.Contains(x.Id))
            .Select(x => new PublicationReportReason
            {
                Id = x.Id,
                Name = x.Name,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive
            })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        db.PublicationReportReasons.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static List<Publication> BuildCatalog(ApplicationUser user, PublicationCategoryLookup categoryLookup)
    {
        var catalog = new List<Publication>
        {
            CreateProperty(
                user,
                categoryLookup,
                "Depto 2 ambientes con balcon en Nueva Cordoba",
                "Departamentos",
                "Cordoba",
                93000,
                "Luminoso, reciclado y listo para entrar.",
                "A 4 cuadras del Patio Olmos, cocina integrada y balcon al frente.",
                "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=1200&q=80",
                -31.4201, -64.1888,
                "Departamento", "Venta", "Nueva Cordoba", 58, 52, "2 ambientes", 1, 0, "Muy bueno",
                "Gas natural, Internet", "Balcon, Ascensor, Terraza",
                ("Apto credito", "Si")),
            CreateProperty(
                user,
                categoryLookup,
                "Casa 3 dormitorios con patio en Yerba Buena",
                "Casas",
                "Tucuman",
                145000,
                "Amplia, luminosa y con quincho.",
                "Casa familiar con jardin, galeria y cochera doble en zona residencial.",
                "https://images.unsplash.com/photo-1564013799919-ab600027ffc6?auto=format&fit=crop&w=1200&q=80",
                -26.8167, -65.2833,
                "Casa", "Venta", "Yerba Buena", 240, 180, "3 dormitorios", 2, 2, "Excelente",
                "Agua, Gas, Internet", "Quincho, Jardin, Parrilla"),
            CreateProperty(
                user,
                categoryLookup,
                "Lote en barrio cerrado Las Tipas",
                "Terrenos",
                "Rosario",
                38000,
                "Lote interno listo para escriturar.",
                "Buen acceso, seguridad 24 hs y amenities del barrio disponibles.",
                "https://images.unsplash.com/photo-1500382017468-9049fed747ef?auto=format&fit=crop&w=1200&q=80",
                -32.8800, -60.7000,
                "Lote", "Venta", "Funes", 620, null, "Terreno", 0, 0, "Muy bueno",
                "Electricidad, Agua", "Seguridad, Club House"),
            CreateProperty(
                user,
                categoryLookup,
                "PH reciclado de 2 dormitorios en La Plata",
                "PH",
                "La Plata",
                79000,
                "Sin expensas, patio propio y cocina nueva.",
                "Ideal primera vivienda, cerca del centro comercial y transporte.",
                "https://images.unsplash.com/photo-1484154218962-a197022b5858?auto=format&fit=crop&w=1200&q=80",
                -34.9214, -57.9544,
                "PH", "Venta", "Centro", 92, 78, "2 dormitorios", 1, 0, "Reciclado",
                "Agua, Cloacas, Gas", "Patio"),
            CreateProperty(
                user,
                categoryLookup,
                "Monoambiente amoblado para alquiler temporal",
                "Departamentos",
                "Buenos Aires",
                550,
                "Equipado y listo para ingresar.",
                "Incluye wifi, ropa blanca y expensas en Palermo Hollywood.",
                "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=1200&q=80",
                -34.5895, -58.4300,
                "Departamento", "Alquiler", "Palermo", 36, 34, "Monoambiente", 1, 0, "Excelente",
                "Internet, Agua", "Laundry, Terraza"),
            CreateProperty(
                user,
                categoryLookup,
                "Duplex a estrenar en Godoy Cruz",
                "Duplex",
                "Mendoza",
                118000,
                "Dos plantas, cochera y patio seco.",
                "Ubicado en barrio tranquilo con rapido acceso al centro.",
                "https://images.unsplash.com/photo-1600585154526-990dced4db0d?auto=format&fit=crop&w=1200&q=80",
                -32.9286, -68.8440,
                "Duplex", "Venta", "Godoy Cruz", 140, 110, "3 dormitorios", 2, 1, "A estrenar",
                "Gas, Agua, Internet", "Patio, Cochera"),
            CreateProperty(
                user,
                categoryLookup,
                "Oficina premium en microcentro",
                "Oficinas",
                "Cordoba",
                68000,
                "Recepcion, sala de reuniones y seguridad.",
                "Apta profesional, excelente luminosidad y expensas moderadas.",
                "https://images.unsplash.com/photo-1497366811353-6870744d04b2?auto=format&fit=crop&w=1200&q=80",
                -31.4167, -64.1833,
                "Oficina", "Venta", "Microcentro", 74, 74, "Planta libre", 1, 1, "Muy bueno",
                "Internet, Luz, Agua", "Seguridad, Recepcion"),
            CreateProperty(
                user,
                categoryLookup,
                "Campo de 12 hectareas con mejora",
                "Campos",
                "Entre Rios",
                210000,
                "Apto agricultura y fin de semana.",
                "Tiene alambrado, perforacion y casa chica de apoyo.",
                "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=1200&q=80",
                -31.7319, -60.5238,
                "Campo", "Venta", "Parana Campana", 120000, 80, "Campo", 1, 0, "Bueno",
                "Perforacion, Electricidad", "Casa de apoyo"),
            CreateProperty(
                user,
                categoryLookup,
                "Casa quinta con pileta en Canning",
                "Quintas",
                "Buenos Aires",
                189000,
                "Ideal descanso o renta por eventos.",
                "Gran parque arbolado, pileta cercada y quincho completo.",
                "https://images.unsplash.com/photo-1512917774080-9991f1c4c750?auto=format&fit=crop&w=1200&q=80",
                -34.8533, -58.5198,
                "Casa quinta", "Venta", "Canning", 1800, 220, "4 ambientes", 3, 3, "Excelente",
                "Luz, Agua, Internet", "Pileta, Quincho, Parque"),
            CreateProperty(
                user,
                categoryLookup,
                "Local comercial sobre avenida principal",
                "Locales",
                "Mar del Plata",
                99000,
                "Vidriera amplia y deposito propio.",
                "Buena circulacion peatonal, apto varios rubros.",
                "https://images.unsplash.com/photo-1441986300917-64674bd600d8?auto=format&fit=crop&w=1200&q=80",
                -38.0055, -57.5426,
                "Local", "Venta", "Guemes", 85, 85, "Salon + deposito", 1, 0, "Muy bueno",
                "Agua, Luz", "Vidriera"),

            CreateVehicle(
                user,
                categoryLookup,
                "Ford Fiesta SE 2018",
                "Autos",
                "Rosario",
                12500,
                "Service al dia, unico dueno.",
                "Nafta, manual, 89.000 km, sensores de estacionamiento y pantalla.",
                "https://images.unsplash.com/photo-1494976388531-d1058494cdd8?auto=format&fit=crop&w=1200&q=80",
                -32.9442, -60.6505,
                "Auto", "Ford", "Fiesta", 2018, 89000, "Nafta", "Manual", "SE", "Gris", "Excelente",
                "Pantalla, Sensores, Llantas",
                ("Sensores", "Si")),
            CreateVehicle(
                user,
                categoryLookup,
                "Toyota Hilux SRX 2021 4x4",
                "Camionetas",
                "Salta",
                36500,
                "Unica mano, impecable y con services oficiales.",
                "Doble cabina, automatica, lista para viajar o trabajar.",
                "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1200&q=80",
                -24.7821, -65.4232,
                "Camioneta", "Toyota", "Hilux", 2021, 54000, "Diesel", "Automatica", "SRX", "Blanca", "Excelente",
                "4x4, Cuero, Camara, Navegador"),
            CreateVehicle(
                user,
                categoryLookup,
                "Volkswagen Gol Trend 2016",
                "Autos",
                "Cordoba",
                9800,
                "Economico y muy cuidado.",
                "Aire, direccion, cubiertas nuevas y papeles al dia.",
                "https://images.unsplash.com/photo-1544636331-e26879cd4d9b?auto=format&fit=crop&w=1200&q=80",
                -31.4201, -64.1888,
                "Auto", "Volkswagen", "Gol Trend", 2016, 112000, "Nafta", "Manual", "Pack I", "Rojo", "Muy bueno",
                "Aire, Direccion, Stereo"),
            CreateVehicle(
                user,
                categoryLookup,
                "Honda Wave S 110 2023",
                "Motos",
                "La Plata",
                2400,
                "Primera mano, lista para transferir.",
                "Uso particular, pocos kilometros y service recien hecho.",
                "https://images.unsplash.com/photo-1558981806-ec527fa84c39?auto=format&fit=crop&w=1200&q=80",
                -34.9214, -57.9544,
                "Moto", "Honda", "Wave", 2023, 6400, "Nafta", "Semi", "S 110", "Negra", "Excelente",
                "Alarma, Baulera"),
            CreateVehicle(
                user,
                categoryLookup,
                "Chevrolet Cruze LT 2020",
                "Autos",
                "Buenos Aires",
                18700,
                "Motor turbo, muy equipado.",
                "Automatico, cuero, techo y mantenimiento al dia.",
                "https://images.unsplash.com/photo-1553440569-bcc63803a83d?auto=format&fit=crop&w=1200&q=80",
                -34.6037, -58.3816,
                "Auto", "Chevrolet", "Cruze", 2020, 68000, "Nafta", "Automatica", "LT", "Azul", "Excelente",
                "Cuero, Techo, CarPlay"),
            CreateVehicle(
                user,
                categoryLookup,
                "Renault Kangoo 1.6 furgon 2017",
                "Utilitarios",
                "Mendoza",
                10900,
                "Ideal repartos o trabajo liviano.",
                "Buen estado general, GNC de quinta y mantenimiento reciente.",
                "https://images.unsplash.com/photo-1519641471654-76ce0107ad1b?auto=format&fit=crop&w=1200&q=80",
                -32.8895, -68.8458,
                "Utilitario", "Renault", "Kangoo", 2017, 136000, "Nafta/GNC", "Manual", "Furgon", "Blanco", "Muy bueno",
                "Aire, GNC, Porton lateral"),
            CreateVehicle(
                user,
                categoryLookup,
                "Peugeot 208 Allure 2022",
                "Autos",
                "Santa Fe",
                21400,
                "Casi sin uso, garantia vigente.",
                "Pantalla grande, camara de retroceso y llantas originales.",
                "https://images.unsplash.com/photo-1502877338535-766e1452684a?auto=format&fit=crop&w=1200&q=80",
                -31.6333, -60.7000,
                "Auto", "Peugeot", "208", 2022, 21000, "Nafta", "Manual", "Allure", "Gris", "Excelente",
                "Camara, Pantalla, Llantas"),
            CreateVehicle(
                user,
                categoryLookup,
                "Yamaha MT-03 2021",
                "Motos",
                "Neuquen",
                6900,
                "Titular, escape original y accesorios.",
                "Uso recreativo, sin caidas y con cubiertas en buen estado.",
                "https://images.unsplash.com/photo-1517846693594-1567da72af75?auto=format&fit=crop&w=1200&q=80",
                -38.9516, -68.0591,
                "Moto", "Yamaha", "MT-03", 2021, 18000, "Nafta", "Manual", "ABS", "Azul", "Excelente",
                "ABS, Sliders, Parabrisas"),
            CreateVehicle(
                user,
                categoryLookup,
                "Citroen Berlingo Multispace 2015",
                "Familiares",
                "Parana",
                8700,
                "Amplia, comoda y con mantenimiento hecho.",
                "Ideal familia o trabajo, con gran baul y aire.",
                "https://images.unsplash.com/photo-1552519507-da3b142c6e3d?auto=format&fit=crop&w=1200&q=80",
                -31.7319, -60.5238,
                "Familiar", "Citroen", "Berlingo", 2015, 149000, "Nafta", "Manual", "Multispace", "Bordo", "Bueno",
                "Aire, Direccion, Gran baul"),
            CreateVehicle(
                user,
                categoryLookup,
                "Mercedes Benz Sprinter 415 2019",
                "Utilitarios",
                "Cordoba",
                28900,
                "Lista para trabajar, impecable de mecanica.",
                "Furgon mediano, un solo conductor y kilometraje de ruta.",
                "https://images.unsplash.com/photo-1485463611174-f302f6a5c1c9?auto=format&fit=crop&w=1200&q=80",
                -31.4201, -64.1888,
                "Utilitario", "Mercedes Benz", "Sprinter", 2019, 98000, "Diesel", "Manual", "415", "Blanca", "Muy bueno",
                "Aire, ABS, Cierre centralizado"),

            CreateGeneral(
                user,
                categoryLookup,
                "iPhone 13 128GB",
                "Celulares",
                "Buenos Aires",
                780,
                "Bateria 88%, caja y cable original.",
                "Sin golpes, libre de fabrica. Se prueba al retirar.",
                "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=1200&q=80",
                -34.6037, -58.3816,
                "Celulares", "Usado", "Apple", "iPhone 13", "7 dias de prueba", "Retiro o envio a coordinar",
                ("Memoria", "128GB"),
                ("Bateria", "88%")),
            CreateGeneral(
                user,
                categoryLookup,
                "Notebook Lenovo IdeaPad 5 Ryzen 7",
                "Computacion",
                "Cordoba",
                920,
                "16GB RAM y SSD de 512GB.",
                "Muy buen estado, se entrega con cargador y funda.",
                "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=1200&q=80",
                -31.4201, -64.1888,
                "Notebooks", "Usado", "Lenovo", "IdeaPad 5", "30 dias", "Envio por encomienda",
                ("RAM", "16GB"),
                ("SSD", "512GB")),
            CreateGeneral(
                user,
                categoryLookup,
                "Bicicleta mountain bike rodado 29",
                "Deportes",
                "Mendoza",
                410,
                "Cuadro aluminio y frenos a disco.",
                "Ideal para ciudad y senderos livianos, lista para usar.",
                "https://images.unsplash.com/photo-1541625602330-2277a4c46182?auto=format&fit=crop&w=1200&q=80",
                -32.8895, -68.8458,
                "Bicicletas", "Usado", "Venzo", "R29", "Sin garantia", "Retiro"),
            CreateGeneral(
                user,
                categoryLookup,
                "Sillon esquinero 5 cuerpos",
                "Hogar",
                "Rosario",
                650,
                "Tapizado gris claro, muy comodo.",
                "Se vende por mudanza, sin roturas ni manchas importantes.",
                "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=1200&q=80",
                -32.9442, -60.6505,
                "Muebles", "Usado", "Nordico", "Esquinero", "Sin garantia", "Retiro a coordinar"),
            CreateGeneral(
                user,
                categoryLookup,
                "PlayStation 5 con joystick extra",
                "Gaming",
                "La Plata",
                890,
                "Poco uso y caja completa.",
                "Incluye un segundo joystick y base de carga.",
                "https://images.unsplash.com/photo-1606813907291-d86efa9b94db?auto=format&fit=crop&w=1200&q=80",
                -34.9214, -57.9544,
                "Consolas", "Usado", "Sony", "PS5", "15 dias", "Envio o retiro",
                ("Joystick extra", "Si")),
            CreateGeneral(
                user,
                categoryLookup,
                "Heladera no frost 430 litros",
                "Electrodomesticos",
                "Mar del Plata",
                740,
                "En excelente estado de funcionamiento.",
                "Color acero, muy silenciosa y con poco consumo.",
                "https://images.unsplash.com/photo-1584568694244-14fbdf83bd30?auto=format&fit=crop&w=1200&q=80",
                -38.0055, -57.5426,
                "Heladeras", "Usado", "Samsung", "No Frost", "Sin garantia", "Retiro"),
            CreateGeneral(
                user,
                categoryLookup,
                "Mesa de comedor de madera maciza",
                "Hogar",
                "Parana",
                320,
                "Para seis personas, muy firme.",
                "Incluye seis sillas tapizadas en buen estado.",
                "https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?auto=format&fit=crop&w=1200&q=80",
                -31.7319, -60.5238,
                "Muebles", "Usado", "Roble", "Comedor", "Sin garantia", "Retiro"),
            CreateGeneral(
                user,
                categoryLookup,
                "Camara Canon EOS Rebel T7",
                "Fotografia",
                "Salta",
                580,
                "Con lente kit y bolso.",
                "Ideal para empezar fotografia, bateria original y cargador.",
                "https://images.unsplash.com/photo-1516035069371-29a1b244cc32?auto=format&fit=crop&w=1200&q=80",
                -24.7821, -65.4232,
                "Camaras", "Usado", "Canon", "Rebel T7", "7 dias", "Envio"),
            CreateGeneral(
                user,
                categoryLookup,
                "Set de herramientas 120 piezas",
                "Ferreteria",
                "Neuquen",
                115,
                "Caja completa con criques y puntas.",
                "Ideal hogar o taller liviano, poco uso.",
                "https://images.unsplash.com/photo-1504148455328-c376907d081c?auto=format&fit=crop&w=1200&q=80",
                -38.9516, -68.0591,
                "Herramientas", "Usado", "Stanley", "120 piezas", "Sin garantia", "Envio"),
            CreateGeneral(
                user,
                categoryLookup,
                "Cuna funcional con cajonera",
                "Bebes",
                "Santa Fe",
                270,
                "Color blanco, en muy buen estado.",
                "Se entrega desarmada con manual y herrajes completos.",
                "https://images.unsplash.com/photo-1519710164239-da123dc03ef4?auto=format&fit=crop&w=1200&q=80",
                -31.6333, -60.7000,
                "Bebes", "Usado", "Infanti", "Funcional", "Sin garantia", "Retiro")
        };

        catalog.AddRange(BuildGeneratedProperties(user, categoryLookup));
        catalog.AddRange(BuildGeneratedVehicles(user, categoryLookup));
        catalog.AddRange(BuildGeneratedGeneralPublications(user, categoryLookup));

        return catalog;
    }

    private static IEnumerable<Publication> BuildGeneratedProperties(ApplicationUser user, PublicationCategoryLookup categoryLookup)
    {
        var cities = new[]
        {
            new CitySeed("Cordoba", "General Paz", -31.4167, -64.1833),
            new CitySeed("Rosario", "Pichincha", -32.9442, -60.6505),
            new CitySeed("Mendoza", "Godoy Cruz", -32.9286, -68.8440),
            new CitySeed("Buenos Aires", "Caballito", -34.6186, -58.4353),
            new CitySeed("La Plata", "City Bell", -34.8686, -58.0718),
            new CitySeed("Mar del Plata", "Guemes", -38.0055, -57.5426),
            new CitySeed("Salta", "Tres Cerritos", -24.7686, -65.3950),
            new CitySeed("Parana", "Centro", -31.7319, -60.5238),
            new CitySeed("Santa Fe", "Candioti", -31.6333, -60.7000),
            new CitySeed("Neuquen", "Alta Barda", -38.9516, -68.0591)
        };
        var kinds = new[]
        {
            new PropertySeed("Departamentos", "Departamento", "Venta"),
            new PropertySeed("Casas", "Casa", "Venta"),
            new PropertySeed("Terrenos", "Lote", "Venta"),
            new PropertySeed("PH", "PH", "Venta"),
            new PropertySeed("Duplex", "Duplex", "Venta"),
            new PropertySeed("Oficinas", "Oficina", "Venta"),
            new PropertySeed("Quintas", "Casa quinta", "Venta"),
            new PropertySeed("Locales", "Local", "Venta"),
            new PropertySeed("Campos", "Campo", "Venta"),
            new PropertySeed("Departamentos", "Departamento", "Alquiler")
        };
        var highlights = new[]
        {
            "con balcon y mucha luz",
            "reciclado y listo para ingresar",
            "apto credito y excelente acceso",
            "con patio y cochera",
            "ideal renta o primera vivienda",
            "sobre calle tranquila y segura",
            "con amenities y bajas expensas",
            "listo para escriturar",
            "con buena orientacion",
            "publicado por tiempo limitado"
        };
        var imageUrls = new[]
        {
            "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1564013799919-ab600027ffc6?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1484154218962-a197022b5858?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1600585154526-990dced4db0d?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1512917774080-9991f1c4c750?auto=format&fit=crop&w=1200&q=80"
        };

        var publications = new List<Publication>();
        for (var i = 0; i < 40; i++)
        {
            var city = cities[i % cities.Length];
            var kind = kinds[i % kinds.Length];
            var highlight = highlights[i % highlights.Length];
            var rooms = kind.PropertyType is "Lote" or "Campo" ? "Terreno" : $"{2 + (i % 4)} ambientes";
            var totalArea = kind.PropertyType switch
            {
                "Lote" => 300 + (i * 15),
                "Campo" => 10000 + (i * 2500),
                _ => 45 + (i * 4)
            };
            decimal? coveredArea = kind.PropertyType switch
            {
                "Lote" => null,
                "Campo" => 90 + (i * 3),
                _ => totalArea - (5 + (i % 12))
            };
            var price = kind.Operation == "Alquiler"
                ? 420 + (i * 18)
                : 42000 + (i * 6200);

            publications.Add(CreateProperty(
                user,
                categoryLookup,
                $"{kind.PropertyType} {highlight} en {city.Zone} - oportunidad {i + 11:00}",
                kind.Category,
                city.Locality,
                price,
                $"{kind.PropertyType} en {city.Zone}, {city.Locality}. {highlight}.",
                $"Operacion {kind.Operation.ToLowerInvariant()} de {kind.PropertyType.ToLowerInvariant()} en {city.Zone}. Buena ubicacion, servicios conectados y publicacion con precio visible.",
                imageUrls[i % imageUrls.Length],
                city.Latitude,
                city.Longitude,
                kind.PropertyType,
                kind.Operation,
                city.Zone,
                totalArea,
                coveredArea,
                rooms,
                1 + (i % 3),
                i % 3,
                i % 2 == 0 ? "Muy bueno" : "Excelente",
                "Agua, Luz, Internet",
                i % 2 == 0 ? "Balcon, Parrilla" : "Patio, Cochera",
                ("Codigo", $"INM-{i + 11:000}")));
        }

        return publications;
    }

    private static IEnumerable<Publication> BuildGeneratedVehicles(ApplicationUser user, PublicationCategoryLookup categoryLookup)
    {
        var cities = new[]
        {
            new CitySeed("Cordoba", "Centro", -31.4201, -64.1888),
            new CitySeed("Rosario", "Centro", -32.9442, -60.6505),
            new CitySeed("Mendoza", "Godoy Cruz", -32.8895, -68.8458),
            new CitySeed("Buenos Aires", "Belgrano", -34.5621, -58.4563),
            new CitySeed("La Plata", "Casco", -34.9214, -57.9544),
            new CitySeed("Salta", "Macrocentro", -24.7821, -65.4232),
            new CitySeed("Parana", "Bajada Grande", -31.7440, -60.5330),
            new CitySeed("Santa Fe", "Centro", -31.6333, -60.7000),
            new CitySeed("Neuquen", "Centro", -38.9516, -68.0591),
            new CitySeed("Mar del Plata", "Constitucion", -37.9700, -57.5500)
        };
        var vehicles = new[]
        {
            new VehicleSeed("Autos", "Auto", "Ford", "Focus", "Manual", "Nafta"),
            new VehicleSeed("Autos", "Auto", "Toyota", "Corolla", "Automatica", "Nafta"),
            new VehicleSeed("Camionetas", "Camioneta", "Volkswagen", "Amarok", "Manual", "Diesel"),
            new VehicleSeed("Motos", "Moto", "Honda", "CB 250", "Manual", "Nafta"),
            new VehicleSeed("Utilitarios", "Utilitario", "Renault", "Master", "Manual", "Diesel"),
            new VehicleSeed("Familiares", "Familiar", "Citroen", "C4 Picasso", "Manual", "Nafta"),
            new VehicleSeed("Autos", "Auto", "Chevrolet", "Onix", "Manual", "Nafta"),
            new VehicleSeed("Motos", "Moto", "Yamaha", "FZ", "Manual", "Nafta"),
            new VehicleSeed("Utilitarios", "Utilitario", "Fiat", "Fiorino", "Manual", "Nafta/GNC"),
            new VehicleSeed("Autos", "Auto", "Peugeot", "208", "Manual", "Nafta")
        };
        var colors = new[] { "Blanco", "Gris", "Negro", "Azul", "Rojo", "Plata" };
        var imageUrls = new[]
        {
            "https://images.unsplash.com/photo-1494976388531-d1058494cdd8?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1544636331-e26879cd4d9b?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1558981806-ec527fa84c39?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1553440569-bcc63803a83d?auto=format&fit=crop&w=1200&q=80"
        };

        var publications = new List<Publication>();
        for (var i = 0; i < 40; i++)
        {
            var city = cities[i % cities.Length];
            var vehicle = vehicles[i % vehicles.Length];
            var year = 2012 + (i % 13);
            var kilometers = 18000 + (i * 4200);
            var price = vehicle.VehicleType == "Moto"
                ? 2400 + (i * 170)
                : vehicle.Category == "Utilitarios"
                    ? 9800 + (i * 850)
                    : 8900 + (i * 760);

            publications.Add(CreateVehicle(
                user,
                categoryLookup,
                $"{vehicle.Brand} {vehicle.Model} {year} - oportunidad {i + 11:00}",
                vehicle.Category,
                city.Locality,
                price,
                $"{vehicle.Brand} {vehicle.Model} en {city.Locality}, listo para transferir.",
                $"{vehicle.VehicleType} con precio publicado, mantenimiento al dia y papeles en regla. Disponible en {city.Locality}.",
                imageUrls[i % imageUrls.Length],
                city.Latitude,
                city.Longitude,
                vehicle.VehicleType,
                vehicle.Brand,
                vehicle.Model,
                year,
                kilometers,
                vehicle.Fuel,
                vehicle.Transmission,
                $"Pack {1 + (i % 4)}",
                colors[i % colors.Length],
                i % 3 == 0 ? "Excelente" : "Muy bueno",
                i % 2 == 0 ? "Pantalla, Camara, Llantas" : "Aire, Direccion, ABS",
                ("Codigo", $"ROD-{i + 11:000}")));
        }

        return publications;
    }

    private static IEnumerable<Publication> BuildGeneratedGeneralPublications(ApplicationUser user, PublicationCategoryLookup categoryLookup)
    {
        var cities = new[]
        {
            new CitySeed("Buenos Aires", "Centro", -34.6037, -58.3816),
            new CitySeed("Cordoba", "Centro", -31.4201, -64.1888),
            new CitySeed("Rosario", "Centro", -32.9442, -60.6505),
            new CitySeed("Mendoza", "Centro", -32.8895, -68.8458),
            new CitySeed("La Plata", "Centro", -34.9214, -57.9544),
            new CitySeed("Salta", "Centro", -24.7821, -65.4232),
            new CitySeed("Parana", "Centro", -31.7319, -60.5238),
            new CitySeed("Santa Fe", "Centro", -31.6333, -60.7000),
            new CitySeed("Neuquen", "Centro", -38.9516, -68.0591),
            new CitySeed("Mar del Plata", "Centro", -38.0055, -57.5426)
        };
        var items = new[]
        {
            new GeneralSeed("Tecnologia", "Tablets", "Samsung", "Galaxy Tab", "15 dias", "Envio o retiro"),
            new GeneralSeed("Computacion", "Monitores", "LG", "24 pulgadas", "7 dias", "Retiro"),
            new GeneralSeed("Hogar", "Muebles", "Madera Viva", "Biblioteca", "Sin garantia", "Retiro"),
            new GeneralSeed("Gaming", "Consolas", "Nintendo", "Switch", "7 dias", "Envio"),
            new GeneralSeed("Electrodomesticos", "Pequenos", "Philips", "Airfryer", "30 dias", "Envio o retiro"),
            new GeneralSeed("Deportes", "Fitness", "Athletic", "Bicicleta fija", "Sin garantia", "Retiro"),
            new GeneralSeed("Fotografia", "Camaras", "Sony", "Alpha", "7 dias", "Envio"),
            new GeneralSeed("Herramientas", "Herramientas", "Bosch", "Taladro", "Sin garantia", "Envio"),
            new GeneralSeed("Bebes", "Bebes", "Graco", "Butaca", "Sin garantia", "Retiro"),
            new GeneralSeed("Celulares", "Celulares", "Motorola", "Edge", "10 dias", "Envio o retiro")
        };
        var imageUrls = new[]
        {
            "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1541625602330-2277a4c46182?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1606813907291-d86efa9b94db?auto=format&fit=crop&w=1200&q=80",
            "https://images.unsplash.com/photo-1584568694244-14fbdf83bd30?auto=format&fit=crop&w=1200&q=80"
        };

        var publications = new List<Publication>();
        for (var i = 0; i < 40; i++)
        {
            var city = cities[i % cities.Length];
            var item = items[i % items.Length];
            var price = 95 + (i * 37);

            publications.Add(CreateGeneral(
                user,
                categoryLookup,
                $"{item.Brand} {item.Model} - oportunidad {i + 11:00}",
                item.Category,
                city.Locality,
                price,
                $"{item.Subcategory} en {city.Locality}, publicado con precio visible.",
                $"Articulo de {item.Subcategory.ToLowerInvariant()} en buen estado, publicado por la comunidad para comprar y vender sin verso.",
                imageUrls[i % imageUrls.Length],
                city.Latitude,
                city.Longitude,
                item.Subcategory,
                i % 2 == 0 ? "Usado" : "Nuevo",
                item.Brand,
                item.Model,
                item.Warranty,
                item.Shipping,
                ("Codigo", $"GEN-{i + 11:000}")));
        }

        return publications;
    }

    private static Publication CreateProperty(
        ApplicationUser user,
        PublicationCategoryLookup categoryLookup,
        string title,
        string category,
        string locality,
        decimal price,
        string shortDescription,
        string longDescription,
        string imageUrl,
        double latitude,
        double longitude,
        string propertyType,
        string operation,
        string zone,
        decimal totalArea,
        decimal? coveredArea,
        string rooms,
        int bathrooms,
        int garageSpaces,
        string condition,
        string? services,
        string? amenities,
        params (string Key, string Value)[] extras)
    {
        var publication = new Publication
        {
            Group = PublicationGroup.Inmuebles,
            CategoryId = ResolveCategoryId(categoryLookup, PublicationGroup.Inmuebles, category),
            Title = title,
            Price = price,
            Currency = "USD",
            Locality = locality,
            ShortDescription = shortDescription,
            LongDescription = longDescription,
            ContactName = user.Name,
            ContactPhone = user.Phone,
            ContactEmail = user.Email,
            User = user,
            Featured = price > 100000,
            Status = "Activa",
            Latitude = latitude,
            Longitude = longitude,
            MediaItems = PublicationMediaBuilder.Build(
                BuildImagesCsv(imageUrl, PropertyGalleryPool),
                null,
                DateTime.UtcNow)
        };
        return publication;
    }

    private static Publication CreateVehicle(
        ApplicationUser user,
        PublicationCategoryLookup categoryLookup,
        string title,
        string category,
        string locality,
        decimal price,
        string shortDescription,
        string longDescription,
        string imageUrl,
        double latitude,
        double longitude,
        string vehicleType,
        string brand,
        string model,
        int year,
        int kilometers,
        string fuel,
        string transmission,
        string version,
        string color,
        string condition,
        string equipment,
        params (string Key, string Value)[] extras)
    {
        var publication = new Publication
        {
            Group = PublicationGroup.Rodados,
            CategoryId = ResolveCategoryId(categoryLookup, PublicationGroup.Rodados, category),
            Title = title,
            Price = price,
            Currency = "USD",
            Locality = locality,
            ShortDescription = shortDescription,
            LongDescription = longDescription,
            ContactName = user.Name,
            ContactPhone = user.Phone,
            ContactEmail = user.Email,
            User = user,
            Featured = price > 18000,
            Status = "Activa",
            Latitude = latitude,
            Longitude = longitude,
            MediaItems = PublicationMediaBuilder.Build(
                BuildImagesCsv(imageUrl, VehicleGalleryPool),
                null,
                DateTime.UtcNow)
        };
        return publication;
    }

    private static Publication CreateGeneral(
        ApplicationUser user,
        PublicationCategoryLookup categoryLookup,
        string title,
        string category,
        string locality,
        decimal price,
        string shortDescription,
        string longDescription,
        string imageUrl,
        double latitude,
        double longitude,
        string subcategory,
        string itemCondition,
        string? brand,
        string? model,
        string? warranty,
        string? shipping,
        params (string Key, string Value)[] extras)
    {
        var publication = new Publication
        {
            Group = PublicationGroup.Generales,
            CategoryId = ResolveCategoryId(categoryLookup, PublicationGroup.Generales, category),
            Title = title,
            Price = price,
            Currency = "USD",
            Locality = locality,
            ShortDescription = shortDescription,
            LongDescription = longDescription,
            ContactName = user.Name,
            ContactPhone = user.Phone,
            ContactEmail = user.Email,
            User = user,
            Featured = price > 700,
            Status = "Activa",
            Latitude = latitude,
            Longitude = longitude,
            MediaItems = PublicationMediaBuilder.Build(
                BuildImagesCsv(imageUrl, GeneralGalleryPool),
                null,
                DateTime.UtcNow)
        };
        return publication;
    }

    private static async Task<PublicationCategoryLookup> BuildCategoryLookupAsync(VentagramDbContext db)
    {
        var categories = await db.PublicationCategories
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Group, x.Name })
            .ToListAsync();

        var byKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var fallbackByGroup = new Dictionary<PublicationGroup, int>();

        foreach (var category in categories)
        {
            var key = $"{(byte)category.Group}|{NormalizeCategoryKey(category.Name)}";
            byKey[key] = category.Id;

            if (!fallbackByGroup.ContainsKey(category.Group))
            {
                fallbackByGroup[category.Group] = category.Id;
            }
        }

        return new PublicationCategoryLookup(byKey, fallbackByGroup);
    }

    private static int ResolveCategoryId(PublicationCategoryLookup lookup, PublicationGroup group, string category)
    {
        var key = $"{(byte)group}|{NormalizeCategoryKey(category)}";
        if (lookup.ByKey.TryGetValue(key, out var id))
        {
            return id;
        }

        if (lookup.FallbackByGroup.TryGetValue(group, out id))
        {
            return id;
        }

        throw new InvalidOperationException($"No se encontro una categoria activa para el grupo {group}.");
    }

    private static string NormalizeCategoryKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static async Task BackfillDynamicFieldValuesAsync(VentagramDbContext db)
    {
        var publications = await db.Publications
            .Include(x => x.Category)
            .Include(x => x.FieldValues)
            .ToListAsync();

        var fieldDefinitions = await db.PublicationCategoryFields
            .Where(x => x.IsActive)
            .ToListAsync();

        var definitionsByCategory = fieldDefinitions
            .Where(x => x.CategoryId.HasValue)
            .GroupBy(x => x.CategoryId)
            .ToDictionary(
                x => x.Key!.Value,
                x => x.ToDictionary(y => y.InternalName, StringComparer.OrdinalIgnoreCase));

        var changed = false;
        foreach (var publication in publications)
        {
            if (!definitionsByCategory.TryGetValue(publication.CategoryId, out var fields))
            {
                continue;
            }

            foreach (var value in BuildBackfillValues(publication, fields))
            {
                if (publication.FieldValues.Any(x => x.CategoryFieldId == value.CategoryFieldId))
                {
                    continue;
                }

                publication.FieldValues.Add(value);
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    private static IEnumerable<PublicationFieldValue> BuildBackfillValues(
        Publication publication,
        IReadOnlyDictionary<string, PublicationCategoryField> fields)
    {
        return [];
    }

    private static async Task BackfillGalleryImagesAsync(VentagramDbContext db, int demoUserId)
    {
        var publications = await db.Publications
            .Include(x => x.MediaItems)
            .ToListAsync();
        var changed = false;
        var seedGalleryImages = new HashSet<string>(
            PropertyGalleryPool
                .Concat(VehicleGalleryPool)
                .Concat(GeneralGalleryPool),
            StringComparer.OrdinalIgnoreCase);

        foreach (var publication in publications)
        {
            var currentImages = publication.MediaItems
                .Where(x => x.MediaType == PublicationMediaType.Image && !string.IsNullOrWhiteSpace(x.Url))
                .OrderBy(x => x.SortOrder)
                .Select(x => x.Url)
                .ToArray();

            if (publication.UserId != demoUserId)
            {
                if (currentImages.Length <= 1)
                {
                    continue;
                }

                var cleanedImages = currentImages
                    .Take(1)
                    .Concat(currentImages.Skip(1).Where(x => !seedGalleryImages.Contains(x)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(11)
                    .ToArray();

                if (currentImages.SequenceEqual(cleanedImages, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                publication.MediaItems.RemoveAll(x => x.MediaType == PublicationMediaType.Image);
                publication.MediaItems.AddRange(PublicationMediaBuilder.Build(string.Join(",", cleanedImages), null, publication.CreatedAtUtc));
                changed = true;
                continue;
            }

            if (currentImages.Length >= 2)
            {
                continue;
            }

            var primary = currentImages.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(primary))
            {
                continue;
            }

            var rebuilt = publication.Group switch
            {
                PublicationGroup.Inmuebles => BuildImagesCsv(primary, PropertyGalleryPool),
                PublicationGroup.Rodados => BuildImagesCsv(primary, VehicleGalleryPool),
                _ => BuildImagesCsv(primary, GeneralGalleryPool)
            };

            publication.MediaItems.RemoveAll(x => x.MediaType == PublicationMediaType.Image);
            publication.MediaItems.AddRange(PublicationMediaBuilder.Build(rebuilt, null, publication.CreatedAtUtc));
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    private static string BuildImagesCsv(string primaryImage, params string[] pool)
    {
        return string.Join(",",
            new[] { primaryImage }
                .Concat(pool)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(11));
    }

    private sealed record CitySeed(string Locality, string Zone, double Latitude, double Longitude);
    private sealed record CategoryFieldSeed(
        string InternalName,
        string Label,
        PublicationCategoryFieldDataType DataType,
        bool Required,
        int SortOrder,
        string? Unit = null,
        string? OptionsCsv = null);
    private sealed record PropertySeed(string Category, string PropertyType, string Operation);
    private sealed record VehicleSeed(string Category, string VehicleType, string Brand, string Model, string Transmission, string Fuel);
    private sealed record GeneralSeed(string Category, string Subcategory, string Brand, string Model, string Warranty, string Shipping);
    private sealed record PublicationCategoryLookup(Dictionary<string, int> ByKey, Dictionary<PublicationGroup, int> FallbackByGroup);
}

