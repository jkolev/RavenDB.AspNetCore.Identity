using FluentAssertions;
using RavenDB.AspNetCore.Identity.Tests.Infrastructure;
using Xunit;

namespace RavenDB.AspNetCore.Identity.Tests.Stores;

/// <summary>
/// Tests for IUserPhoneNumberStore operations in RavenUserStore.
/// </summary>
public class RavenUserStorePhoneTests : RavenDbTestBase
{
    [Fact]
    public async Task SetPhoneNumberAsync_SetsNumber()
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
        await userStore.SetPhoneNumberAsync(user, "555-1234", CancellationToken.None);

        // Assert
        user.PhoneNumber.Should().Be("555-1234");
    }

    [Fact]
    public async Task GetPhoneNumberAsync_ReturnsNumber()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            PhoneNumber = "555-5678"
        };

        // Act
        var phoneNumber = await userStore.GetPhoneNumberAsync(user, CancellationToken.None);

        // Assert
        phoneNumber.Should().Be("555-5678");
    }

    [Fact]
    public async Task GetPhoneNumberAsync_WithNullPhoneNumber_ReturnsNull()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            PhoneNumber = null
        };

        // Act
        var phoneNumber = await userStore.GetPhoneNumberAsync(user, CancellationToken.None);

        // Assert
        phoneNumber.Should().BeNull();
    }

    [Fact]
    public async Task SetPhoneNumberConfirmedAsync_SetsFlag()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            PhoneNumberConfirmed = false
        };

        // Act
        await userStore.SetPhoneNumberConfirmedAsync(user, true, CancellationToken.None);

        // Assert
        user.PhoneNumberConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task GetPhoneNumberConfirmedAsync_ReturnsFlag()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            PhoneNumberConfirmed = true
        };

        // Act
        var confirmed = await userStore.GetPhoneNumberConfirmedAsync(user, CancellationToken.None);

        // Assert
        confirmed.Should().BeTrue();
    }

    [Fact]
    public async Task SetPhoneNumberAsync_WithDisposedStore_ThrowsObjectDisposedException()
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
        var act = async () => await userStore.SetPhoneNumberAsync(user, "555-1234", CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task SetPhoneNumberConfirmedAsync_WithDisposedStore_ThrowsObjectDisposedException()
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
        var act = async () => await userStore.SetPhoneNumberConfirmedAsync(user, true, CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
