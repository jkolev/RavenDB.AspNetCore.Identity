using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.Identity.Stores;

namespace RavenDB.AspNetCore.Identity.Infrastructure;

/// <summary>
/// Options for initializing RavenDB.Identity.
/// </summary>
public class RavenDbIdentityOptions
{
    /// <summary>
    /// Whether to use static indexes, defaults to false.
    /// </summary>
    /// <remarks>
    /// Indexes need to be deployed to server in order for static index queries to work.
    /// </remarks>
    public bool UseStaticIndexes { get; set; }

    /// <summary>
    ///   If set, changes detected in <see cref="RavenUserStore{TUser}" /> and <see cref="RavenRoleStore{TRole}" />
    ///   will be saved to Raven immediately (by calling <see cref="IAsyncDocumentSession.SaveChangesAsync"/>).
    ///   Leave false (the default) if you've implemented the save changes call in middleware.
    /// </summary>
    public bool AutoSaveChanges { get; set; }
}