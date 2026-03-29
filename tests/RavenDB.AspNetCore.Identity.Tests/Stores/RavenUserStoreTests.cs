using FluentAssertions;
using RavenDB.AspNetCore.Identity.Tests.Infrastructure;
using Xunit;

namespace RavenDB.AspNetCore.Identity.Tests.Stores;

/// <summary>
/// Tests for core CRUD operations in RavenUserStore.
/// </summary>
public class RavenUserStoreTests : RavenDbTestBase
{
    [Fact]
    public async Task CreateAsync_WithValidUser_Succeeds()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "valid@example.com",
            UserName = "validuser"
        };

        // Act
        var result = await userStore.CreateAsync(user, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        user.Id.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateAsync_WithNullEmail_ThrowsArgumentException()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = null!,
            UserName = "testuser"
        };

        // Act & Assert
        var act = async () => await userStore.CreateAsync(user, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*email*");
    }

    [Fact]
    public async Task CreateAsync_WithEmptyEmail_ThrowsArgumentException()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = string.Empty,
            UserName = "testuser"
        };

        // Act & Assert
        var act = async () => await userStore.CreateAsync(user, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*email*");
    }

    [Fact]
    public async Task UpdateAsync_WithoutChanges_LogsWarningAndSucceeds()
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
        await userStore.CreateAsync(user, CancellationToken.None);

        // Act - call update without making any changes
        var result = await userStore.UpdateAsync(user, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_WithPropertyChanges_SavesChanges()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session1 = store.OpenAsyncSession();
        var userStore1 = CreateUserStore<TestUser>(session1);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser"
        };
        await userStore1.CreateAsync(user, CancellationToken.None);
        var userId = user.Id;

        // Act - load and update user
        using var session2 = store.OpenAsyncSession();
        var userStore2 = CreateUserStore<TestUser>(session2);
        var loadedUser = await session2.LoadAsync<TestUser>(userId);
        loadedUser!.PhoneNumber = "555-1234";
        var result = await userStore2.UpdateAsync(loadedUser, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();

        // Verify changes were saved
        using var session3 = store.OpenAsyncSession();
        var verifiedUser = await session3.LoadAsync<TestUser>(userId);
        verifiedUser!.PhoneNumber.Should().Be("555-1234");
    }

    [Fact]
    public async Task DeleteAsync_WithValidUser_Succeeds()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "delete@example.com",
            UserName = "deleteuser"
        };
        await userStore.CreateAsync(user, CancellationToken.None);
        var userId = user.Id;

        // Act
        var result = await userStore.DeleteAsync(user, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();

        // Verify user is deleted
        using var session2 = store.OpenAsyncSession();
        var deletedUser = await session2.LoadAsync<TestUser>(userId);
        deletedUser.Should().BeNull();
    }

    [Fact]
    public async Task FindByIdAsync_WithExistingUser_ReturnsUser()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "find@example.com",
            UserName = "finduser"
        };
        await userStore.CreateAsync(user, CancellationToken.None);
        var userId = user.Id;

        // Act
        using var session2 = store.OpenAsyncSession();
        var userStore2 = CreateUserStore<TestUser>(session2);
        var foundUser = await userStore2.FindByIdAsync(userId!, CancellationToken.None);

        // Assert
        foundUser.Should().NotBeNull();
        foundUser!.Id.Should().Be(userId);
        foundUser.Email.Should().Be("find@example.com");
    }

    [Fact]
    public async Task FindByIdAsync_WithNonexistentId_ReturnsNull()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        // Act
        var foundUser = await userStore.FindByIdAsync("TestUsers/999-A", CancellationToken.None);

        // Assert
        foundUser.Should().BeNull();
    }

    [Fact]
    public async Task FindByNameAsync_WithExistingUser_ReturnsUser()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "findbyname@example.com",
            UserName = "uniqueusername"
        };
        await userStore.CreateAsync(user, CancellationToken.None);

        // Wait for index to update
        await Task.Delay(100);

        // Act
        using var session2 = store.OpenAsyncSession();
        var userStore2 = CreateUserStore<TestUser>(session2);
        var foundUser = await userStore2.FindByNameAsync("uniqueusername", CancellationToken.None);

        // Assert
        foundUser.Should().NotBeNull();
        foundUser!.UserName.Should().Be("uniqueusername");
    }

    [Fact]
    public async Task FindByNameAsync_WithNonexistentName_ReturnsNull()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        // Act
        var foundUser = await userStore.FindByNameAsync("nonexistentuser", CancellationToken.None);

        // Assert
        foundUser.Should().BeNull();
    }

    [Fact]
    public async Task GetUserIdAsync_WithNullId_ThrowsInvalidOperationException()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Id = null,
            Email = "test@example.com"
        };

        // Act & Assert
        var act = async () => await userStore.GetUserIdAsync(user, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*User ID*");
    }

    [Fact]
    public async Task SetNormalizedUserNameAsync_NormalizesToLowercase()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "TestUser"
        };

        // Act
        await userStore.SetNormalizedUserNameAsync(user, "TESTUSER", CancellationToken.None);

        // Assert
        user.UserName.Should().Be("testuser");
    }

    [Fact]
    public async Task GetNormalizedUserNameAsync_ReturnsUserName()
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
        var normalizedUserName = await userStore.GetNormalizedUserNameAsync(user, CancellationToken.None);

        // Assert
        normalizedUserName.Should().Be("testuser");
    }

    [Fact]
    public async Task Dispose_SetsDisposedFlag()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        // Act
        userStore.Dispose();

        // Assert - next operation should throw
        var user = new TestUser { Email = "test@example.com" };
        var act = async () => await userStore.CreateAsync(user, CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task CreateAsync_AfterDispose_ThrowsObjectDisposedException()
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
        var act = async () => await userStore.CreateAsync(user, CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
