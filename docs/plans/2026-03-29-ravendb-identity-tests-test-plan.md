# Test Plan: RavenDB.AspNetCore.Identity Test Suite

**Date:** 2026-03-29
**Feature:** Comprehensive test coverage for RavenDB.AspNetCore.Identity
**Status:** Approved

## Strategy Reconciliation

After reviewing the implementation plan and codebase, the testing strategy from PLAN.md remains valid:

- **Harness:** RavenDB.TestDriver with xUnit provides true integration testing
- **Coverage Target:** >80% for RavenUserStore
- **Approach:** Integration tests over unit tests, real database operations over mocks
- **Key Focus:** Email uniqueness via compare/exchange (highest business value)

No strategy changes required.

## Test Infrastructure

### Harness: RavenDbTestBase

**Purpose:** Base class for all tests using RavenDB.TestDriver

**Capabilities:**
- Embedded RavenDB server instances (isolated per test)
- Unique database per test via `[CallerMemberName]`
- Helper methods for compare/exchange verification
- TestUser concrete class (RavenIdentityUser is abstract)

**Complexity:** Low (2-3 hours)

**Dependencies:** RavenDB.TestDriver 7.2.1, xUnit, FluentAssertions, Moq

## Test Plan

### 1. Email Uniqueness & Compare/Exchange Tests (Priority 1)

**File:** `RavenUserStoreEmailTests.cs`

**Type:** integration | invariant
**Harness:** RavenDbTestBase
**Source of Truth:** RavenUserStore implementation lines 67-251, ASP.NET Core Identity contracts

#### 1.1 Email reservation created on user creation
- **Preconditions:** Clean database
- **Actions:** CreateAsync with unique email
- **Expected:** User created, compare/exchange reservation exists with user ID
- **Interactions:** RavenDB compare/exchange, document store

#### 1.2 Duplicate email rejected
- **Preconditions:** User exists with email
- **Actions:** CreateAsync with same email
- **Expected:** IdentityResult.Failed, no second user created
- **Interactions:** Compare/exchange prevents duplicate

#### 1.3 Concurrent duplicate emails - only one succeeds
- **Type:** integration | boundary
- **Preconditions:** Clean database
- **Actions:** Two parallel CreateAsync with same email
- **Expected:** One succeeds, one fails with DuplicateEmail
- **Interactions:** Compare/exchange race condition handling

#### 1.4 Email reservation updated when email changes
- **Preconditions:** User with email A exists
- **Actions:** UpdateAsync changing email to B
- **Expected:** New reservation B created, old reservation A deleted, username updated if matched
- **Interactions:** Compare/exchange migration

#### 1.5 Case-only email change doesn't migrate reservation
- **Preconditions:** User with "test@example.com"
- **Actions:** UpdateAsync changing to "TEST@example.com"
- **Expected:** Email normalized, no reservation migration (same normalized value)
- **Interactions:** NormalizedEmail value object

#### 1.6 Email change to duplicate fails
- **Preconditions:** Two users with different emails
- **Actions:** UpdateAsync changing user A's email to match user B
- **Expected:** IdentityResult.Failed, user A keeps original email and reservation
- **Interactions:** Compare/exchange prevents collision

#### 1.7 Email reservation deleted on user deletion
- **Preconditions:** User with email exists
- **Actions:** DeleteAsync
- **Expected:** User deleted, email reservation deleted
- **Interactions:** Cleanup compare/exchange

#### 1.8 FindByEmailAsync uses compare/exchange directly
- **Preconditions:** User exists with email
- **Actions:** FindByEmailAsync
- **Expected:** User found via compare/exchange lookup (bypasses indexes)
- **Interactions:** Compare/exchange read, document load

#### 1.9 FindByEmailAsync with nonexistent email returns null
- **Preconditions:** Clean database
- **Actions:** FindByEmailAsync with random email
- **Expected:** Null returned
- **Interactions:** Compare/exchange read

#### 1.10 SetEmailConfirmedAsync sets flag
- **Preconditions:** User exists
- **Actions:** SetEmailConfirmedAsync(true)
- **Expected:** EmailConfirmed = true after SaveChangesAsync
- **Interactions:** Document update

#### 1.11 GetEmailConfirmedAsync returns flag
- **Preconditions:** User with EmailConfirmed = true
- **Actions:** GetEmailConfirmedAsync
- **Expected:** Returns true
- **Interactions:** Property read

### 2. User CRUD Operations Tests (Priority 2)

**File:** `RavenUserStoreTests.cs`

**Type:** integration | boundary
**Harness:** RavenDbTestBase
**Source of Truth:** IUserStore contract, RavenUserStore implementation

#### 2.1 CreateAsync with valid user succeeds
- **Actions:** CreateAsync with valid TestUser
- **Expected:** IdentityResult.Success, user loadable by ID
- **Interactions:** Document store, compare/exchange

#### 2.2 CreateAsync with null email throws ArgumentException
- **Actions:** CreateAsync with user.Email = null
- **Expected:** ArgumentException before SaveChangesAsync
- **Interactions:** Validation

#### 2.3 CreateAsync with empty email throws ArgumentException
- **Actions:** CreateAsync with user.Email = ""
- **Expected:** ArgumentException before SaveChangesAsync
- **Interactions:** Validation

#### 2.4 UpdateAsync without changes logs warning and succeeds
- **Actions:** UpdateAsync on unchanged user
- **Expected:** IdentityResult.Success, warning logged via WhatChanged()
- **Interactions:** Change tracking

#### 2.5 UpdateAsync with property changes saves changes
- **Actions:** Modify user.UserName, call UpdateAsync
- **Expected:** IdentityResult.Success, changes persisted
- **Interactions:** Document update

#### 2.6 DeleteAsync removes user
- **Actions:** DeleteAsync on existing user
- **Expected:** IdentityResult.Success, user not findable
- **Interactions:** Document delete, compare/exchange cleanup

#### 2.7 FindByIdAsync with existing user returns user
- **Actions:** FindByIdAsync with valid ID
- **Expected:** User returned
- **Interactions:** Document load

#### 2.8 FindByIdAsync with nonexistent ID returns null
- **Actions:** FindByIdAsync with random ID
- **Expected:** Null returned
- **Interactions:** Document load miss

#### 2.9 FindByNameAsync with existing user returns user
- **Actions:** FindByNameAsync with valid username
- **Expected:** User returned
- **Interactions:** Query by UserName

#### 2.10 FindByNameAsync with nonexistent name returns null
- **Actions:** FindByNameAsync with random name
- **Expected:** Null returned
- **Interactions:** Query miss

#### 2.11 GetUserIdAsync with null ID throws InvalidOperationException
- **Actions:** GetUserIdAsync on user with Id = null
- **Expected:** InvalidOperationException
- **Interactions:** Validation

#### 2.12 SetNormalizedUserNameAsync normalizes to lowercase
- **Actions:** SetNormalizedUserNameAsync("TestUser")
- **Expected:** UserName = "testuser"
- **Interactions:** Normalization

#### 2.13 GetNormalizedUserNameAsync returns username
- **Actions:** GetNormalizedUserNameAsync
- **Expected:** Returns UserName value
- **Interactions:** Property read

#### 2.14 Dispose sets disposed flag
- **Actions:** store.Dispose()
- **Expected:** _disposed = true
- **Interactions:** Disposal

#### 2.15 CreateAsync after Dispose throws ObjectDisposedException
- **Actions:** Dispose(), then CreateAsync
- **Expected:** ObjectDisposedException
- **Interactions:** Disposal guard

### 3. Password Operations Tests

**File:** `RavenUserStorePasswordTests.cs`

**Type:** unit | integration
**Harness:** RavenDbTestBase

#### 3.1 SetPasswordHashAsync sets hash
- **Expected:** PasswordHash property updated
#### 3.2 GetPasswordHashAsync returns hash
- **Expected:** Returns PasswordHash value
#### 3.3 HasPasswordAsync with hash returns true
- **Expected:** Returns true when PasswordHash != null
#### 3.4 HasPasswordAsync without hash returns false
- **Expected:** Returns false when PasswordHash == null

### 4. Lockout Operations Tests

**File:** `RavenUserStoreLockoutTests.cs`

**Type:** unit | integration
**Harness:** RavenDbTestBase

#### 4.1-4.7 Standard lockout operations
- Set/Get LockoutEndDate
- Increment/Reset/Get AccessFailedCount
- Set/Get LockoutEnabled

### 5. External Login Operations Tests

**File:** `RavenUserStoreLoginTests.cs`

**Type:** integration
**Harness:** RavenDbTestBase

#### 5.1 AddLoginAsync adds login
- **Expected:** Login added to Logins collection
#### 5.2 RemoveLoginAsync removes login
- **Expected:** Login removed from collection
#### 5.3 GetLoginsAsync returns all logins
- **Expected:** All logins returned
#### 5.4 FindByLoginAsync with existing login returns user
- **Expected:** User found by login provider/key
#### 5.5 FindByLoginAsync with nonexistent login returns null
- **Expected:** Null returned

### 6. Security Operations Tests

**File:** `RavenUserStoreSecurityTests.cs`

**Type:** unit

#### 6.1 SetSecurityStampAsync sets stamp
#### 6.2 GetSecurityStampAsync returns stamp

### 7. Phone Operations Tests

**File:** `RavenUserStorePhoneTests.cs`

**Type:** unit

#### 7.1 SetPhoneNumberAsync sets number
#### 7.2 GetPhoneNumberAsync returns number
#### 7.3 SetPhoneNumberConfirmedAsync sets flag
#### 7.4 GetPhoneNumberConfirmedAsync returns flag

### 8. Session Lifecycle Tests

**File:** `RavenUserStoreSessionTests.cs`

**Type:** integration | boundary
**Harness:** RavenDbTestBase

#### 8.1 CreateAsync with disposed session throws
- **Expected:** ObjectDisposedException
#### 8.2 FindByIdAsync with disposed session throws
- **Expected:** ObjectDisposedException
#### 8.3 Multiple operations on same session work correctly
- **Expected:** Sequential operations succeed
#### 8.4 CreateThenFind on same session returns user
- **Expected:** User findable before session disposal

### 9. Value Objects & Models Tests

**File:** `NormalizedEmailTests.cs` (unit)

#### 9.1-9.7 NormalizedEmail behavior
- Constructor normalization
- Whitespace trimming
- Null/empty validation
- Equality comparison
- Implicit conversions

**File:** `RavenIdentityUserTests.cs` (unit)

#### 10.1-10.3 Email setter behavior
- Normalizes to lowercase
- Handles mixed case
- Trims whitespace

### 10. DI Registration Tests

**File:** `IdentityBuilderExtensionsTests.cs` (unit, no RavenDB)

#### 11.1-11.6 Service registration verification
- Both stores registered
- User-only registration
- Options configuration

## Coverage Summary

**Covered:**
- Email uniqueness invariants (11 tests)
- User CRUD operations (15 tests)
- ASP.NET Core Identity interfaces (28 tests total)
- Session lifecycle (4 tests)
- Value objects (10 tests)
- DI registration (6 tests)

**Total:** ~74 tests

**Explicitly Excluded:**
- RavenRoleStore (unimplemented)
- IUserRoleStore methods (return empty/false)
- ID generation conventions (user-configured)
- Static indexes
- Multi-node clusters

**Risks of Exclusions:** Low - excluded features are documented as unimplemented or user-responsibility

## Success Criteria

1. All 74 tests pass
2. >80% code coverage for RavenUserStore
3. Tests run in <2 minutes
4. No flaky tests (unique databases prevent pollution)
5. Email uniqueness invariants verified