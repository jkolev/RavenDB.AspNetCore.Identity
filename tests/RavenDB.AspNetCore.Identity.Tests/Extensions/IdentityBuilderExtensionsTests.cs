using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RavenDB.AspNetCore.Identity.Extensions;
using RavenDB.AspNetCore.Identity.Infrastructure;
using RavenDB.AspNetCore.Identity.Models;
using RavenDB.AspNetCore.Identity.Stores;
using Xunit;

namespace RavenDB.AspNetCore.Identity.Tests.Extensions;

/// <summary>
/// Unit tests for DI registration extensions.
/// These tests verify service registration without requiring RavenDB.
/// </summary>
public class IdentityBuilderExtensionsTests
{
    private class TestUser : RavenIdentityUser { }
    private class TestRole : RavenIdentityRole { }

    [Fact]
    public void AddRavenDbIdentityStores_WithBothTypes_RegistersUserStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddIdentity<TestUser, TestRole>();

        // Act
        builder.AddRavenDbIdentityStores<TestUser, TestRole>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var userStoreDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IUserStore<TestUser>));
        userStoreDescriptor.Should().NotBeNull();
        userStoreDescriptor!.ImplementationType.Should().Be(typeof(RavenUserStore<TestUser>));
        userStoreDescriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddRavenDbIdentityStores_WithBothTypes_RegistersRoleStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddIdentity<TestUser, TestRole>();

        // Act
        builder.AddRavenDbIdentityStores<TestUser, TestRole>();

        // Assert
        var roleStoreDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IRoleStore<TestRole>));
        roleStoreDescriptor.Should().NotBeNull();
        roleStoreDescriptor!.ImplementationType.Should().Be(typeof(RavenRoleStore<TestRole>));
        roleStoreDescriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddRavenDbIdentityStores_UserOnly_RegistersOnlyUserStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddIdentity<TestUser, IdentityRole>();

        // Act
        builder.AddRavenDbIdentityStores<TestUser>();

        // Assert
        var userStoreDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IUserStore<TestUser>));
        userStoreDescriptor.Should().NotBeNull();
        userStoreDescriptor!.ImplementationType.Should().Be(typeof(RavenUserStore<TestUser>));
    }

    [Fact]
    public void AddRavenDbIdentityStores_UserOnly_DoesNotRegisterRoleStore()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddIdentity<TestUser, IdentityRole>();

        // Act
        builder.AddRavenDbIdentityStores<TestUser>();

        // Assert
        var roleStoreDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IRoleStore<IdentityRole>));
        roleStoreDescriptor.Should().BeNull("user-only registration should not register a role store");
    }

    [Fact]
    public void AddRavenDbIdentityStores_WithOptions_ConfiguresUseStaticIndexes()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddIdentity<TestUser, TestRole>();

        // Act
        builder.AddRavenDbIdentityStores<TestUser, TestRole>(options =>
        {
            options.UseStaticIndexes = true;
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<RavenDbIdentityOptions>>();
        options.Value.UseStaticIndexes.Should().BeTrue();
    }

    [Fact]
    public void AddRavenDbIdentityStores_WithOptions_ConfiguresAutoSaveChanges()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddIdentity<TestUser, TestRole>();

        // Act
        builder.AddRavenDbIdentityStores<TestUser, TestRole>(options =>
        {
            options.AutoSaveChanges = true;
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<RavenDbIdentityOptions>>();
        options.Value.AutoSaveChanges.Should().BeTrue();
    }

    [Fact]
    public void AddRavenDbIdentityStores_WithoutOptions_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddIdentity<TestUser, TestRole>();

        // Act
        builder.AddRavenDbIdentityStores<TestUser, TestRole>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<RavenDbIdentityOptions>>();
        options.Value.UseStaticIndexes.Should().BeFalse();
        options.Value.AutoSaveChanges.Should().BeFalse();
    }

    [Fact]
    public void AddRavenDbIdentityStores_WithMultipleOptions_ConfiguresBoth()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddIdentity<TestUser, TestRole>();

        // Act
        builder.AddRavenDbIdentityStores<TestUser, TestRole>(options =>
        {
            options.UseStaticIndexes = true;
            options.AutoSaveChanges = true;
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<RavenDbIdentityOptions>>();
        options.Value.UseStaticIndexes.Should().BeTrue();
        options.Value.AutoSaveChanges.Should().BeTrue();
    }

    [Fact]
    public void AddRavenDbIdentityStores_UserOnly_WithOptions_ConfiguresOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddIdentity<TestUser, IdentityRole>();

        // Act
        builder.AddRavenDbIdentityStores<TestUser>(options =>
        {
            options.UseStaticIndexes = true;
        });

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<RavenDbIdentityOptions>>();
        options.Value.UseStaticIndexes.Should().BeTrue();
    }

    [Fact]
    public void AddRavenDbIdentityStores_ReturnsIdentityBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddIdentity<TestUser, TestRole>();

        // Act
        var result = builder.AddRavenDbIdentityStores<TestUser, TestRole>();

        // Assert
        result.Should().BeSameAs(builder, "extension method should return the same builder for chaining");
    }

    [Fact]
    public void AddRavenDbIdentityStores_UserOnly_ReturnsIdentityBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddIdentity<TestUser, IdentityRole>();

        // Act
        var result = builder.AddRavenDbIdentityStores<TestUser>();

        // Assert
        result.Should().BeSameAs(builder, "extension method should return the same builder for chaining");
    }
}
