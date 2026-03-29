using FluentAssertions;
using RavenDB.AspNetCore.Identity.ValueObjects;
using Xunit;

namespace RavenDB.AspNetCore.Identity.Tests.ValueObjects;

/// <summary>
/// Tests for the NormalizedEmail value object.
/// </summary>
public class NormalizedEmailTests
{
    [Fact]
    public void Constructor_WithValidEmail_NormalizesToLowercase()
    {
        // Arrange & Act
        var email = new NormalizedEmail("Test@Example.COM");

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void Constructor_WithWhitespace_TrimsAndNormalizes()
    {
        // Arrange & Act
        var email = new NormalizedEmail("  Test@Example.COM  ");

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void Constructor_WithNullEmail_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new NormalizedEmail(null!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Email*");
    }

    [Fact]
    public void Constructor_WithEmptyEmail_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new NormalizedEmail(string.Empty);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Email*");
    }

    [Fact]
    public void Constructor_WithWhitespaceEmail_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new NormalizedEmail("   ");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Email*");
    }

    [Fact]
    public void Equals_WithSameEmailDifferentCase_ReturnsTrue()
    {
        // Arrange
        var email1 = new NormalizedEmail("test@example.com");
        var email2 = new NormalizedEmail("TEST@EXAMPLE.COM");

        // Act & Assert
        email1.Should().Be(email2);
        (email1 == email2).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithStringDifferentCase_ReturnsTrue()
    {
        // Arrange
        var email = new NormalizedEmail("test@example.com");

        // Act & Assert
        email.Equals("TEST@EXAMPLE.COM").Should().BeTrue();
    }

    [Fact]
    public void ImplicitConversion_FromString_Works()
    {
        // Act
        NormalizedEmail email = "Test@Example.COM";

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void ImplicitConversion_ToString_Works()
    {
        // Arrange
        var email = new NormalizedEmail("test@example.com");

        // Act
        string emailString = email;

        // Assert
        emailString.Should().Be("test@example.com");
    }

    [Fact]
    public void ToString_ReturnsNormalizedValue()
    {
        // Arrange
        var email = new NormalizedEmail("Test@Example.COM");

        // Act
        var result = email.ToString();

        // Assert
        result.Should().Be("test@example.com");
    }

    [Fact]
    public void GetHashCode_WithSameEmail_ReturnsSameHash()
    {
        // Arrange
        var email1 = new NormalizedEmail("test@example.com");
        var email2 = new NormalizedEmail("TEST@EXAMPLE.COM");

        // Act & Assert
        email1.GetHashCode().Should().Be(email2.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentEmail_ReturnsFalse()
    {
        // Arrange
        var email1 = new NormalizedEmail("test1@example.com");
        var email2 = new NormalizedEmail("test2@example.com");

        // Act & Assert
        email1.Should().NotBe(email2);
        (email1 == email2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithNullString_ReturnsFalse()
    {
        // Arrange
        var email = new NormalizedEmail("test@example.com");

        // Act & Assert
        email.Equals((string?)null).Should().BeFalse();
    }
}
