namespace RavenDB.AspNetCore.Identity.ValueObjects;

/// <summary>
/// Represents a normalized email address that is guaranteed to be lowercase and non-empty.
/// This value object enforces email normalization at compile time, preventing bugs related
/// to case-sensitivity in email comparisons and Compare/Exchange operations.
/// </summary>
public readonly record struct NormalizedEmail
{
    /// <summary>
    /// Creates a new normalized email. The email is automatically converted to lowercase.
    /// </summary>
    /// <param name="email">The email address to normalize</param>
    /// <exception cref="ArgumentException">Thrown if the email is null, empty, or whitespace</exception>
    public NormalizedEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null, empty, or whitespace.", nameof(email));
        }

        Value = email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Gets the normalized (lowercase) email value.
    /// </summary>
    public string Value => field ?? string.Empty;

    /// <summary>
    /// Implicit conversion from string to NormalizedEmail.
    /// </summary>
    public static implicit operator NormalizedEmail(string email) => new(email);

    /// <summary>
    /// Implicit conversion from NormalizedEmail to string.
    /// </summary>
    public static implicit operator string(NormalizedEmail email) => email.Value;

    /// <summary>
    /// Returns the normalized email string.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// Checks if two emails are equal (case-insensitive).
    /// </summary>
    public bool Equals(string? other) =>
        string.Equals(Value, other, StringComparison.OrdinalIgnoreCase);
}