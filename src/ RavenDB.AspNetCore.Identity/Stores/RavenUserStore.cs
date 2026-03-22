using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations.CompareExchange;
using Raven.Client.Documents.Session;
using RavenDB.AspNetCore.Identity.Infrastructure;
using RavenDB.AspNetCore.Identity.Models;
using RavenDB.AspNetCore.Identity.ValueObjects;

namespace RavenDB.AspNetCore.Identity.Stores;

public class RavenUserStore<TUser> :
    IUserStore<TUser>,
    IUserPasswordStore<TUser>,
    IUserLockoutStore<TUser>,
    IUserEmailStore<TUser>,
    IUserLoginStore<TUser>,
    IUserPhoneNumberStore<TUser>,
    IUserRoleStore<TUser>,
    IUserSecurityStampStore<TUser>
    where TUser : RavenIdentityUser, new()
{
    private bool _disposed;
    private readonly IAsyncDocumentSession _session;
    private readonly ILogger _logger;

    public RavenUserStore(IAsyncDocumentSession session, ILogger<RavenUserStore<TUser>> logger)
    {
        _session = session;
        _logger = logger;
    }

    public void Dispose()
    {
        _disposed = true;
    }

    /// <inheritdoc />
    public virtual Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Id))
        {
            throw new InvalidOperationException("User ID cannot be null or empty.");
        }

        return Task.FromResult(user.Id);
    }

    public virtual Task<string?> GetUserNameAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.UserName);

    public virtual Task SetUserNameAsync(TUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public virtual Task<string?> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.UserName);

    public virtual Task SetNormalizedUserNameAsync(TUser user, string? normalizedName,
        CancellationToken cancellationToken)
    {
        user.UserName = normalizedName?.ToLowerInvariant() ?? string.Empty;
        return Task.CompletedTask;
    }

    public virtual async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);

        // Make sure we have a valid email address, as we use this for uniqueness.
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new ArgumentException("The user's email address can't be null or empty.", nameof(user));
        }

        // Email is already normalized by RavenIdentityUser.Email setter
        var normalizedEmail = new NormalizedEmail(user.Email);
        user.UserName = user.UserName?.ToLowerInvariant() ?? user.Email;

        // See if the email address is already taken.
        // We do this using Raven's compare/exchange functionality, which works cluster-wide.
        // https://ravendb.net/docs/article-page/4.1/csharp/client-api/operations/compare-exchange/overview#creating-a-key
        //
        // User creation is done in 3 steps:
        // 1. Reserve the email address, pointing to an empty user ID.
        // 2. Store the user and save it.
        // 3. Update the email address reservation to point to the new user's email.

        // 1. Reserve the email address.
        _logger.LogDebug("Creating email reservation for {Email}", normalizedEmail.Value);
        var reserveEmailResult =
            await CreateEmailReservationAsync(normalizedEmail,
                string.Empty); // Empty string: Just reserve it for now while we create the user and assign the user's ID.
        if (!reserveEmailResult.Successful)
        {
            _logger.LogError("Error creating email reservation for {Email}", normalizedEmail.Value);
            return IdentityResult.Failed(new IdentityErrorDescriber().DuplicateEmail(normalizedEmail.Value));
        }

        // 2. Store the user in the database and save it.
        try
        {
            await _session.StoreAsync(user, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            // 3. Update the email reservation to point to the saved user.
            if (string.IsNullOrWhiteSpace(user.Id))
            {
                throw new InvalidOperationException("User ID was not assigned by RavenDB after save.");
            }

            var updateReservationResult = await UpdateEmailReservationAsync(normalizedEmail, user.Id);
            if (!updateReservationResult.Successful)
            {
                _logger.LogError("Error updating email reservation for {Email} to {UserId}", normalizedEmail.Value,
                    user.Id);
                throw new Exception("Unable to update the email reservation");
            }
        }
        catch (Exception createUserError)
        {
            // The compare/exchange email reservation is cluster-wide, outside of the session scope.
            // We need to manually roll it back.
            _logger.LogError(createUserError, "Error during user creation");
            _session.Delete(user); // It's possible user is already saved to the database. If so, delete him.
            try
            {
                await DeleteEmailReservation(normalizedEmail);
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "Caught an exception trying to remove user email reservation for {Email} after save failed. An admin must manually delete the compare exchange key {CompareExchangeKey}",
                    normalizedEmail.Value, Conventions.CompareExchangeKeyFor(normalizedEmail.Value));
            }

            return IdentityResult.Failed(new IdentityErrorDescriber().DefaultError());
        }

        return IdentityResult.Success;
    }

    /// <inheritdoc />
    public virtual async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);

        // Make sure we have a valid email address.
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new ArgumentException("The user's email address can't be null or empty.", nameof(user));
        }

        if (string.IsNullOrWhiteSpace(user.Id))
        {
            throw new ArgumentException("The user can't have a null ID.");
        }

        // If nothing changed we have no work to do
        var changes = _session.Advanced.WhatChanged();
        var hasUserChanged = changes.TryGetValue(user.Id, out var userChange);
        if (!hasUserChanged || userChange == null)
        {
            _logger.LogWarning("UserStore UpdateAsync called without any changes to the User {UserId}", user.Id);

            // No changes to this document
            return IdentityResult.Success;
        }

        // Check if their changed their email. If not, the rest of the code is unnecessary
        var emailChange = userChange.FirstOrDefault(x => string.Equals(x.FieldName, nameof(user.Email)));

        if (emailChange == null)
        {
            _logger.LogTrace("User {UserId} did not have modified Email, saving normally", user.Id);

            // Email didn't change, so no reservation to update. Just save the user data
            await _session.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }

        // If the user changed their email, we need to update the email compare/exchange reservation.
        // Get the previous value for their email

        var oldEmailString = emailChange.FieldOldValue?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(oldEmailString))
        {
            throw new InvalidOperationException("Previous email value cannot be null or empty during email update");
        }

        var oldEmail = new NormalizedEmail(oldEmailString);
        // Email is already normalized by RavenIdentityUser.Email setter
        var newEmail = new NormalizedEmail(user.Email);

        if (string.Equals(user.UserName, oldEmailString, StringComparison.InvariantCultureIgnoreCase))
        {
            _logger.LogTrace("Updating username to match modified email for {UserId}", user.Id);

            // The username was set to their email so we should update username as well.
            user.UserName = user.Email;
        }

        // See if the email change was only due to case sensitivity.
        if (oldEmail.Equals(newEmail.Value))
        {
            return IdentityResult.Success;
        }

        // Create the new email reservation.
        var emailReservation = await CreateEmailReservationAsync(newEmail, user.Id);
        if (!emailReservation.Successful)
        {
            _session.Advanced.IgnoreChangesFor(user);
            return IdentityResult.Failed(new IdentityErrorDescriber().DuplicateEmail(newEmail.Value));
        }

        // Save the user document changes to the database
        await _session.SaveChangesAsync(cancellationToken);

        await TryRemoveMigratedEmailReservation(oldEmail, newEmail);
        return IdentityResult.Success;
    }

    public virtual async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new ArgumentException("The user's email address can't be null or empty.", nameof(user));
        }

        var normalizedEmail = new NormalizedEmail(user.Email);

        // Delete the user and save it. We must save it because deleting is a cluster-wide operation.
        // Only if the deletion succeeds will we remove the cluster-wide compare/exchange key.
        _session.Delete(user);
        await _session.SaveChangesAsync(cancellationToken);

        // Delete was successful, remove the cluster-wide compare/exchange key.
        var deletionResult = await DeleteEmailReservation(normalizedEmail);
        if (!deletionResult.Successful)
        {
            _logger.LogWarning(
                "User was deleted, but there was an error deleting email reservation for {Email}. The compare/exchange value for this should be manually deleted",
                normalizedEmail.Value);
        }

        return IdentityResult.Success;
    }

    public virtual async Task<TUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        await _session.LoadAsync<TUser>(userId, cancellationToken);

    public virtual async Task<TUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return await _session.Query<TUser>()
            .SingleOrDefaultAsync(u => u.UserName == normalizedUserName, cancellationToken);
    }

    public virtual Task SetPasswordHashAsync(TUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);

        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public virtual Task<string?> GetPasswordHashAsync(TUser user, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        return Task.FromResult(user.PasswordHash);
    }

    public virtual Task<bool> HasPasswordAsync(TUser user, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        return Task.FromResult(user.PasswordHash != null);
    }

    public virtual Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        return Task.FromResult(user.LockoutEnd);
    }

    public virtual Task SetLockoutEndDateAsync(TUser user, DateTimeOffset? lockoutEnd,
        CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public virtual Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        user.AccessFailedCount++;
        return Task.FromResult(user.AccessFailedCount);
    }

    public virtual Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public virtual Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        return Task.FromResult(user.AccessFailedCount);
    }

    public virtual Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        return Task.FromResult(user.LockoutEnabled);
    }

    public Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    public virtual Task SetEmailAsync(TUser user, string? email, CancellationToken cancellationToken)
    {
        ThrowIfDisposedOrCancelled(cancellationToken);
        // Email normalization happens automatically in RavenIdentityUser.Email setter
        user.Email = email ?? throw new ArgumentNullException(nameof(email));
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.Email);

    public Task<bool> GetEmailConfirmedAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public virtual async Task<TUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        // While we could just do an index query here: DbSession.Query<TUser>().FirstOrDefaultAsync(u => u.Email == normalizedEmail)
        // We decided against this because indexes can be stale.
        // Instead, we're going to go straight to the compare/exchange values and find the user for the email.

        // Ensure the email is properly normalized
        var email = new NormalizedEmail(normalizedEmail);
        var key = Conventions.CompareExchangeKeyFor(email.Value);
        var readResult =
            await _session.Advanced.DocumentStore.Operations.SendAsync(
                new GetCompareExchangeValueOperation<string>(key));
        if (readResult == null || string.IsNullOrEmpty(readResult.Value))
        {
            return null;
        }

        return await _session.LoadAsync<TUser>(readResult.Value, cancellationToken);
    }

    public virtual Task<string?> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.Email);

    public virtual Task SetNormalizedEmailAsync(TUser user, string? normalizedEmail,
        CancellationToken cancellationToken)
    {
        // Email normalization happens automatically in RavenIdentityUser.Email setter
        user.Email = normalizedEmail ?? string.Empty;
        return Task.CompletedTask;
    }

    public virtual Task AddLoginAsync(TUser user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        ArgumentNullException.ThrowIfNull(login);

        user.Logins.Add(login);
        return Task.CompletedTask;
    }

    public virtual Task RemoveLoginAsync(TUser user, string loginProvider, string providerKey,
        CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        user.Logins.RemoveAll(l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey);
        return Task.CompletedTask;
    }

    public virtual Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        return Task.FromResult<IList<UserLoginInfo>>(user.Logins);
    }

    public virtual async Task<TUser?> FindByLoginAsync(string loginProvider, string providerKey,
        CancellationToken cancellationToken)
    {
        return await _session.Query<TUser>()
            .FirstOrDefaultAsync(
                u => u.Logins.Any(l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey),
                token: cancellationToken);
    }

    public Task AddToRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
    {
        // TODO: Implement role management once RavenRoleStore is implemented
        return Task.CompletedTask;
    }

    public Task RemoveFromRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
    {
        // TODO: Implement role management once RavenRoleStore is implemented
        return Task.CompletedTask;
    }

    public Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken)
    {
        // TODO: Implement role management once RavenRoleStore is implemented
        // For now, return empty list to allow authentication to work
        return Task.FromResult<IList<string>>(Array.Empty<string>());
    }

    public Task<bool> IsInRoleAsync(TUser user, string roleName, CancellationToken cancellationToken)
    {
        // TODO: Implement role management once RavenRoleStore is implemented
        // For now, return false to allow authentication to work
        return Task.FromResult(false);
    }

    public Task<IList<TUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        // TODO: Implement role management once RavenRoleStore is implemented
        // For now, return empty list to allow queries to work
        return Task.FromResult<IList<TUser>>(Array.Empty<TUser>());
    }

    public virtual Task SetSecurityStampAsync(TUser user, string stamp, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(TUser user, CancellationToken cancellationToken)
    {
        ThrowIfNullDisposedCancelled(user, cancellationToken);
        return Task.FromResult(user.SecurityStamp);
    }

    private void ThrowIfNullDisposedCancelled(TUser user, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(user);
        token.ThrowIfCancellationRequested();
    }

    private void ThrowIfDisposedOrCancelled(CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        token.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Updates an existing email reservation to point to a new user ID.
    /// </summary>
    /// <param name="email">The normalized email address.</param>
    /// <param name="id">The user ID to associate with the email.</param>
    /// <returns>The result of the compare/exchange operation.</returns>
    protected virtual async Task<CompareExchangeResult<string>> UpdateEmailReservationAsync(NormalizedEmail email,
        string id)
    {
        var key = Conventions.CompareExchangeKeyFor(email.Value);
        var store = _session.Advanced.DocumentStore;

        var readResult = await store.Operations.SendAsync(new GetCompareExchangeValueOperation<string>(key));
        if (readResult == null)
        {
            _logger.LogError("Failed to get current index for {EmailReservation} to update it to {ReservedFor}", key,
                id);
            return new CompareExchangeResult<string>() { Successful = false };
        }

        var updateEmailUserIdOperation = new PutCompareExchangeValueOperation<string>(key, id, readResult.Index);
        return await store.Operations.SendAsync(updateEmailUserIdOperation);
    }

    /// <summary>
    /// Removes an email reservation.
    /// </summary>
    /// <param name="email">The normalized email address to remove.</param>
    /// <returns>The result of the compare/exchange delete operation.</returns>
    protected virtual async Task<CompareExchangeResult<string>> DeleteEmailReservation(NormalizedEmail email)
    {
        var key = Conventions.CompareExchangeKeyFor(email.Value);
        var store = _session.Advanced.DocumentStore;

        var readResult = await store.Operations.SendAsync(new GetCompareExchangeValueOperation<string>(key));
        if (readResult == null)
        {
            _logger.LogError("Failed to get current index for {EmailReservation} to delete it", key);
            return new CompareExchangeResult<string>() { Successful = false };
        }

        var deleteEmailOperation = new DeleteCompareExchangeValueOperation<string>(key, readResult.Index);
        return await _session.Advanced.DocumentStore.Operations.SendAsync(deleteEmailOperation);
    }

    /// <summary>
    /// Creates a new email reservation in Compare/Exchange.
    /// </summary>
    /// <param name="email">The normalized email address to reserve.</param>
    /// <param name="id">The user ID to associate with the email (empty string for initial reservation).</param>
    /// <returns>The result of the compare/exchange create operation.</returns>
    protected virtual Task<CompareExchangeResult<string>> CreateEmailReservationAsync(NormalizedEmail email, string id)
    {
        var compareExchangeKey = Conventions.CompareExchangeKeyFor(email.Value);
        var reserveEmailOperation = new PutCompareExchangeValueOperation<string>(compareExchangeKey, id, 0);
        return _session.Advanced.DocumentStore.Operations.ForDatabase(((AsyncDocumentSession)_session).DatabaseName)
            .SendAsync(reserveEmailOperation);
    }

    /// <summary>
    /// Attempts to remove an old email reservation as part of a migration from one email to another.
    /// If unsuccessful, a warning will be logged, but no exception will be thrown.
    /// </summary>
    private async Task TryRemoveMigratedEmailReservation(NormalizedEmail oldEmail, NormalizedEmail newEmail)
    {
        var deleteEmailResult = await DeleteEmailReservation(oldEmail);
        if (!deleteEmailResult.Successful)
        {
            // If this happens, it's not critical: the user still changed their email successfully.
            // They just won't be able to register again with their old email. Log a warning.
            _logger.LogWarning(
                "When user changed email from {OldEmail} to {NewEmail}, there was an error removing the old email reservation. The compare exchange key {CompareExchangeKey} should be removed manually by an admin",
                oldEmail.Value, newEmail.Value, Conventions.CompareExchangeKeyFor(oldEmail.Value));
        }
    }

    public Task SetPhoneNumberAsync(TUser user, string? phoneNumber, CancellationToken cancellationToken)
    {
        ThrowIfDisposedOrCancelled(cancellationToken);
        user.PhoneNumber = phoneNumber;
        return Task.CompletedTask;
    }

    public Task<string?> GetPhoneNumberAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PhoneNumber);

    public Task<bool> GetPhoneNumberConfirmedAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PhoneNumberConfirmed);

    public Task SetPhoneNumberConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
    {
        ThrowIfDisposedOrCancelled(cancellationToken);
        user.PhoneNumberConfirmed = confirmed;
        return Task.CompletedTask;
    }
}