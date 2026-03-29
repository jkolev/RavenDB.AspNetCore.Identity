using FluentAssertions;
using RavenDB.AspNetCore.Identity.Tests.Infrastructure;
using Xunit;

namespace RavenDB.AspNetCore.Identity.Tests.Stores;

/// <summary>
/// Tests for IUserLockoutStore operations in RavenUserStore.
/// </summary>
public class RavenUserStoreLockoutTests : RavenDbTestBase
{
    [Fact]
    public async Task SetLockoutEndDateAsync_SetsDate()
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
        var lockoutEnd = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        await userStore.SetLockoutEndDateAsync(user, lockoutEnd, CancellationToken.None);

        // Assert
        user.LockoutEnd.Should().Be(lockoutEnd);
    }

    [Fact]
    public async Task GetLockoutEndDateAsync_ReturnsDate()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var lockoutEnd = DateTimeOffset.UtcNow.AddHours(2);
        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            LockoutEnd = lockoutEnd
        };

        // Act
        var result = await userStore.GetLockoutEndDateAsync(user, CancellationToken.None);

        // Assert
        result.Should().Be(lockoutEnd);
    }

    [Fact]
    public async Task IncrementAccessFailedCountAsync_IncrementsCounter()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            AccessFailedCount = 0
        };

        // Act
        var count = await userStore.IncrementAccessFailedCountAsync(user, CancellationToken.None);

        // Assert
        count.Should().Be(1);
        user.AccessFailedCount.Should().Be(1);

        // Act again
        count = await userStore.IncrementAccessFailedCountAsync(user, CancellationToken.None);

        // Assert
        count.Should().Be(2);
        user.AccessFailedCount.Should().Be(2);
    }

    [Fact]
    public async Task ResetAccessFailedCountAsync_ResetsToZero()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            AccessFailedCount = 5
        };

        // Act
        await userStore.ResetAccessFailedCountAsync(user, CancellationToken.None);

        // Assert
        user.AccessFailedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAccessFailedCountAsync_ReturnsCount()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            AccessFailedCount = 3
        };

        // Act
        var count = await userStore.GetAccessFailedCountAsync(user, CancellationToken.None);

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task SetLockoutEnabledAsync_SetsFlag()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            LockoutEnabled = false
        };

        // Act
        await userStore.SetLockoutEnabledAsync(user, true, CancellationToken.None);

        // Assert
        user.LockoutEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetLockoutEnabledAsync_ReturnsFlag()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            LockoutEnabled = true
        };

        // Act
        var enabled = await userStore.GetLockoutEnabledAsync(user, CancellationToken.None);

        // Assert
        enabled.Should().BeTrue();
    }

    [Fact]
    public async Task SetLockoutEndDateAsync_WithDisposedStore_ThrowsObjectDisposedException()
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
        var act = async () => await userStore.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow, CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
