using FluentAssertions;
using RavenDB.AspNetCore.Identity.Tests.Infrastructure;
using Xunit;

namespace RavenDB.AspNetCore.Identity.Tests.Stores;

/// <summary>
/// Tests for IUserRoleStore operations in RavenUserStore.
/// Note: These methods are currently stubs that return empty values or Task.CompletedTask.
/// Role management will be fully implemented once RavenRoleStore is completed.
/// </summary>
public class RavenUserStoreRoleTests : RavenDbTestBase
{
    [Fact]
    public async Task AddToRoleAsync_CompletesSuccessfully()
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
        await userStore.AddToRoleAsync(user, "Admin", CancellationToken.None);

        // Assert - method should complete without throwing
        // TODO: Once role management is implemented, verify the role was added
    }

    [Fact]
    public async Task RemoveFromRoleAsync_CompletesSuccessfully()
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
        await userStore.RemoveFromRoleAsync(user, "Admin", CancellationToken.None);

        // Assert - method should complete without throwing
        // TODO: Once role management is implemented, verify the role was removed
    }

    [Fact]
    public async Task GetRolesAsync_ReturnsEmptyList()
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
        var roles = await userStore.GetRolesAsync(user, CancellationToken.None);

        // Assert
        roles.Should().NotBeNull();
        roles.Should().BeEmpty();
    }

    [Fact]
    public async Task IsInRoleAsync_ReturnsFalse()
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
        var isInRole = await userStore.IsInRoleAsync(user, "Admin", CancellationToken.None);

        // Assert
        isInRole.Should().BeFalse();
    }

    [Fact]
    public async Task GetUsersInRoleAsync_ReturnsEmptyList()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        // Act
        var users = await userStore.GetUsersInRoleAsync("Admin", CancellationToken.None);

        // Assert
        users.Should().NotBeNull();
        users.Should().BeEmpty();
    }

}
