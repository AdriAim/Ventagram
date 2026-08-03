using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Ventagram.Data;
using Ventagram.Models;
using Ventagram.Services;

var builder = WebApplication.CreateBuilder(args);
var configuredUrls = builder.Configuration["urls"]
    ?? builder.Configuration["ASPNETCORE_URLS"]
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
    ?? string.Empty;
var hasHttpsUrlConfigured = configuredUrls
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
var sharedApplicationName = builder.Configuration["Authentication:SharedApplicationName"] ?? "Ventagram";

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<VentagramDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = builder.Configuration["Authentication:CookieName"] ?? ".Ventagram.Auth";
        options.Cookie.Domain = builder.Configuration["Authentication:CookieDomain"];
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
    });

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.Events.OnTicketReceived = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<VentagramDbContext>();
                var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrWhiteSpace(email))
                {
                    return;
                }

                var name = context.Principal?.Identity?.Name
                    ?? context.Principal?.FindFirstValue(ClaimTypes.Name)
                    ?? email;

                var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email.ToLower());
                if (user is null)
                {
                    user = new ApplicationUser
                    {
                        Name = name,
                        Email = email.ToLower(),
                        Phone = string.Empty,
                        RespondsEmails = false,
                        AcceptsCalls = true,
                        RespondsWhatsApp = true,
                        ContactPreference = "Calls|WhatsApp",
                        PasswordHash = string.Empty,
                        AuthProvider = "Google"
                    };
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new(ClaimTypes.Name, user.Name),
                    new(ClaimTypes.Email, user.Email),
                    new("phone", user.Phone ?? string.Empty),
                    new("contact-preference", BuildContactPreference(user.RespondsEmails, user.AcceptsCalls, user.RespondsWhatsApp)),
                    new("provider", user.AuthProvider),
                    new("is-admin", user.IsAdmin ? "true" : "false")
                };

                context.Principal = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
                if (context.Properties is not null)
                {
                    context.Properties.IsPersistent = true;
                }

                context.Response.Cookies.Delete(NavigationLocalityService.CookieName);
            };
        });
}

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName(sharedApplicationName);
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<CloudflareR2ImageStorageService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PublicationService>();
builder.Services.AddScoped<PublicationGroupTypeService>();
builder.Services.AddScoped<PublicationCategoryService>();
builder.Services.AddScoped<PublicationCategoryFieldService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<FavoriteService>();
builder.Services.AddScoped<SuggestionService>();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddScoped<NavigationLocalityService>();
builder.Services.AddHostedService<PublicationExpirationWorker>();

var app = builder.Build();
app.UseForwardedHeaders();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VentagramDbContext>();
    db.Database.EnsureCreated();
    await EnsureArgentineLocalitiesTableAsync(db);
    await EnsurePublicationGroupTypesTableAsync(db);
    await EnsureUserLocalityColumnAsync(db);
    await SeedArgentineLocalitiesAsync(db);
    await EnsurePublicationGroupColumnAsync(db);
    await EnsureUserContactColumnsAsync(db);
    await EnsurePublicationCategoriesTableAsync(db);
    await EnsurePublicationCategoryFieldsTableAsync(db);
    await EnsurePublicationFieldValuesTableAsync(db);
    await EnsurePublicationCategoryIdColumnAsync(db);
    await EnsurePublicationReportReasonsTableAsync(db);
    await EnsureFavoriteListsTableAsync(db);
    await EnsureFavoriteListItemsTableAsync(db);
    await EnsureSiteSuggestionsTableAsync(db);
    await SeedData.InitializeAsync(db);
    await EnsurePublicationMediaTableAsync(db);
    await EnsurePublicationCategoryIdColumnAsync(db);
    await EnsurePublicationReportCommentColumnAsync(db);
    await EnsureModerationColumnsAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    if (hasHttpsUrlConfigured)
    {
        app.UseHsts();
    }
}

if (hasHttpsUrlConfigured)
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();
app.Run();

static async Task EnsureUserContactColumnsAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await EnsureColumnAsync(connection, "Users", "RespondsEmails", "bit(1) NOT NULL DEFAULT b'0'");
        await EnsureColumnAsync(connection, "Users", "AcceptsCalls", "bit(1) NOT NULL DEFAULT b'0'");
        await EnsureColumnAsync(connection, "Users", "RespondsWhatsApp", "bit(1) NOT NULL DEFAULT b'0'");
        await EnsureColumnAsync(connection, "Users", "AllowsSiteChat", "bit(1) NOT NULL DEFAULT b'1'");

        await using var backfill = connection.CreateCommand();
        backfill.CommandText = """
            UPDATE Users
            SET
                RespondsEmails = CASE WHEN COALESCE(ContactPreference, '') LIKE '%Email%' THEN 1 ELSE RespondsEmails END,
                AcceptsCalls = CASE WHEN COALESCE(ContactPreference, '') LIKE '%Calls%' THEN 1 ELSE AcceptsCalls END,
                RespondsWhatsApp = CASE WHEN COALESCE(ContactPreference, '') LIKE '%WhatsApp%' THEN 1 ELSE RespondsWhatsApp END
            """;
        await backfill.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsurePublicationGroupColumnAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var check = connection.CreateCommand();
        check.CommandText = """
            SELECT DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Publications'
              AND COLUMN_NAME = 'Group'
            LIMIT 1
            """;

        var existingType = Convert.ToString(await check.ExecuteScalarAsync())?.ToLowerInvariant();
        if (existingType is "tinyint" or "smallint" or "mediumint" or "int" or "bigint")
        {
            return;
        }

        await using var backfill = connection.CreateCommand();
        backfill.CommandText = """
            UPDATE Publications
            SET `Group` = CASE `Group`
                WHEN 'Inmuebles' THEN 1
                WHEN 'Rodados' THEN 2
                WHEN 'Generales' THEN 3
                ELSE 1
            END
            """;
        await backfill.ExecuteNonQueryAsync();

        await using var alter = connection.CreateCommand();
        alter.CommandText = """
            ALTER TABLE Publications
            MODIFY COLUMN `Group` TINYINT UNSIGNED NOT NULL
            """;
        await alter.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureColumnAsync(System.Data.Common.DbConnection connection, string tableName, string columnName, string definition)
{
    await using var check = connection.CreateCommand();
    check.CommandText = """
        SELECT COUNT(*)
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @tableName
          AND COLUMN_NAME = @columnName
        """;
    var tableParameter = check.CreateParameter();
    tableParameter.ParameterName = "@tableName";
    tableParameter.Value = tableName;
    check.Parameters.Add(tableParameter);
    var parameter = check.CreateParameter();
    parameter.ParameterName = "@columnName";
    parameter.Value = columnName;
    check.Parameters.Add(parameter);

    var exists = Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;
    if (exists)
    {
        return;
    }

    await using var alter = connection.CreateCommand();
    alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition}";
    await alter.ExecuteNonQueryAsync();
}

static async Task EnsureUserLocalityColumnAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await EnsureColumnAsync(connection, "Users", "ArgentineLocalityId", "int NULL");
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureArgentineLocalitiesTableAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS ArgentineLocalities (
                Id INT NOT NULL AUTO_INCREMENT,
                Locality VARCHAR(120) NOT NULL,
                Province VARCHAR(120) NOT NULL,
                Latitude DOUBLE NOT NULL,
                Longitude DOUBLE NOT NULL,
                SortOrder INT NOT NULL DEFAULT 0,
                IsActive BIT(1) NOT NULL DEFAULT b'1',
                PRIMARY KEY (Id),
                UNIQUE KEY UX_ArgentineLocalities_Province_Locality (Province, Locality)
            )
            """;
        await create.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsurePublicationGroupTypesTableAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS PublicationGroupTypes (
                Id TINYINT UNSIGNED NOT NULL,
                Name VARCHAR(120) NOT NULL,
                SortOrder INT NOT NULL DEFAULT 0,
                IsActive BIT(1) NOT NULL DEFAULT b'1',
                PRIMARY KEY (Id),
                UNIQUE KEY UX_PublicationGroupTypes_Name (Name)
            )
            """;
        await create.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task SeedArgentineLocalitiesAsync(VentagramDbContext db)
{
    var existingKeys = new HashSet<string>(
        await db.ArgentineLocalities
            .Select(x => $"{x.Province}|{x.Locality}")
            .ToListAsync(),
        StringComparer.OrdinalIgnoreCase);

    var missing = ArgentineLocalityCatalog.All
        .Where(x => !existingKeys.Contains($"{x.Province}|{x.Locality}"))
        .Select(x => new ArgentineLocality
        {
            Locality = x.Locality,
            Province = x.Province,
            Latitude = x.Latitude,
            Longitude = x.Longitude,
            SortOrder = x.SortOrder,
            IsActive = x.IsActive
        })
        .ToList();

    if (missing.Count == 0)
    {
        return;
    }

    db.ArgentineLocalities.AddRange(missing);
    await db.SaveChangesAsync();
}

static async Task EnsurePublicationCategoriesTableAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS PublicationCategories (
                Id INT NOT NULL AUTO_INCREMENT,
                `Group` TINYINT UNSIGNED NOT NULL,
                Name VARCHAR(120) NOT NULL,
                SortOrder INT NOT NULL DEFAULT 0,
                IsActive BIT(1) NOT NULL DEFAULT b'1',
                PRIMARY KEY (Id),
                UNIQUE KEY UX_PublicationCategories_Group_Name (`Group`, Name)
            )
            """;
        await create.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsurePublicationCategoryIdColumnAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await EnsureColumnAsync(connection, "Publications", "CategoryId", "INT NULL");

        await using (var categoryCountCheck = connection.CreateCommand())
        {
            categoryCountCheck.CommandText = """
                SELECT COUNT(*)
                FROM PublicationCategories
                """;

            var categoryCount = Convert.ToInt32(await categoryCountCheck.ExecuteScalarAsync());
            if (categoryCount == 0)
            {
                return;
            }
        }

        var hasLegacyCategoryColumn = false;
        await using (var checkLegacyCategory = connection.CreateCommand())
        {
            checkLegacyCategory.CommandText = """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'Publications'
                  AND COLUMN_NAME = 'Category'
                """;

            hasLegacyCategoryColumn = Convert.ToInt32(await checkLegacyCategory.ExecuteScalarAsync()) > 0;
        }

        if (hasLegacyCategoryColumn)
        {
            await using var backfillExact = connection.CreateCommand();
            backfillExact.CommandText = """
                UPDATE Publications p
                LEFT JOIN PublicationCategories c
                  ON c.`Group` = p.`Group`
                 AND c.Name = COALESCE(p.Category, '')
                SET p.CategoryId = c.Id
                WHERE p.CategoryId IS NULL OR p.CategoryId = 0
                """;
            await backfillExact.ExecuteNonQueryAsync();
        }

        await using (var backfillFallback = connection.CreateCommand())
        {
            backfillFallback.CommandText = """
                UPDATE Publications p
                JOIN (
                    SELECT `Group`, MIN(Id) AS CategoryId
                    FROM PublicationCategories
                    WHERE IsActive = b'1'
                    GROUP BY `Group`
                ) c ON c.`Group` = p.`Group`
                SET p.CategoryId = c.CategoryId
                WHERE p.CategoryId IS NULL OR p.CategoryId = 0
                """;
            await backfillFallback.ExecuteNonQueryAsync();
        }

        await using (var alter = connection.CreateCommand())
        {
            alter.CommandText = """
                ALTER TABLE Publications
                MODIFY COLUMN CategoryId INT NOT NULL
                """;
            await alter.ExecuteNonQueryAsync();
        }

        if (hasLegacyCategoryColumn)
        {
            await using var dropLegacyCategory = connection.CreateCommand();
            dropLegacyCategory.CommandText = "ALTER TABLE Publications DROP COLUMN Category";
            await dropLegacyCategory.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsurePublicationReportReasonsTableAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS PublicationReportReasons (
                Id INT NOT NULL AUTO_INCREMENT,
                Name VARCHAR(120) NOT NULL,
                SortOrder INT NOT NULL DEFAULT 0,
                IsActive BIT(1) NOT NULL DEFAULT b'1',
                PRIMARY KEY (Id),
                UNIQUE KEY UX_PublicationReportReasons_Name (Name)
            )
            """;
        await create.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsurePublicationCategoryFieldsTableAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS PublicationCategoryFields (
                Id INT NOT NULL AUTO_INCREMENT,
                CategoryId INT NOT NULL,
                InternalName VARCHAR(80) NOT NULL,
                Label VARCHAR(120) NOT NULL,
                DataType TINYINT UNSIGNED NOT NULL,
                Required BIT(1) NOT NULL DEFAULT b'0',
                SortOrder INT NOT NULL DEFAULT 0,
                IsActive BIT(1) NOT NULL DEFAULT b'1',
                OptionsCsv VARCHAR(1000) NULL,
                PRIMARY KEY (Id),
                UNIQUE KEY UX_PublicationCategoryFields_Category_InternalName (CategoryId, InternalName),
                KEY IX_PublicationCategoryFields_Category_Active_Order (CategoryId, IsActive, SortOrder)
            )
            """;
        await create.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsurePublicationFieldValuesTableAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS PublicationFieldValues (
                Id INT NOT NULL AUTO_INCREMENT,
                PublicationId INT NOT NULL,
                CategoryFieldId INT NOT NULL,
                ValueText VARCHAR(500) NULL,
                ValueNumber DECIMAL(18,2) NULL,
                ValueBoolean BIT(1) NULL,
                PRIMARY KEY (Id),
                UNIQUE KEY UX_PublicationFieldValues_Publication_Field (PublicationId, CategoryFieldId),
                KEY IX_PublicationFieldValues_Field_Text (CategoryFieldId, ValueText),
                KEY IX_PublicationFieldValues_Field_Number (CategoryFieldId, ValueNumber),
                KEY IX_PublicationFieldValues_Field_Boolean (CategoryFieldId, ValueBoolean)
            )
            """;
        await create.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsurePublicationMediaTableAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS PublicationMedia (
                Id INT NOT NULL AUTO_INCREMENT,
                PublicationId INT NOT NULL,
                SortOrder INT NOT NULL,
                MediaType TINYINT UNSIGNED NOT NULL,
                Url VARCHAR(1000) NOT NULL,
                IsPrimary BIT(1) NOT NULL DEFAULT b'0',
                CreatedAtUtc DATETIME(6) NOT NULL,
                PRIMARY KEY (Id),
                UNIQUE KEY UX_PublicationMedia_Publication_SortOrder (PublicationId, SortOrder),
                KEY IX_PublicationMedia_Publication_Type_Primary (PublicationId, MediaType, IsPrimary)
            )
            """;
        await create.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }

    await BackfillPublicationMediaAsync(db);
    await DropLegacyPublicationColumnsAsync(db);
}

static async Task BackfillPublicationMediaAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        if (!await ColumnExistsAsync(connection, "Publications", "ImagesCsv")
            && !await ColumnExistsAsync(connection, "Publications", "VideoUrl"))
        {
            return;
        }

        var existingPublicationIds = new HashSet<int>(
            await db.PublicationMedia
                .AsNoTracking()
                .Select(x => x.PublicationId)
                .Distinct()
                .ToListAsync());

        await using var select = connection.CreateCommand();
        select.CommandText = """
            SELECT Id, ImagesCsv, VideoUrl, CreatedAtUtc
            FROM Publications
            ORDER BY Id
            """;

        var publications = new List<(int Id, string? ImagesCsv, string? VideoUrl, DateTime CreatedAtUtc)>();
        await using (var reader = await select.ExecuteReaderAsync())
        {
            var idOrdinal = reader.GetOrdinal("Id");
            var imagesOrdinal = reader.GetOrdinal("ImagesCsv");
            var videoOrdinal = reader.GetOrdinal("VideoUrl");
            var createdOrdinal = reader.GetOrdinal("CreatedAtUtc");

            while (await reader.ReadAsync())
            {
                publications.Add((
                    reader.GetInt32(idOrdinal),
                    reader.IsDBNull(imagesOrdinal) ? null : reader.GetString(imagesOrdinal),
                    reader.IsDBNull(videoOrdinal) ? null : reader.GetString(videoOrdinal),
                    reader.GetDateTime(createdOrdinal)));
            }
        }

        var pendingMedia = new List<PublicationMedia>();
        foreach (var publication in publications)
        {
            if (existingPublicationIds.Contains(publication.Id))
            {
                continue;
            }

            pendingMedia.AddRange(PublicationMediaBuilder.Build(
                publication.ImagesCsv,
                publication.VideoUrl,
                publication.CreatedAtUtc)
                .Select((item, index) =>
                {
                    item.PublicationId = publication.Id;
                    item.SortOrder = index + 1;
                    return item;
                }));
        }

        if (pendingMedia.Count > 0)
        {
            db.PublicationMedia.AddRange(pendingMedia);
            await db.SaveChangesAsync();
        }
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task DropLegacyPublicationColumnsAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        if (await ColumnExistsAsync(connection, "Publications", "ImagesCsv"))
        {
            await using var dropImages = connection.CreateCommand();
            dropImages.CommandText = "ALTER TABLE Publications DROP COLUMN ImagesCsv";
            await dropImages.ExecuteNonQueryAsync();
        }

        if (await ColumnExistsAsync(connection, "Publications", "VideoUrl"))
        {
            await using var dropVideo = connection.CreateCommand();
            dropVideo.CommandText = "ALTER TABLE Publications DROP COLUMN VideoUrl";
            await dropVideo.ExecuteNonQueryAsync();
        }
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task<bool> ColumnExistsAsync(System.Data.Common.DbConnection connection, string tableName, string columnName)
{
    await using var check = connection.CreateCommand();
    check.CommandText = """
        SELECT COUNT(*)
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @tableName
          AND COLUMN_NAME = @columnName
        """;
    var tableParameter = check.CreateParameter();
    tableParameter.ParameterName = "@tableName";
    tableParameter.Value = tableName;
    check.Parameters.Add(tableParameter);
    var parameter = check.CreateParameter();
    parameter.ParameterName = "@columnName";
    parameter.Value = columnName;
    check.Parameters.Add(parameter);

    return Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;
}

static async Task EnsureFavoriteListsTableAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS FavoriteLists (
                Id INT NOT NULL AUTO_INCREMENT,
                UserId INT NOT NULL,
                Name VARCHAR(120) NOT NULL,
                CreatedAtUtc DATETIME(6) NOT NULL,
                UpdatedAtUtc DATETIME(6) NOT NULL,
                PRIMARY KEY (Id),
                UNIQUE KEY UX_FavoriteLists_User_Name (UserId, Name),
                KEY IX_FavoriteLists_User_Updated (UserId, UpdatedAtUtc)
            )
            """;
        await create.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureFavoriteListItemsTableAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS FavoriteListItems (
                Id INT NOT NULL AUTO_INCREMENT,
                FavoriteListId INT NOT NULL,
                PublicationId INT NOT NULL,
                CreatedAtUtc DATETIME(6) NOT NULL,
                PRIMARY KEY (Id),
                UNIQUE KEY UX_FavoriteListItems_List_Publication (FavoriteListId, PublicationId),
                KEY IX_FavoriteListItems_Publication (PublicationId),
                KEY IX_FavoriteListItems_List_Created (FavoriteListId, CreatedAtUtc)
            )
            """;
        await create.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsurePublicationReportCommentColumnAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await EnsureColumnAsync(connection, "PublicationReports", "Comment", "VARCHAR(500) NULL");
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureSiteSuggestionsTableAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS SiteSuggestions (
                Id INT NOT NULL AUTO_INCREMENT,
                UserId INT NULL,
                SenderName VARCHAR(120) NULL,
                SenderEmail VARCHAR(160) NULL,
                Message VARCHAR(2000) NOT NULL,
                CreatedAtUtc DATETIME(6) NOT NULL,
                PRIMARY KEY (Id),
                KEY IX_SiteSuggestions_CreatedAtUtc (CreatedAtUtc),
                KEY IX_SiteSuggestions_UserId (UserId)
            )
            """;
        await create.ExecuteNonQueryAsync();
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureModerationColumnsAsync(VentagramDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State != System.Data.ConnectionState.Open;
    if (shouldClose)
    {
        await connection.OpenAsync();
    }

    try
    {
        await EnsureColumnAsync(connection, "Users", "IsAdmin", "bit(1) NOT NULL DEFAULT b'0'");
        await EnsureColumnAsync(connection, "Users", "CanPublish", "bit(1) NOT NULL DEFAULT b'1'");
        await EnsureColumnAsync(connection, "Users", "CanReport", "bit(1) NOT NULL DEFAULT b'1'");

        await EnsureColumnAsync(connection, "Publications", "ModerationStatus", "VARCHAR(40) NOT NULL DEFAULT 'None'");
        await EnsureColumnAsync(connection, "Publications", "ReportWarningSentAtUtc", "DATETIME(6) NULL");
        await EnsureColumnAsync(connection, "Publications", "ReportTrashSentAtUtc", "DATETIME(6) NULL");
        await EnsureColumnAsync(connection, "Publications", "TrashedAtUtc", "DATETIME(6) NULL");

        await EnsureColumnAsync(connection, "PublicationReports", "ReporterUserId", "INT NOT NULL DEFAULT 0");
        await EnsureColumnAsync(connection, "PublicationReports", "CountsTowardThreshold", "bit(1) NOT NULL DEFAULT b'1'");
        await EnsureColumnAsync(connection, "PublicationReports", "ReviewStatus", "VARCHAR(30) NOT NULL DEFAULT 'Pending'");
        await EnsureColumnAsync(connection, "PublicationReports", "ReviewedAtUtc", "DATETIME(6) NULL");
        await EnsureColumnAsync(connection, "PublicationReports", "ReviewedByUserId", "INT NULL");

        await using (var deleteInvalidReports = connection.CreateCommand())
        {
            deleteInvalidReports.CommandText = """
                DELETE FROM PublicationReports
                WHERE ReporterUserId = 0
                """;
            await deleteInvalidReports.ExecuteNonQueryAsync();
        }

        await using (var adjustReporterColumn = connection.CreateCommand())
        {
            adjustReporterColumn.CommandText = """
                ALTER TABLE PublicationReports
                MODIFY COLUMN ReporterUserId INT NOT NULL
                """;
            await adjustReporterColumn.ExecuteNonQueryAsync();
        }

        await EnsureIndexAsync(connection, "PublicationReports", "UX_PublicationReports_Publication_Reporter", "UNIQUE KEY UX_PublicationReports_Publication_Reporter (PublicationId, ReporterUserId)");
    }
    finally
    {
        if (shouldClose)
        {
            await connection.CloseAsync();
        }
    }
}

static async Task EnsureIndexAsync(System.Data.Common.DbConnection connection, string tableName, string indexName, string definition)
{
    await using var check = connection.CreateCommand();
    check.CommandText = """
        SELECT COUNT(*)
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = @tableName
          AND INDEX_NAME = @indexName
        """;
    var tableParameter = check.CreateParameter();
    tableParameter.ParameterName = "@tableName";
    tableParameter.Value = tableName;
    check.Parameters.Add(tableParameter);
    var indexParameter = check.CreateParameter();
    indexParameter.ParameterName = "@indexName";
    indexParameter.Value = indexName;
    check.Parameters.Add(indexParameter);

    var exists = Convert.ToInt32(await check.ExecuteScalarAsync()) > 0;
    if (exists)
    {
        return;
    }

    await using var alter = connection.CreateCommand();
    alter.CommandText = $"ALTER TABLE {tableName} ADD {definition}";
    await alter.ExecuteNonQueryAsync();
}

static string BuildContactPreference(bool respondsEmails, bool acceptsCalls, bool respondsWhatsApp)
{
    var preferences = new List<string>();
    if (respondsEmails)
    {
        preferences.Add("Email");
    }

    if (acceptsCalls)
    {
        preferences.Add("Calls");
    }

    if (respondsWhatsApp)
    {
        preferences.Add("WhatsApp");
    }

    return preferences.Count == 0 ? "None" : string.Join("|", preferences);
}
