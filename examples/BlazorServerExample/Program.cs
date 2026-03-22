using BlazorServerExample.Components;
using BlazorServerExample.Models;
using Microsoft.AspNetCore.Identity;
using Raven.Client.Documents;
using RavenDB.AspNetCore.Identity.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure RavenDB
var ravenSettings = builder.Configuration.GetSection("RavenDB");
var documentStore = new DocumentStore
{
    Urls = ravenSettings.GetSection("Urls").Get<string[]>() ?? new[] { "http://localhost:8080" },
    Database = ravenSettings["Database"] ?? "BlazorIdentityExample"
};

var certPath = ravenSettings["CertificatePath"];
if (!string.IsNullOrEmpty(certPath))
{
    // Make path absolute if it's relative
    var absoluteCertPath = Path.IsPathRooted(certPath)
        ? certPath
        : Path.Combine(builder.Environment.ContentRootPath, certPath);

    if (!File.Exists(absoluteCertPath))
    {
        throw new FileNotFoundException(
            $"RavenDB certificate file not found at: {absoluteCertPath}. " +
            $"Please ensure the certificate file exists or remove 'CertificatePath' from appsettings.json for development.");
    }

    try
    {
        var certPassword = ravenSettings["CertificatePassword"];

        // Load the certificate with appropriate limits for RavenDB Cloud certificates
        var loaderLimits = new System.Security.Cryptography.X509Certificates.Pkcs12LoaderLimits
        {
            AllowDuplicateAttributes = true // RavenDB Cloud certificates may have duplicate attributes
        };

        documentStore.Certificate = string.IsNullOrEmpty(certPassword)
            ? System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(absoluteCertPath)
            : System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(absoluteCertPath, certPassword,
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.DefaultKeySet, loaderLimits);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            $"Failed to load RavenDB certificate from: {absoluteCertPath}. " +
            $"Error: {ex.Message}", ex);
    }
}

// Configure ID generation conventions (optional - HiLo is the default)
// For email-based IDs:
// documentStore.Conventions.RegisterAsyncIdConvention<ApplicationUser>(
//     (dbName, user) => Task.FromResult($"ApplicationUsers/{user.Email}"));

documentStore.Initialize();

// Register document store as singleton
builder.Services.AddSingleton<IDocumentStore>(documentStore);

// Register document session as scoped
builder.Services.AddScoped(provider =>
{
    var store = provider.GetRequiredService<IDocumentStore>();
    return store.OpenAsyncSession();
});

// Configure ASP.NET Core Identity with RavenDB
builder.Services
    .AddIdentity<ApplicationUser, RavenDB.AspNetCore.Identity.Models.RavenIdentityRole>(options =>
    {
        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User settings
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedPhoneNumber = false;
    })
    .AddRavenDbIdentityStores<ApplicationUser, RavenDB.AspNetCore.Identity.Models.RavenIdentityRole>(configure: options =>
    {
        options.AutoSaveChanges = false; // Handle saves in middleware
    })
    .AddDefaultTokenProviders();

// Add authentication state and authorization
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Middleware to save RavenDB changes after each request
app.Use(async (context, next) =>
{
    await next();

    if (context.RequestServices.GetService<Raven.Client.Documents.Session.IAsyncDocumentSession>() is { } session)
    {
        if (session.Advanced.HasChanges)
        {
            await session.SaveChangesAsync();
        }
    }
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
