using FluentAssertions;
using RavenDB.AspNetCore.Identity.Tests.Infrastructure;
using Xunit;

namespace RavenDB.AspNetCore.Identity.Tests.Models;

/// <summary>
/// Tests for email normalization in RavenIdentityUser.
/// </summary>
public class RavenIdentityUserTests : RavenDbTestBase
{
    [Fact]
    public void EmailSetter_NormalizesToLowercase()
    {
        // Arrange
        var user = new TestUser();

        // Act
        user.Email = "Test@Example.COM";

        // Assert
        user.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void EmailSetter_WithMixedCase_StoresLowercase()
    {
        // Arrange
        var user = new TestUser();

        // Act
        user.Email = "TeSt@ExAmPlE.CoM";

        // Assert
        user.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void EmailSetter_WithWhitespace_TrimsAndNormalizes()
    {
        // Arrange
        var user = new TestUser();

        // Act
        user.Email = "  Test@Example.COM  ";

        // Assert
        user.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void EmailSetter_WithNullValue_SetsEmptyString()
    {
        // Arrange
        var user = new TestUser
        {
            Email = "test@example.com"
        };

        // Act
        user.Email = null!;

        // Assert
        user.Email.Should().Be(string.Empty);
    }

    [Fact]
    public void EmailSetter_WithEmptyString_SetsEmptyString()
    {
        // Arrange
        var user = new TestUser();

        // Act
        user.Email = string.Empty;

        // Assert
        user.Email.Should().Be(string.Empty);
    }

    [Fact]
    public void EmailSetter_WithWhitespaceOnly_SetsEmptyString()
    {
        // Arrange
        var user = new TestUser();

        // Act
        user.Email = "   ";

        // Assert
        user.Email.Should().Be(string.Empty);
    }

    [Fact]
    public void EmailSetter_MultipleTimes_AlwaysNormalizes()
    {
        // Arrange
        var user = new TestUser();

        // Act & Assert
        user.Email = "First@Example.COM";
        user.Email.Should().Be("first@example.com");

        user.Email = "SECOND@EXAMPLE.COM";
        user.Email.Should().Be("second@example.com");

        user.Email = "ThIrD@ExAmPlE.CoM";
        user.Email.Should().Be("third@example.com");
    }

    [Fact]
    public void Constructor_DefaultValues_AreSet()
    {
        // Act
        var user = new TestUser();

        // Assert
        user.Id.Should().BeNull();
        user.UserName.Should().Be(string.Empty);
        user.Email.Should().Be(string.Empty);
        user.PasswordHash.Should().BeNull();
        user.SecurityStamp.Should().BeNull();
        user.PhoneNumber.Should().BeNull();
        user.EmailConfirmed.Should().BeFalse();
        user.PhoneNumberConfirmed.Should().BeFalse();
        user.AccessFailedCount.Should().Be(0);
        user.LockoutEnabled.Should().BeFalse();
        user.LockoutEnd.Should().BeNull();
        user.TwoFactorEnabled.Should().BeFalse();
        user.Logins.Should().NotBeNull().And.BeEmpty();
        user.Roles.Should().NotBeNull().And.BeEmpty();
        user.Claims.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        // Arrange
        var user = new TestUser();
        var lockoutEnd = DateTimeOffset.UtcNow.AddHours(1);

        // Act
        user.UserName = "testuser";
        user.PasswordHash = "hash123";
        user.SecurityStamp = "stamp456";
        user.PhoneNumber = "555-1234";
        user.EmailConfirmed = true;
        user.PhoneNumberConfirmed = true;
        user.AccessFailedCount = 5;
        user.LockoutEnabled = true;
        user.LockoutEnd = lockoutEnd;
        user.TwoFactorEnabled = true;

        // Assert
        user.UserName.Should().Be("testuser");
        user.PasswordHash.Should().Be("hash123");
        user.SecurityStamp.Should().Be("stamp456");
        user.PhoneNumber.Should().Be("555-1234");
        user.EmailConfirmed.Should().BeTrue();
        user.PhoneNumberConfirmed.Should().BeTrue();
        user.AccessFailedCount.Should().Be(5);
        user.LockoutEnabled.Should().BeTrue();
        user.LockoutEnd.Should().Be(lockoutEnd);
        user.TwoFactorEnabled.Should().BeTrue();
    }
}
