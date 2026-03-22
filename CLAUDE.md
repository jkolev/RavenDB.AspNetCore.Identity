# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

RavenDB.AspNetCore.Identity is a RavenDB-based implementation of ASP.NET Core Identity. It provides user and role stores that work with RavenDB's document database, leveraging RavenDB's cluster-wide compare/exchange mechanism for email uniqueness.

**Target Framework:** .NET 10.0
**Key Dependencies:** RavenDB.Client 7.2.1, Microsoft.AspNetCore.Identity 2.3.9

## Build and Development Commands

```bash
# Build the project
dotnet build

# Build the solution
dotnet build RavenDB.AspNetCore.Identity.sln

# Clean build artifacts
dotnet clean

# Restore packages
dotnet restore
```

## Architecture

### Email Uniqueness with Compare/Exchange

The library uses RavenDB's compare/exchange mechanism for ensuring email uniqueness across the cluster. This is critical to understand when working with user creation/updates:

1. **User Creation** (3-step process):
   - Reserve email with empty user ID
   - Store user and save to database
   - Update email reservation with actual user ID
   - If any step fails, the email reservation must be rolled back

2. **User Update**:
   - If email changes, create new reservation then delete old one
   - Check for email changes via `_session.Advanced.WhatChanged()`
   - Case-only email changes don't require reservation updates

3. **User Deletion**: Delete user first, then remove email reservation

See `RavenUserStore.cs` lines 67-141 (CreateAsync), 144-222 (UpdateAsync), 224-251 (DeleteAsync) for implementations.

### Email Normalization

Email normalization happens automatically:
- `RavenIdentityUser.Email` setter normalizes to lowercase (lines 43-48)
- `NormalizedEmail` value object enforces normalization at compile time
- All email operations use `NormalizedEmail` internally

### ID Generation Strategies

The library provides fluent methods for configuring document ID generation, leveraging RavenDB's native conventions:

**Available Strategies:**

1. **HiLo (Default)** - `UseHiLoIds()`
   - RavenDB's default algorithm (e.g., "AppUsers/10-A")
   - Efficient, conflict-free IDs with minimal server communication
   - Recommended for most scenarios

2. **Email-Based** - `UseEmailBasedIds<TUser>()`
   - Semantic IDs using email (e.g., "AppUsers/user@example.com")
   - Human-readable for debugging
   - Requires email uniqueness

3. **Username-Based** - `UseUserNameBasedIds<TUser>()`
   - Semantic IDs using username (e.g., "AppUsers/johndoe")
   - Human-readable for debugging
   - Requires username uniqueness

4. **Server-Generated** - `UseServerGeneratedIds<TUser>()`
   - Server assigns IDs (e.g., "AppUsers/000000000000000027-A")
   - Lowest server overhead
   - Best for bulk operations

5. **Cluster-Wide** - `UseClusterWideIds<TUser>()`
   - Consecutive IDs (e.g., "AppUsers/1", "AppUsers/2")
   - **Performance overhead** due to cluster coordination
   - Only use for legal/compliance requirements (invoices, receipts)

6. **Custom** - `UseCustomIdConvention<TUser>(Func<string, TUser, Task<string>>)`
   - Fully custom ID generation logic
   - Provides complete control over ID format

See `RavenDbIdentityOptions.cs` for implementation details.

### Store Implementations

**RavenUserStore<TUser>** (fully implemented):
- Generic store allowing users to extend with their own properties
- `TUser` must inherit from `RavenIdentityUser` and have a parameterless constructor
- Implements: IUserStore, IUserPasswordStore, IUserLockoutStore, IUserEmailStore, IUserLoginStore, IUserPhoneNumberStore, IUserSecurityStampStore
- IUserRoleStore: Methods throw NotImplementedException
- Uses IAsyncDocumentSession for all database operations
- FindByEmailAsync uses compare/exchange directly (not indexes) to avoid staleness

**RavenRoleStore<TRole>** (stub implementation):
- Generic store allowing users to extend with their own properties
- `TRole` must inherit from `RavenIdentityRole` and have a parameterless constructor
- All methods throw NotImplementedException
- This is a known incomplete feature

## Known Issues and Incomplete Features

1. **Role Management**: RavenRoleStore is completely unimplemented - all methods throw NotImplementedException
2. **User Roles**: IUserRoleStore methods in RavenUserStore are not implemented (AddToRoleAsync, RemoveFromRoleAsync, GetRolesAsync, IsInRoleAsync, GetUsersInRoleAsync)
3. **RavenIdentityRole**: Empty class placeholder (Models/RavenIdentityRole.cs)

## Key Conventions

- **Email Reservation Key Pattern**: `emails/{normalized-email-lowercase}`
- **Collection Naming**: Uses RavenDB conventions (e.g., "AppUsers/", "AppRoles/")
- **Identity Parts Separator**: Configurable via RavenDB conventions (typically "/")
- **Document IDs**: Follow RavenDB collection/id pattern based on UserIdType

## Configuration

### Registering the Stores

**Basic Setup:**

```csharp
// Register both user and role stores
services.AddIdentity<MyUser, MyRole>()
    .AddRavenDbIdentityStores<MyUser, MyRole>(documentStore, options =>
    {
        options.UseStaticIndexes = false;
        options.AutoSaveChanges = false;
    });

// Or register only user store (role store not registered)
services.AddIdentity<MyUser, IdentityRole>()
    .AddRavenDbIdentityStores<MyUser>(documentStore, options =>
    {
        options.AutoSaveChanges = false;
    });
```

**With ID Generation Strategy:**

```csharp
// Use email-based IDs
services.AddIdentity<MyUser, MyRole>()
    .AddRavenDbIdentityStores<MyUser, MyRole>(documentStore, options =>
    {
        options.UseEmailBasedIds<MyUser>();
        options.AutoSaveChanges = false;
    });

// Use server-generated IDs for bulk operations
services.AddIdentity<MyUser, MyRole>()
    .AddRavenDbIdentityStores<MyUser, MyRole>(documentStore, options =>
    {
        options.UseServerGeneratedIds<MyUser>();
        options.AutoSaveChanges = false;
    });

// Use custom ID convention
services.AddIdentity<MyUser, MyRole>()
    .AddRavenDbIdentityStores<MyUser, MyRole>(documentStore, options =>
    {
        options.UseCustomIdConvention<MyUser>((dbName, user) =>
            Task.FromResult($"users/{user.Email?.Split('@')[0]}"));
        options.AutoSaveChanges = false;
    });
```

Where `MyUser : RavenIdentityUser` and `MyRole : RavenIdentityRole`.

**Important:** The `IDocumentStore` must be passed as a parameter to configure ID conventions before `DocumentStore.Initialize()` is called.

### RavenDbIdentityOptions

`RavenDbIdentityOptions` (Infrastructure/RavenDbIdentityOptions.cs):

**Properties:**
- `UseStaticIndexes`: Whether to use static indexes (default: false). Requires deploying indexes to server.
- `AutoSaveChanges`: If true, changes are saved immediately. Leave false if save is handled in middleware (recommended).

**ID Configuration Methods:**
- `UseEmailBasedIds<TUser>()`: Email-based semantic IDs
- `UseUserNameBasedIds<TUser>()`: Username-based semantic IDs
- `UseHiLoIds()`: RavenDB's default HiLo algorithm (no-op, for explicitness)
- `UseServerGeneratedIds<TUser>()`: Server-assigned IDs (lowest overhead)
- `UseClusterWideIds<TUser>()`: Sequential cluster-wide IDs (performance impact)
- `UseCustomIdConvention<TUser>(Func<string, TUser, Task<string>>)`: Custom ID logic

All methods return `RavenDbIdentityOptions` for fluent chaining.

## Project Structure

```
src/RavenDB.AspNetCore.Identity/
├── Extensions/
│   └── IdentityBuilderExtensions.cs    # .AddRavenDbIdentityStores<TUser, TRole>() and <TUser>()
├── Infrastructure/
│   ├── Conventions.cs                  # Collection naming and email reservation helpers
│   └── RavenDbIdentityOptions.cs       # Configuration options with fluent ID generation methods
├── Models/
│   ├── RavenIdentityUser.cs            # Abstract base user class
│   ├── RavenIdentityRole.cs            # Base role class
│   └── IdentityUserAuthToken.cs        # OAuth token storage
├── Stores/
│   ├── RavenUserStore.cs               # Generic user store RavenUserStore<TUser>
│   └── RavenRoleStore.cs               # Generic role store RavenRoleStore<TRole> (stub)
└── ValueObjects/
    └── NormalizedEmail.cs              # Email normalization value object
```

## Important Notes

- **Generic Stores**: Both `RavenUserStore<TUser>` and `RavenRoleStore<TRole>` are generic, allowing users to extend with custom properties
- **Type Constraints**: User types must inherit from `RavenIdentityUser`, role types from `RavenIdentityRole`, both require parameterless constructors
- **ID Generation**: Configure via fluent methods in `RavenDbIdentityOptions`. Uses RavenDB's native conventions system. HiLo is the default and recommended for most scenarios.
- When modifying user creation/update/delete, always maintain the compare/exchange email reservation integrity
- Email lookups bypass indexes to avoid staleness - they use compare/exchange directly
- The library expects users to inherit from RavenIdentityUser (abstract class) and roles from RavenIdentityRole
- Session management is the caller's responsibility - the store receives IAsyncDocumentSession via DI
- Phone number storage exists but IUserPhoneNumberStore implementation is minimal