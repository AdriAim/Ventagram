using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Ubika.Data;
using Ubika.Models;
using Ubika.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<UbikaDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
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
                var db = context.HttpContext.RequestServices.GetRequiredService<UbikaDbContext>();
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
                    new("provider", user.AuthProvider)
                };

                context.Principal = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
                if (context.Properties is not null)
                {
                    context.Properties.IsPersistent = true;
                }
            };
        });
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PublicationService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<CurrentUserAccessor>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UbikaDbContext>();
    db.Database.EnsureCreated();
    await SeedData.InitializeAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();
app.Run();
