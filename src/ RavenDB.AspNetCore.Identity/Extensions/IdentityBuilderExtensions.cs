using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using RavenDB.AspNetCore.Identity.Infrastructure;
using RavenDB.AspNetCore.Identity.Models;
using RavenDB.AspNetCore.Identity.Stores;

namespace RavenDB.AspNetCore.Identity.Extensions;

/// <summary>
/// Extends <see cref="IdentityBuilder"/> so that RavenDB services can be registered through it.
/// </summary>
public static class IdentityBuilderExtensions
{
    /// <summary>
    /// Registers RavenDB user and role stores.
    /// Configure ID generation conventions directly on DocumentStore.Conventions before calling Initialize().
    /// </summary>
    /// <typeparam name="TUser">The type of the user.</typeparam>
    /// <typeparam name="TRole">The type of the role.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Options configuration callback for identity integration.</param>
    /// <returns>The builder.</returns>
    public static IdentityBuilder AddRavenDbIdentityStores<TUser, TRole>(
        this IdentityBuilder builder,
        Action<RavenDbIdentityOptions>? configure = null)
        where TUser : RavenIdentityUser, new()
        where TRole : RavenIdentityRole, new()
    {
        var options = new RavenDbIdentityOptions();
        configure?.Invoke(options);

        builder.Services.Configure<RavenDbIdentityOptions>(opts =>
        {
            opts.UseStaticIndexes = options.UseStaticIndexes;
            opts.AutoSaveChanges = options.AutoSaveChanges;
        });

        builder.Services.AddScoped<IUserStore<TUser>, RavenUserStore<TUser>>();
        builder.Services.AddScoped<IRoleStore<TRole>, RavenRoleStore<TRole>>();

        return builder;
    }

    /// <summary>
    /// Registers RavenDB user store only (without role store).
    /// Configure ID generation conventions directly on DocumentStore.Conventions before calling Initialize().
    /// </summary>
    /// <typeparam name="TUser">The type of the user.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Options configuration callback for identity integration.</param>
    /// <returns>The builder.</returns>
    public static IdentityBuilder AddRavenDbIdentityStores<TUser>(
        this IdentityBuilder builder,
        Action<RavenDbIdentityOptions>? configure = null)
        where TUser : RavenIdentityUser, new()
    {
        var options = new RavenDbIdentityOptions();
        configure?.Invoke(options);

        builder.Services.Configure<RavenDbIdentityOptions>(opts =>
        {
            opts.UseStaticIndexes = options.UseStaticIndexes;
            opts.AutoSaveChanges = options.AutoSaveChanges;
        });

        builder.Services.AddScoped<IUserStore<TUser>, RavenUserStore<TUser>>();

        return builder;
    }
}