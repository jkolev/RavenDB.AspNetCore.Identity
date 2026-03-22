# Blazor Server Example with RavenDB.AspNetCore.Identity

This is a working example of a Blazor Server application using **RavenDB.AspNetCore.Identity** for authentication and user management.

> **⚠️ Work in Progress:** This library is under active development. Role management and some IUserRoleStore methods are not yet implemented. See [Known Issues](https://github.com/jkolev/RavenDB.AspNetCore.Identity/issues) for details.

## Features

- ✅ User registration with email and password
- ✅ User login with persistent sessions
- ✅ User logout
- ✅ Custom user properties (DisplayName, RegisteredAt)
- ✅ Email-based user identification
- ✅ Account lockout after failed login attempts
- ✅ Integration with ASP.NET Core Identity

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [RavenDB Server](https://ravendb.net/download) (Community Edition is free)

## Running RavenDB

### Option 1: Docker (Recommended for Development)

```bash
docker run -d \
  -p 8080:8080 \
  -p 38888:38888 \
  -e RAVEN_ARGS="--Setup.Mode=None" \
  -e RAVEN_Security_UnsecuredAccessAllowed=PublicNetwork \
  --name ravendb \
  ravendb/ravendb:latest
```

Access RavenDB Studio at: http://localhost:8080

### Option 2: Download and Install

1. Download RavenDB from https://ravendb.net/download
2. Extract and run `./Server/Raven.Server`
3. Follow the setup wizard
4. Access RavenDB Studio at the URL shown in the console

## Configuration

The application is configured to connect to RavenDB via `appsettings.json`:

```json
{
  "RavenDB": {
    "Urls": [ "http://localhost:8080" ],
    "Database": "BlazorIdentityExample",
    "CertificatePath": null,
    "CertificatePassword": null
  }
}
```

### Using a Secure Connection (Production)

For production environments with SSL/TLS:

```json
{
  "RavenDB": {
    "Urls": [ "https://a.your-ravendb-cluster.ravendb.cloud" ],
    "Database": "YourDatabaseName",
    "CertificatePath": "/path/to/your/client/certificate.pfx",
    "CertificatePassword": "your-certificate-password"
  }
}
```

## Running the Example

1. **Ensure RavenDB is running** (see above)

2. **Navigate to the example directory:**
   ```bash
   cd examples/BlazorServerExample
   ```

3. **Run the application:**
   ```bash
   dotnet run
   ```

4. **Open your browser:**
   ```
   https://localhost:5001
   ```

5. **Register a new user:**
   - Click "Register" in the navigation
   - Fill in your email, display name, and password
   - Submit the form
   - You'll be automatically logged in

6. **View the database in RavenDB Studio:**
   - Go to http://localhost:8080
   - Select "BlazorIdentityExample" database
   - Browse the "ApplicationUsers" collection
   - View the compare/exchange values to see email reservations

## Project Structure

```
BlazorServerExample/
├── Components/
│   ├── Account/
│   │   ├── Login.razor           # Login page
│   │   ├── Register.razor        # Registration page
│   │   ├── Logout.razor          # Logout handler
│   │   └── LoginDisplay.razor    # Login/logout display component
│   ├── Layout/
│   │   ├── NavMenu.razor         # Navigation with auth links
│   │   └── MainLayout.razor
│   └── Pages/
│       └── (Blazor pages)
├── Models/
│   └── ApplicationUser.cs        # Custom user model extending RavenIdentityUser
├── Program.cs                    # Application configuration
└── appsettings.json             # RavenDB connection settings
```

## Key Implementation Details

### Program.cs Configuration

The `Program.cs` file shows how to:
1. Configure RavenDB DocumentStore
2. Register Identity services with RavenDB stores
3. Configure password requirements
4. Set up authentication middleware
5. Automatically save RavenDB session changes after each request

### Custom User Model

```csharp
public class ApplicationUser : RavenIdentityUser
{
    public string? DisplayName { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}
```

You can extend `RavenIdentityUser` with any properties your application needs.

### ID Generation Strategy

This example uses the default **HiLo** algorithm for user IDs (no configuration needed).

To use a different strategy, configure `DocumentStore.Conventions` before calling `Initialize()`:

```csharp
// Example: Email-based IDs
documentStore.Conventions.RegisterAsyncIdConvention<ApplicationUser>(
    (dbName, user) => Task.FromResult($"ApplicationUsers/{user.Email}"));

documentStore.Initialize();
```

See the [main documentation](https://github.com/jkolev/RavenDB.AspNetCore.Identity#id-generation-strategies) for other ID generation strategies.

## Common Issues

### RavenDB Connection Failed

**Error:** `Cannot connect to RavenDB at http://localhost:8080`

**Solution:**
- Ensure RavenDB server is running
- Check the port (default: 8080)
- Verify firewall settings

### Database Not Created

The database will be created automatically by RavenDB when the first document is stored. If you see errors about missing database:
1. Open RavenDB Studio (http://localhost:8080)
2. Click "Create Database"
3. Enter "BlazorIdentityExample" as the database name

### Certificate Errors

If using SSL/TLS, ensure:
- Certificate path is correct and accessible
- Certificate password is correct
- Certificate is valid and not expired

## Learn More

- [RavenDB.AspNetCore.Identity Repository](https://github.com/jkolev/RavenDB.AspNetCore.Identity)
- [Report Issues](https://github.com/jkolev/RavenDB.AspNetCore.Identity/issues)
- [RavenDB Documentation](https://ravendb.net/docs)
- [ASP.NET Core Identity Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity)
- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor)

## License

This example is part of the RavenDB.AspNetCore.Identity library and is provided under the same license.