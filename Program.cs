using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Sahspn.ArtHistoire.Configuration;
using Sahspn.ArtHistoire.Data;
using Sahspn.ArtHistoire.Models;
using Sahspn.ArtHistoire.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
builder.Services.AddOptions<AdminBootstrapOptions>()
    .BindConfiguration(AdminBootstrapOptions.SectionName);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Denied";
        options.Cookie.Name = "SAHSPN.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization();
builder.Services.AddOptions<DatabaseOptions>()
    .BindConfiguration(DatabaseOptions.SectionName)
    .Validate(options =>
        string.Equals(options.Provider, "MySql", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(options.Provider, "MongoDb", StringComparison.OrdinalIgnoreCase),
        "Database:Provider must be either 'MySql' or 'MongoDb'.")
    .ValidateOnStart();

var provider = builder.Configuration[$"{DatabaseOptions.SectionName}:Provider"]?.Trim();
if (string.IsNullOrWhiteSpace(provider))
{
    throw new InvalidOperationException(
        "Database:Provider is required. Set it to 'MySql' or 'MongoDb' in appsettings.json.");
}

switch (provider.ToLowerInvariant())
{
    case "mysql":
        builder.Services.AddOptions<MySqlOptions>()
            .BindConfiguration(MySqlOptions.SectionName)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "MySql:ConnectionString is required when Database:Provider is 'MySql'.")
            .ValidateOnStart();

        builder.Services.AddDbContext<SahspDbContext>((serviceProvider, options) =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<MySqlOptions>>().Value;
            options.UseMySql(settings.ConnectionString, new MySqlServerVersion(new Version(8, 4, 0)));
        });
        builder.Services.AddScoped<ISiteContentStore, MySqlSiteContentStore>();
        builder.Services.AddScoped<IAdminManagementStore, MySqlAdminManagementStore>();
        builder.Services.AddScoped<IShopCatalogStore, MySqlShopCatalogStore>();
        builder.Services.AddScoped<IPartnerStore, MySqlPartnerStore>();
        builder.Services.AddScoped<IDocumentStudyStore, MySqlDocumentStudyStore>();
        builder.Services.AddScoped<ILibraryStore, MySqlLibraryStore>();
        break;

    case "mongodb":
        builder.Services.AddOptions<MongoDbOptions>()
            .BindConfiguration(MongoDbOptions.SectionName)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "MongoDb:ConnectionString is required when Database:Provider is 'MongoDb'.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DatabaseName),
                "MongoDb:DatabaseName is required when Database:Provider is 'MongoDb'.")
            .ValidateOnStart();

        // MongoClient owns connection pools and is thread-safe: reuse one instance application-wide.
        builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<MongoDbOptions>>().Value;
            return new MongoClient(settings.ConnectionString);
        });
        builder.Services.AddSingleton<ISiteContentStore, MongoSiteContentStore>();
        builder.Services.AddSingleton<IAdminManagementStore, MongoAdminManagementStore>();
        builder.Services.AddSingleton<IShopCatalogStore, MongoShopCatalogStore>();
        builder.Services.AddSingleton<IPartnerStore, MongoPartnerStore>();
        builder.Services.AddSingleton<IDocumentStudyStore, MongoDocumentStudyStore>();
        builder.Services.AddSingleton<ILibraryStore, MongoLibraryStore>();
        break;

    default:
        throw new InvalidOperationException(
            $"Unsupported Database:Provider '{provider}'. Supported values: MySql, MongoDb.");
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

var configuredUploadsRoot = Environment.GetEnvironmentVariable("Uploads__RootPath");
if (!string.IsNullOrWhiteSpace(configuredUploadsRoot))
{
    var uploadsRoot = Path.GetFullPath(configuredUploadsRoot);
    Directory.CreateDirectory(uploadsRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsRoot),
        RequestPath = "/uploads"
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Only the provider selected above is registered and initialized.
try
{
    await using var scope = app.Services.CreateAsyncScope();
    var store = scope.ServiceProvider.GetRequiredService<ISiteContentStore>();
    await store.InitializeAsync();
    var bootstrap = scope.ServiceProvider.GetRequiredService<IOptions<AdminBootstrapOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(bootstrap.Password))
    {
        var existing = await store.FindAdminUserAsync(bootstrap.Username);
        if (existing is null)
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AdminUser>>();
            var user = new AdminUser { Username = bootstrap.Username.Trim(), PasswordHash = "pending" };
            user.PasswordHash = hasher.HashPassword(user, bootstrap.Password);
            await store.CreateAdminUserAsync(user);
            app.Logger.LogWarning("Initial administrator '{Username}' was provisioned. Remove AdminBootstrap password from the environment after first startup.", user.Username);
        }
    }
    else
    {
        app.Logger.LogWarning("AdminBootstrap password is not configured. No initial administrator was provisioned.");
    }
}
catch (Exception exception)
{
    app.Logger.LogCritical(exception,
        "The selected database provider '{DatabaseProvider}' could not be initialized. " +
        "Verify that the selected database server is running and its configuration is correct.", provider);
    throw;
}

await app.RunAsync();
