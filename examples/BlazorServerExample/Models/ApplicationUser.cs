using RavenDB.AspNetCore.Identity.Models;

namespace BlazorServerExample.Models;

/// <summary>
/// Application user with custom properties.
/// </summary>
public class ApplicationUser : RavenIdentityUser
{
    /// <summary>
    /// User's display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Date when the user registered.
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}