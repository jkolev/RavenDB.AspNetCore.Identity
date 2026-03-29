using FluentAssertions;
using RavenDB.AspNetCore.Identity.Tests.Infrastructure;
using Xunit;

namespace RavenDB.AspNetCore.Identity.Tests.Stores;

/// <summary>
/// Tests for IUserSecurityStampStore operations in RavenUserStore.
/// </summary>
public class RavenUserStoreSecurityTests : RavenDbTestBase
{
    [Fact]
    public async Task SetSecurityStampAsync_SetsStamp()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser"
        };

        // Act
        await userStore.SetSecurityStampAsync(user, "security-stamp-123", CancellationToken.None);

        // Assert
        user.SecurityStamp.Should().Be("security-stamp-123");
    }

    [Fact]
    public async Task GetSecurityStampAsync_ReturnsStamp()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            SecurityStamp = "existing-stamp-456"
        };

        // Act
        var stamp = await userStore.GetSecurityStampAsync(user, CancellationToken.None);

        // Assert
        stamp.Should().Be("existing-stamp-456");
    }

    [Fact]
    public async Task GetSecurityStampAsync_WithNullStamp_ReturnsNull()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            SecurityStamp = null
        };

        // Act
        var stamp = await userStore.GetSecurityStampAsync(user, CancellationToken.None);

        // Assert
        stamp.Should().BeNull();
    }

    [Fact]
    public async Task SetSecurityStampAsync_WithDisposedStore_ThrowsObjectDisposedException()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);
        userStore.Dispose();

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser"
        };

        // Act & Assert
        var act = async () => await userStore.SetSecurityStampAsync(user, "stamp", CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task GetSecurityStampAsync_WithDisposedStore_ThrowsObjectDisposedException()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);
        userStore.Dispose();

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser"
        };

        // Act & Assert
        var act = async () => await userStore.GetSecurityStampAsync(user, CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
