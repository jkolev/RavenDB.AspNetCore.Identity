using FluentAssertions;
using RavenDB.AspNetCore.Identity.Tests.Infrastructure;
using Xunit;

namespace RavenDB.AspNetCore.Identity.Tests.Stores;

/// <summary>
/// Tests for IUserPasswordStore operations in RavenUserStore.
/// </summary>
public class RavenUserStorePasswordTests : RavenDbTestBase
{
    [Fact]
    public async Task SetPasswordHashAsync_SetsHash()
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
        await userStore.SetPasswordHashAsync(user, "hashedpassword123", CancellationToken.None);

        // Assert
        user.PasswordHash.Should().Be("hashedpassword123");
    }

    [Fact]
    public async Task GetPasswordHashAsync_ReturnsHash()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            PasswordHash = "existinghash456"
        };

        // Act
        var hash = await userStore.GetPasswordHashAsync(user, CancellationToken.None);

        // Assert
        hash.Should().Be("existinghash456");
    }

    [Fact]
    public async Task HasPasswordAsync_WithHash_ReturnsTrue()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            PasswordHash = "somehash"
        };

        // Act
        var hasPassword = await userStore.HasPasswordAsync(user, CancellationToken.None);

        // Assert
        hasPassword.Should().BeTrue();
    }

    [Fact]
    public async Task HasPasswordAsync_WithoutHash_ReturnsFalse()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            PasswordHash = null
        };

        // Act
        var hasPassword = await userStore.HasPasswordAsync(user, CancellationToken.None);

        // Assert
        hasPassword.Should().BeFalse();
    }

    [Fact]
    public async Task SetPasswordHashAsync_WithDisposedStore_ThrowsObjectDisposedException()
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
        var act = async () => await userStore.SetPasswordHashAsync(user, "hash", CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task GetPasswordHashAsync_WithDisposedStore_ThrowsObjectDisposedException()
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
        var act = async () => await userStore.GetPasswordHashAsync(user, CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task HasPasswordAsync_WithDisposedStore_ThrowsObjectDisposedException()
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
        var act = async () => await userStore.HasPasswordAsync(user, CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
