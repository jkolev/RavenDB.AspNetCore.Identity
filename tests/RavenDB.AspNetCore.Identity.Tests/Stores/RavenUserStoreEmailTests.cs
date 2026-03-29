using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using RavenDB.AspNetCore.Identity.Tests.Infrastructure;
using Xunit;

namespace RavenDB.AspNetCore.Identity.Tests.Stores;

/// <summary>
/// Tests for email uniqueness and compare/exchange operations in RavenUserStore.
/// </summary>
public class RavenUserStoreEmailTests : RavenDbTestBase
{
    [Fact]
    public async Task CreateAsync_WithUniqueEmail_CreatesReservation()
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
        var result = await userStore.CreateAsync(user, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();
        user.Id.Should().NotBeNullOrWhiteSpace();

        // Verify email reservation exists and points to the user
        var reservation = await GetEmailReservationAsync(store, "test@example.com");
        reservation.Should().Be(user.Id);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateEmail_ReturnsFailed()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session1 = store.OpenAsyncSession();
        var userStore1 = CreateUserStore<TestUser>(session1);

        var user1 = new TestUser
        {
            Email = "duplicate@example.com",
            UserName = "user1"
        };
        await userStore1.CreateAsync(user1, CancellationToken.None);

        // Act - try to create second user with same email
        using var session2 = store.OpenAsyncSession();
        var userStore2 = CreateUserStore<TestUser>(session2);
        var user2 = new TestUser
        {
            Email = "duplicate@example.com",
            UserName = "user2"
        };
        var result = await userStore2.CreateAsync(user2, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "DuplicateEmail");

        // Verify original reservation is still intact
        var reservation = await GetEmailReservationAsync(store, "duplicate@example.com");
        reservation.Should().Be(user1.Id);
    }

    [Fact]
    public async Task CreateAsync_ConcurrentDuplicateEmails_OnlyOneSucceeds()
    {
        // Arrange
        using var store = GetTestDocumentStore();

        var user1 = new TestUser
        {
            Email = "concurrent@example.com",
            UserName = "user1"
        };

        var user2 = new TestUser
        {
            Email = "concurrent@example.com",
            UserName = "user2"
        };

        // Act - create both users concurrently
        var task1 = Task.Run(async () =>
        {
            using var session = store.OpenAsyncSession();
            var userStore = CreateUserStore<TestUser>(session);
            return await userStore.CreateAsync(user1, CancellationToken.None);
        });

        var task2 = Task.Run(async () =>
        {
            using var session = store.OpenAsyncSession();
            var userStore = CreateUserStore<TestUser>(session);
            return await userStore.CreateAsync(user2, CancellationToken.None);
        });

        var results = await Task.WhenAll(task1, task2);

        // Assert - exactly one should succeed
        var successCount = results.Count(r => r.Succeeded);
        successCount.Should().Be(1, "compare/exchange should prevent duplicate email reservations");

        // Verify only one reservation exists
        var reservation = await GetEmailReservationAsync(store, "concurrent@example.com");
        reservation.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateAsync_WithEmailChange_MigratesReservation()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "old@example.com",
            UserName = "testuser"
        };
        await userStore.CreateAsync(user, CancellationToken.None);

        // Act - change email
        user.Email = "new@example.com";
        var result = await userStore.UpdateAsync(user, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();

        // Verify new email reservation exists
        var newReservation = await GetEmailReservationAsync(store, "new@example.com");
        newReservation.Should().Be(user.Id);

        // Verify old email reservation is removed
        var oldReservation = await GetEmailReservationAsync(store, "old@example.com");
        oldReservation.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithCaseOnlyChange_DoesNotMigrateReservation()
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
        var userId = user.Id;

        // Act - change only the case
        user.Email = "TEST@EXAMPLE.COM";
        var result = await userStore.UpdateAsync(user, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();

        // Verify reservation is unchanged (normalized email is the same)
        var reservation = await GetEmailReservationAsync(store, "test@example.com");
        reservation.Should().Be(userId);
    }

    [Fact]
    public async Task UpdateAsync_WithDuplicateEmail_ReturnsFailedAndKeepsOldEmail()
    {
        // Arrange
        using var store = GetTestDocumentStore();

        // Create first user
        using var session1 = store.OpenAsyncSession();
        var userStore1 = CreateUserStore<TestUser>(session1);
        var user1 = new TestUser
        {
            Email = "existing@example.com",
            UserName = "user1"
        };
        await userStore1.CreateAsync(user1, CancellationToken.None);

        // Create second user
        using var session2 = store.OpenAsyncSession();
        var userStore2 = CreateUserStore<TestUser>(session2);
        var user2 = new TestUser
        {
            Email = "original@example.com",
            UserName = "user2"
        };
        await userStore2.CreateAsync(user2, CancellationToken.None);
        var user2Id = user2.Id;

        // Act - try to change user2's email to user1's email
        using var session3 = store.OpenAsyncSession();
        var userStore3 = CreateUserStore<TestUser>(session3);
        var user2Loaded = await session3.LoadAsync<TestUser>(user2Id);
        user2Loaded!.Email = "existing@example.com";
        var result = await userStore3.UpdateAsync(user2Loaded, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "DuplicateEmail");

        // Verify user2's original email reservation is still intact
        var user2Reservation = await GetEmailReservationAsync(store, "original@example.com");
        user2Reservation.Should().Be(user2Id);

        // Verify user1's email reservation is still intact
        var user1Reservation = await GetEmailReservationAsync(store, "existing@example.com");
        user1Reservation.Should().Be(user1.Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEmailReservation()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "todelete@example.com",
            UserName = "testuser"
        };
        await userStore.CreateAsync(user, CancellationToken.None);

        // Act
        var result = await userStore.DeleteAsync(user, CancellationToken.None);

        // Assert
        result.Succeeded.Should().BeTrue();

        // Verify email reservation is removed
        var reservation = await GetEmailReservationAsync(store, "todelete@example.com");
        reservation.Should().BeNull();
    }

    [Fact]
    public async Task FindByEmailAsync_BypassesIndexes_UsesCompareExchange()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "findme@example.com",
            UserName = "testuser"
        };
        await userStore.CreateAsync(user, CancellationToken.None);

        // Act - find by email without waiting for indexes
        using var session2 = store.OpenAsyncSession();
        var userStore2 = CreateUserStore<TestUser>(session2);
        var foundUser = await userStore2.FindByEmailAsync("findme@example.com", CancellationToken.None);

        // Assert
        foundUser.Should().NotBeNull();
        foundUser!.Id.Should().Be(user.Id);
        foundUser.Email.Should().Be("findme@example.com");
    }

    [Fact]
    public async Task FindByEmailAsync_WithNonexistentEmail_ReturnsNull()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        // Act
        var foundUser = await userStore.FindByEmailAsync("nonexistent@example.com", CancellationToken.None);

        // Assert
        foundUser.Should().BeNull();
    }

    [Fact]
    public async Task SetEmailConfirmedAsync_SetsFlag()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            EmailConfirmed = false
        };

        // Act
        await userStore.SetEmailConfirmedAsync(user, true, CancellationToken.None);

        // Assert
        user.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task GetEmailConfirmedAsync_ReturnsFlag()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        using var session = store.OpenAsyncSession();
        var userStore = CreateUserStore<TestUser>(session);

        var user = new TestUser
        {
            Email = "test@example.com",
            UserName = "testuser",
            EmailConfirmed = true
        };

        // Act
        var confirmed = await userStore.GetEmailConfirmedAsync(user, CancellationToken.None);

        // Assert
        confirmed.Should().BeTrue();
    }
}
