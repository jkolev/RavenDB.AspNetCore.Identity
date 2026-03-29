using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Moq;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations.CompareExchange;
using Raven.Embedded;
using Raven.TestDriver;
using RavenDB.AspNetCore.Identity.Infrastructure;
using RavenDB.AspNetCore.Identity.Models;
using RavenDB.AspNetCore.Identity.Stores;

namespace RavenDB.AspNetCore.Identity.Tests.Infrastructure;

/// <summary>
/// Base class for RavenDB tests. Provides helpers for creating document stores, sessions, and user stores.
/// </summary>
public abstract class RavenDbTestBase : RavenTestDriver
{
    static RavenDbTestBase()
    {
        ConfigureServer(new TestServerOptions
        {
            Licensing = new ServerOptions.LicensingOptions
            {
                ThrowOnInvalidOrMissingLicense = false
            }
        });
    }
    /// <summary>
    /// Creates a test document store with a unique database name per test method.
    /// Uses [CallerMemberName] to ensure test isolation.
    /// </summary>
    protected IDocumentStore GetTestDocumentStore([CallerMemberName] string? database = null)
    {
        return base.GetDocumentStore(database: database);
    }

    /// <summary>
    /// Creates a RavenUserStore for testing with the given session and optional logger.
    /// </summary>
    protected RavenUserStore<TUser> CreateUserStore<TUser>(
        Raven.Client.Documents.Session.IAsyncDocumentSession session,
        ILogger<RavenUserStore<TUser>>? logger = null)
        where TUser : RavenIdentityUser, new()
    {
        logger ??= new Mock<ILogger<RavenUserStore<TUser>>>().Object;
        return new RavenUserStore<TUser>(session, logger);
    }

    /// <summary>
    /// Gets the email reservation value from compare/exchange.
    /// </summary>
    /// <param name="store">The document store.</param>
    /// <param name="normalizedEmail">The normalized email address.</param>
    /// <returns>The user ID associated with the email, or null if not reserved.</returns>
    protected async Task<string?> GetEmailReservationAsync(IDocumentStore store, string normalizedEmail)
    {
        var key = Conventions.CompareExchangeKeyFor(normalizedEmail);
        var result = await store.Operations.SendAsync(
            new GetCompareExchangeValueOperation<string>(key));
        return result?.Value;
    }

    /// <summary>
    /// Deletes an email reservation from compare/exchange.
    /// Useful for cleanup between tests if needed.
    /// </summary>
    protected async Task DeleteEmailReservationAsync(IDocumentStore store, string normalizedEmail)
    {
        var key = Conventions.CompareExchangeKeyFor(normalizedEmail);
        var existing = await store.Operations.SendAsync(
            new GetCompareExchangeValueOperation<string>(key));
        if (existing != null)
        {
            await store.Operations.SendAsync(
                new DeleteCompareExchangeValueOperation<string>(key, existing.Index));
        }
    }

    /// <summary>
    /// Concrete test user class for testing.
    /// RavenIdentityUser is abstract, so we need a concrete implementation.
    /// </summary>
    public class TestUser : RavenIdentityUser
    {
        public TestUser() { }
    }
}