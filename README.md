# RavenDB.AspNetCore.Identity

[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![RavenDB](https://img.shields.io/badge/RavenDB-7.2.1-red)](https://ravendb.net/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A RavenDB-based implementation of ASP.NET Core Identity, providing seamless integration between Microsoft's Identity framework and RavenDB's document database.

## Features

- ✅ **Full ASP.NET Core Identity Support** - Drop-in replacement for Entity Framework providers
- ✅ **Email Uniqueness with Compare/Exchange** - Cluster-wide email uniqueness using RavenDB's atomic operations
- ✅ **Flexible ID Generation** - Multiple strategies (HiLo, Email-based, Server-generated, Cluster-wide, Custom)
- ✅ **Generic Store Architecture** - Extend users and roles with custom properties
- ✅ **Automatic Email Normalization** - Built-in case-insensitive email handling
- ✅ **Session Management** - Efficient RavenDB session handling with middleware support
- ✅ **.NET 10.0** - Built for the latest .NET

## Quick Start

### Installation

```bash
dotnet add package RavenDB.AspNetCore.Identity
```

### Basic Setup

```csharp
// Program.cs
using RavenDB.AspNetCore.Identity.Extensions;
using RavenDB.AspNetCore.Identity.Models;
using Raven.Client.Documents;

var builder = WebApplication.CreateBuilder(args);

// Configure RavenDB
var documentStore = new DocumentStore
{
    Urls = new[] { "http://localhost:8080" },
    Database = "MyAppDatabase"
};

// Optional: Configure custom ID generation (before Initialize)
// documentStore.Conventions.RegisterAsyncIdConvention<MyUser>(
//     (dbName, user) => Task.FromResult($"MyUsers/{user.Email}"));

documentStore.Initialize();

builder.Services.AddSingleton<IDocumentStore>(documentStore);
builder.Services.AddScoped(provider =>
    provider.GetRequiredService<IDocumentStore>().OpenAsyncSession());

// Configure Identity with RavenDB
builder.Services
    .AddIdentity<MyUser, RavenIdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.User.RequireUniqueEmail = true;
    })
    .AddRavenDbIdentityStores<MyUser>(configure: options =>
    {
        options.AutoSaveChanges = false; // Use middleware for saves
    })
    .AddDefaultTokenProviders();

var app = builder.Build();

// Middleware to auto-save RavenDB changes
app.Use(async (context, next) =>
{
    await next();
    if (context.RequestServices.GetService<IAsyncDocumentSession>() is { } session
        && session.Advanced.HasChanges)
    {
        await session.SaveChangesAsync();
    }
});

app.Run();
```

### Custom User Model

```csharp
public class MyUser : RavenIdentityUser
{
    public string DisplayName { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}
```

## ID Generation Strategies

Configure ID generation directly on `DocumentStore.Conventions` before calling `Initialize()`:

### HiLo (Default - Recommended)

```csharp
// No configuration needed - RavenDB uses HiLo by default
var documentStore = new DocumentStore { /* ... */ };
documentStore.Initialize();
```
**Result:** `AppUsers/10-A`, `AppUsers/11-A`

### Email-Based Semantic IDs

```csharp
documentStore.Conventions.RegisterAsyncIdConvention<MyUser>(
    (dbName, user) => Task.FromResult($"AppUsers/{user.Email}"));
documentStore.Initialize();
```
**Result:** `AppUsers/user@example.com`

### Username-Based Semantic IDs

```csharp
documentStore.Conventions.RegisterAsyncIdConvention<MyUser>(
    (dbName, user) => Task.FromResult($"AppUsers/{user.UserName}"));
documentStore.Initialize();
```
**Result:** `AppUsers/johndoe`

### Server-Generated IDs

```csharp
documentStore.Conventions.RegisterAsyncIdConvention<MyUser>(
    (_, _) => Task.FromResult("AppUsers/"));
documentStore.Initialize();
```
**Result:** `AppUsers/000000000000000027-A`

### Cluster-Wide Consecutive IDs

```csharp
documentStore.Conventions.RegisterAsyncIdConvention<MyUser>(
    (_, _) => Task.FromResult("AppUsers|")); // ⚠️ Performance overhead
documentStore.Initialize();
```
**Result:** `AppUsers/1`, `AppUsers/2` (use only for legal/compliance requirements)

### Custom Convention

```csharp
documentStore.Conventions.RegisterAsyncIdConvention<MyUser>(
    (dbName, user) => Task.FromResult($"users/{user.Email?.Split('@')[0]}"));
documentStore.Initialize();
```
**Result:** `users/john`

## Examples

### Blazor Server Example

A complete Blazor Server application with authentication is available in [`examples/BlazorServerExample`](examples/BlazorServerExample).

**Features:**
- User registration
- Login/logout
- Custom user properties
- RavenDB integration

**Run the example:**
```bash
cd examples/BlazorServerExample
dotnet run
```

See the [example README](examples/BlazorServerExample/README.md) for detailed instructions.

## Architecture

### Email Uniqueness

The library uses RavenDB's **compare/exchange** mechanism to ensure email uniqueness across the cluster:

1. **User Creation** - 3-step atomic process:
   - Reserve email with empty user ID
   - Store user document
   - Update reservation with actual user ID

2. **User Update** - Atomic email change:
   - Create new email reservation
   - Delete old reservation

3. **User Deletion** - Cleanup:
   - Delete user document
   - Remove email reservation

### Email Normalization

All emails are automatically normalized to lowercase:
- `RavenIdentityUser.Email` setter normalizes on assignment
- `NormalizedEmail` value object enforces normalization
- Compare/exchange keys use normalized emails

### Session Management

The stores receive `IAsyncDocumentSession` via dependency injection. You control when changes are saved:

**Option 1: Middleware** (Recommended)
```csharp
app.Use(async (context, next) =>
{
    await next();
    var session = context.RequestServices.GetService<IAsyncDocumentSession>();
    if (session?.Advanced.HasChanges == true)
        await session.SaveChangesAsync();
});
```

**Option 2: Auto-save** (Not recommended)
```csharp
.AddRavenDbIdentityStores<MyUser>(configure: options =>
{
    options.AutoSaveChanges = true; // Saves immediately on every operation
})
```

## API Reference

### RavenDbIdentityOptions

Configuration options passed to `AddRavenDbIdentityStores()`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `UseStaticIndexes` | `bool` | `false` | Use static indexes (requires deployment) |
| `AutoSaveChanges` | `bool` | `false` | Auto-save after each operation |

### ID Generation

Configure via `DocumentStore.Conventions` before calling `Initialize()`:

| Strategy | Code | Example ID |
|----------|------|------------|
| **HiLo** (Default) | _(no configuration)_ | `AppUsers/10-A` |
| **Email-Based** | `RegisterAsyncIdConvention<TUser>((_, user) => ...)` | `AppUsers/user@example.com` |
| **Username-Based** | `RegisterAsyncIdConvention<TUser>((_, user) => ...)` | `AppUsers/johndoe` |
| **Server-Generated** | `RegisterAsyncIdConvention<TUser>((_, _) => "AppUsers/")` | `AppUsers/000000027-A` |
| **Cluster-Wide** | `RegisterAsyncIdConvention<TUser>((_, _) => "AppUsers\|")` | `AppUsers/1` |
| **Custom** | `RegisterAsyncIdConvention<TUser>((dbName, user) => ...)` | Custom |

### Store Implementations

**RavenUserStore<TUser>** (Fully Implemented)
- ✅ `IUserStore<TUser>`
- ✅ `IUserPasswordStore<TUser>`
- ✅ `IUserEmailStore<TUser>`
- ✅ `IUserLoginStore<TUser>`
- ✅ `IUserLockoutStore<TUser>`
- ✅ `IUserPhoneNumberStore<TUser>`
- ✅ `IUserSecurityStampStore<TUser>`
- ⚠️ `IUserRoleStore<TUser>` (stub - returns empty results)

**RavenRoleStore<TRole>** (Stub Implementation)
- ❌ All methods throw `NotImplementedException`

## Known Limitations

1. **Role Management** - `RavenRoleStore` is not implemented (all methods throw `NotImplementedException`)
2. **User Roles** - `IUserRoleStore` methods return empty results (users cannot be assigned to roles)
3. **Phone Number** - Basic storage only, minimal implementation

## Requirements

- **.NET 10.0** or higher
- **RavenDB.Client 7.2.1** or higher
- **Microsoft.AspNetCore.Identity 2.3.9** or higher

## RavenDB Setup

### Development (Docker)

```bash
docker run -d \
  -p 8080:8080 \
  -e RAVEN_ARGS="--Setup.Mode=None" \
  -e RAVEN_Security_UnsecuredAccessAllowed=PublicNetwork \
  --name ravendb \
  ravendb/ravendb:latest
```

### Production

Download from [ravendb.net](https://ravendb.net/download) or use [RavenDB Cloud](https://cloud.ravendb.net/).

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

### Development Guidelines

1. Follow existing code patterns
2. Update CLAUDE.md with architectural changes
3. Add tests for new functionality
4. Update README with new features

## License

[MIT License](LICENSE)

## Resources

- [RavenDB Documentation](https://ravendb.net/docs)
- [ASP.NET Core Identity Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity)
- [Example Application](examples/BlazorServerExample)
- [CLAUDE.md](CLAUDE.md) - Developer documentation

## Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/RavenDB.AspNetCore.Identity/issues)
- **RavenDB Support**: [RavenDB Community](https://groups.google.com/g/ravendb)
- **ASP.NET Support**: [ASP.NET Core GitHub](https://github.com/dotnet/aspnetcore)

---

Made with ❤️ for the RavenDB and .NET communities