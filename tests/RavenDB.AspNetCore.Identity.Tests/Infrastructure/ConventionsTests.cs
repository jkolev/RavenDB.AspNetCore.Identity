using FluentAssertions;
using RavenDB.AspNetCore.Identity.Infrastructure;
using RavenDB.AspNetCore.Identity.Tests.Infrastructure;
using Xunit;

namespace RavenDB.AspNetCore.Identity.Tests.Infrastructure;

/// <summary>
/// Tests for the Conventions helper class.
/// </summary>
public class ConventionsTests : RavenDbTestBase
{
    [Fact]
    public void CompareExchangeKeyFor_WithEmail_ReturnsCorrectKey()
    {
        // Arrange
        const string email = "test@example.com";

        // Act
        var key = Conventions.CompareExchangeKeyFor(email);

        // Assert
        key.Should().Be("emails/test@example.com");
    }

    [Fact]
    public void CompareExchangeKeyFor_WithUppercaseEmail_NormalizesToLowercase()
    {
        // Arrange
        const string email = "TEST@EXAMPLE.COM";

        // Act
        var key = Conventions.CompareExchangeKeyFor(email);

        // Assert
        key.Should().Be("emails/test@example.com");
    }

    [Fact]
    public void CompareExchangeKeyFor_WithMixedCaseEmail_NormalizesToLowercase()
    {
        // Arrange
        const string email = "TeSt@ExAmPlE.CoM";

        // Act
        var key = Conventions.CompareExchangeKeyFor(email);

        // Assert
        key.Should().Be("emails/test@example.com");
    }

    [Fact]
    public void EmailReservationKeyPrefix_HasCorrectValue()
    {
        // Assert
        Conventions.EmailReservationKeyPrefix.Should().Be("emails/");
    }

    [Fact]
    public void CollectionNameFor_WithTestUser_ReturnsCollectionName()
    {
        // Arrange
        using var store = GetTestDocumentStore();

        // Act
        var collectionName = Conventions.CollectionNameFor<TestUser>(store);

        // Assert
        collectionName.Should().NotBeNullOrWhiteSpace();
        collectionName.Should().Contain("TestUsers");
    }

    [Fact]
    public void CollectionNameWithSeparator_WithTestUser_IncludesSeparator()
    {
        // Arrange
        using var store = GetTestDocumentStore();

        // Act
        var collectionNameWithSeparator = Conventions.CollectionNameWithSeparator<TestUser>(store);

        // Assert
        collectionNameWithSeparator.Should().NotBeNullOrWhiteSpace();
        collectionNameWithSeparator.Should().EndWith(store.Conventions.IdentityPartsSeparator.ToString());
    }

    [Fact]
    public void CollectionNameWithSeparator_EndsWithSlash_ByDefault()
    {
        // Arrange
        using var store = GetTestDocumentStore();

        // Act
        var collectionNameWithSeparator = Conventions.CollectionNameWithSeparator<TestUser>(store);

        // Assert
        // Default separator is "/"
        collectionNameWithSeparator.Should().EndWith("/");
    }

    [Fact]
    public void RoleIdFor_WithRoleName_ReturnsCorrectId()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        const string roleName = "Admin";

        // Act
        var roleId = Conventions.RoleIdFor<Microsoft.AspNetCore.Identity.IdentityRole>(roleName, store);

        // Assert
        roleId.Should().NotBeNullOrWhiteSpace();
        roleId.Should().Contain("admin"); // Normalized to lowercase
        roleId.Should().Contain("/");
    }

    [Fact]
    public void RoleIdFor_WithUppercaseRoleName_NormalizesToLowercase()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        const string roleName = "SUPERADMIN";

        // Act
        var roleId = Conventions.RoleIdFor<Microsoft.AspNetCore.Identity.IdentityRole>(roleName, store);

        // Assert
        roleId.Should().Contain("superadmin");
    }

    [Fact]
    public void RoleIdFor_WithMixedCaseRoleName_NormalizesToLowercase()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        const string roleName = "PowerUser";

        // Act
        var roleId = Conventions.RoleIdFor<Microsoft.AspNetCore.Identity.IdentityRole>(roleName, store);

        // Assert
        roleId.Should().Contain("poweruser");
    }

    [Fact]
    public void RoleIdFor_IncludesCollectionName()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        const string roleName = "Moderator";

        // Act
        var roleId = Conventions.RoleIdFor<Microsoft.AspNetCore.Identity.IdentityRole>(roleName, store);
        var collectionNameWithSeparator = Conventions.CollectionNameWithSeparator<Microsoft.AspNetCore.Identity.IdentityRole>(store);

        // Assert
        roleId.Should().StartWith(collectionNameWithSeparator);
    }

    [Fact]
    public void RoleIdFor_WithEmptyString_ReturnsCollectionNameOnly()
    {
        // Arrange
        using var store = GetTestDocumentStore();
        const string roleName = "";

        // Act
        var roleId = Conventions.RoleIdFor<Microsoft.AspNetCore.Identity.IdentityRole>(roleName, store);
        var collectionNameWithSeparator = Conventions.CollectionNameWithSeparator<Microsoft.AspNetCore.Identity.IdentityRole>(store);

        // Assert
        roleId.Should().Be(collectionNameWithSeparator);
    }

    [Fact]
    public void CompareExchangeKeyFor_WithSpecialCharacters_IncludesThemInKey()
    {
        // Arrange
        const string email = "test+alias@example.com";

        // Act
        var key = Conventions.CompareExchangeKeyFor(email);

        // Assert
        key.Should().Be("emails/test+alias@example.com");
    }

    [Fact]
    public void CompareExchangeKeyFor_WithDots_IncludesThemInKey()
    {
        // Arrange
        const string email = "first.last@example.com";

        // Act
        var key = Conventions.CompareExchangeKeyFor(email);

        // Assert
        key.Should().Be("emails/first.last@example.com");
    }

    [Fact]
    public void CompareExchangeKeyFor_WithUnderscore_IncludesItInKey()
    {
        // Arrange
        const string email = "test_user@example.com";

        // Act
        var key = Conventions.CompareExchangeKeyFor(email);

        // Assert
        key.Should().Be("emails/test_user@example.com");
    }
}
