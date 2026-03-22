using Microsoft.AspNetCore.Identity;
using RavenDB.AspNetCore.Identity.ValueObjects;

namespace RavenDB.AspNetCore.Identity.Models;

/// <summary>
/// Base user class for RavenDB Identity. Inherit from this class to add your own properties.
/// </summary>
public abstract class RavenIdentityUser
{
    private List<string> _roles = new();

    /// <summary>
    /// The ID of the user.
    /// </summary>
    public virtual string? Id { get; set; }

    /// <summary>
    /// The user name. Usually the same as the email.
    /// </summary>
    public virtual string? UserName { get; set; } = string.Empty;

    /// <summary>
    /// The password hash.
    /// </summary>
    public virtual string? PasswordHash { get; set; }

    /// <summary>
    /// The security stamp.
    /// </summary>
    public virtual string? SecurityStamp { get; set; }

    /// <summary>
    /// The concurrency stamp.
    /// </summary>
    public virtual string? ConcurrencyStamp { get; set; }

    private string _email = string.Empty;

    /// <summary>
    /// The email of the user. Automatically normalized to lowercase on set.
    /// </summary>
    public virtual string Email
    {
        get => _email;
        set => _email = string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new NormalizedEmail(value).Value;
    }

    /// <summary>
    /// The phone number.
    /// </summary>
    public virtual string? PhoneNumber { get; set; }

    /// <summary>
    /// Whether the user has confirmed their email address.
    /// </summary>
    public virtual bool EmailConfirmed { get; set; }

    /// <summary>
    /// Whether the user has confirmed their phone.
    /// </summary>
    public virtual bool PhoneNumberConfirmed { get; set; }

    /// <summary>
    /// Number of times sign in failed.
    /// </summary>
    public virtual int AccessFailedCount { get; set; }

    /// <summary>
    /// Whether the user is locked out.
    /// </summary>
    public virtual bool LockoutEnabled { get; set; }

    /// <summary>
    /// When the user lock out is over.
    /// </summary>
    public virtual DateTimeOffset? LockoutEnd { get; set; }

    /// <summary>
    /// Whether 2-factor authentication is enabled.
    /// </summary>
    public virtual bool TwoFactorEnabled { get; set; }

    /// <summary>
    /// The two-factor authenticator key.
    /// </summary>
    public string? TwoFactorAuthenticatorKey { get; set; }

    /// <summary>
    /// The roles of the user. To modify the user's roles, use <see cref="UserManager{TUser}.AddToRoleAsync(TUser, string)"/> and <see cref="UserManager{TUser}.RemoveFromRolesAsync(TUser, IEnumerable{string})"/>.
    /// </summary>
    public virtual IReadOnlyList<string> Roles
    {
        get => _roles;
        private set => _roles = value.ToList();
    }

    /// <summary>
    /// The user's claims, for use in claims-based authentication.
    /// </summary>
    public virtual List<System.Security.Claims.Claim> Claims { get; private set; } = new();

    /// <summary>
    /// The logins of the user.
    /// </summary>
    public virtual List<UserLoginInfo> Logins { get; private set; } = new List<UserLoginInfo>();

    /// <summary>
    /// The list of two factor authentication recovery codes.
    /// </summary>
    public virtual List<string> TwoFactorRecoveryCodes { get; set; } = new List<string>();

    /// <summary>
    /// The list of authorization tokens from 3rd party authentication, e.g. Google, Microsoft, GitHub, etc.
    /// </summary>
    public virtual List<IdentityUserAuthToken> Tokens { get; set; } = new List<IdentityUserAuthToken>();

    /// <summary>
    /// Gets the mutable roles list. This shouldn't be modified by user code; roles should be changed via UserManager instead.
    /// </summary>
    /// <returns>The internal mutable list of role names.</returns>
    internal List<string> GetRolesList() => _roles;
}

/// <summary>
/// Entity that aids in loading users by a well-known name directly from the RavenDB ACID storage engine, bypassing the eventually consistent RavenDB indexes.
/// </summary>
[Obsolete("This has been replaced with RavenDB compare/exchange values which work cluster-wide.")]
public sealed class IdentityUserByUserName
{
    /// <summary>
    /// The ID of the user.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// The user name.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Creates a new IdentityUserByUserName.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="userName">The user name.</param>
    public IdentityUserByUserName(string userId, string userName)
    {
        UserId = userId;
        UserName = userName;
    }
}