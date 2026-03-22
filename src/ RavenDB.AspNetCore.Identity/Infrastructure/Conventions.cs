using Raven.Client.Documents;
using IdentityRole = Microsoft.AspNetCore.Identity.IdentityRole;

namespace RavenDB.AspNetCore.Identity.Infrastructure;

public static class Conventions
{
    /// <summary>
    /// The prefix used for compare/exchange values used by RavenDB.Identity to ensure user uniqueness based on email address.
    /// </summary>
    public const string EmailReservationKeyPrefix = "emails/";

    /// <summary>
    /// Gets the compare/exchange key used to store the specified email address.
    /// </summary>
    /// <param name="email">The email address to generate a key for.</param>
    /// <returns>The compare/exchange key in the format "emails/{email}".</returns>
    public static string CompareExchangeKeyFor(string email)
    {
        return EmailReservationKeyPrefix + email.ToLowerInvariant();
    }

    /// <summary>
    /// Gets the collection name of the specified type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="db">The RavenDB document store.</param>
    /// <returns>The collection name, e.g. "AppUsers".</returns>
    public static string CollectionNameFor<T>(IDocumentStore db)
    {
        var entityName = db.Conventions.GetCollectionName(typeof(T));
        return db.Conventions.TransformTypeCollectionNameToDocumentIdPrefix(entityName);
    }

    /// <summary>
    /// Gets the collection name with separator for the specified type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="db">The RavenDB document store.</param>
    /// <returns>The collection name with separator, e.g. "AppUsers/".</returns>
    public static string CollectionNameWithSeparator<T>(IDocumentStore db)
    {
        return CollectionNameFor<T>(db) + db.Conventions.IdentityPartsSeparator;
    }

    /// <summary>
    /// Creates the ID for the role with the specified name.
    /// </summary>
    /// <typeparam name="TRole">The type of role.</typeparam>
    /// <param name="roleName">The name of the role.</param>
    /// <param name="db">The Raven database. Used for finding the collection name for <typeparamref name="TRole"/>s and the identity parts separator.</param>
    /// <returns>An ID for the role with the specified name.</returns>
    public static string RoleIdFor<TRole>(string roleName, IDocumentStore db)
        where TRole : IdentityRole
    {
        var roleNameLowered = roleName.ToLowerInvariant();
        return CollectionNameWithSeparator<TRole>(db) + roleNameLowered;
    }
}