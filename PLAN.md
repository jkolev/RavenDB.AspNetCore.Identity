# Implementation Plan: RavenDB.AspNetCore.Identity Test Suite

## Plan Verdict
REVISED

## Objective
Add comprehensive test coverage for RavenDB.AspNetCore.Identity using RavenDB.TestDriver with xUnit.

## Architectural Decisions

### Test Framework Stack
- **Test Runner**: xUnit 2.9.0+ (modern, excellent async support, widely used in .NET ecosystem)
- **Test Driver**: RavenDB.TestDriver 7.2.0+ (must match RavenDB.Client 7.2.1 major version)
- **Assertion Library**: FluentAssertions 7.0.0+ (readable, expressive assertions)
- **Mocking**: Moq 4.20.0+ (for logger mocking only, avoid mocking RavenDB infrastructure)
- **Target Framework**: net10.0 (match main project)

**Justification**: RavenDB.TestDriver provides embedded RavenDB server instances for true integration testing rather than mocking. This catches real database behavior including compare/exchange operations, which are critical to email uniqueness guarantees. TestDriver version must match Client library major version for compatibility.

### Test Project Structure
```
tests/RavenDB.AspNetCore.Identity.Tests/
├── RavenDB.AspNetCore.Identity.Tests.csproj
├── Infrastructure/
│   └── RavenDbTestBase.cs                    # Base class with GetDocumentStore helper
├── Stores/
│   ├── RavenUserStoreTests.cs                # Core CRUD operations
│   ├── RavenUserStoreEmailTests.cs           # Email uniqueness & compare/exchange
│   ├── RavenUserStorePasswordTests.cs        # Password operations
│   ├── RavenUserStoreLockoutTests.cs         # Lockout functionality
│   ├── RavenUserStoreLoginTests.cs           # External login providers
│   ├── RavenUserStoreSecurityTests.cs        # Security stamp operations
│   ├── RavenUserStorePhoneTests.cs           # Phone number operations
│   └── RavenUserStoreSessionTests.cs         # Session lifecycle management
├── ValueObjects/
│   └── NormalizedEmailTests.cs               # Value object behavior
├── Models/
│   └── RavenIdentityUserTests.cs             # Email normalization in setter
└── Extensions/
    └── IdentityBuilderExtensionsTests.cs     # DI registration (unit tests only)
```

### Test Coverage Strategy

**Priority 1: Email Uniqueness & Compare/Exchange (Critical Business Logic)**
- Email reservation creation (3-step process)
- Email reservation rollback on failure
- Email update with reservation migration
- Email deletion with reservation cleanup
- Concurrent email registration attempts (race conditions)
- FindByEmailAsync bypassing stale indexes

**Priority 2: User CRUD Operations**
- CreateAsync with valid data
- UpdateAsync with email changes, without email changes
- DeleteAsync cleanup
- FindByIdAsync, FindByNameAsync

**Priority 3: ASP.NET Core Identity Interface Contracts**
- IUserPasswordStore operations
- IUserLockoutStore operations
- IUserLoginStore operations
- IUserSecurityStampStore operations
- IUserPhoneNumberStore operations
- IUserEmailStore operations

**Priority 4: Edge Cases & Error Handling**
- Null/empty email validation
- Disposed store detection
- Cancellation token handling
- Case-only email changes
- Orphaned email reservations
- Session disposal before store operations
- Multiple operations on same session

### Critical Test Scenarios

#### Email Uniqueness Invariants
1. **No two users can have same email** - verified via compare/exchange
2. **Email reservation matches user ID** - after successful creation
3. **Old email released on update** - prevents reservation leak
4. **Email released on deletion** - prevents reservation leak
5. **Failed creation leaves no reservation** - rollback works

#### Session Lifecycle Invariants
1. **Store operations require active session** - disposed session throws
2. **Multiple operations on same session** - work correctly when sequenced
3. **Store always calls SaveChangesAsync** - changes persist immediately (AutoSaveChanges option documented but not implemented)

#### Compare/Exchange Failure Modes
1. Duplicate email during CreateAsync -> IdentityResult.Failed with DuplicateEmail error
2. Store failure after email reservation -> rollback reservation, log error
3. Email reservation update failure -> log critical error, manual cleanup needed
4. Email reservation deletion failure -> log warning, manual cleanup needed

### Test Implementation Patterns

**Base Test Class Pattern**:
```csharp
public abstract class RavenDbTestBase : RavenTestDriver
{
    // CRITICAL: Use [CallerMemberName] for unique database per test to prevent cross-test pollution
    protected IDocumentStore GetTestDocumentStore([CallerMemberName] string? database = null)
    {
        var store = GetDocumentStore(database);
        store.Initialize(); // Initialize before using
        return store;
    }

    protected RavenUserStore<TUser> CreateUserStore<TUser>(
        IAsyncDocumentSession session,
        ILogger<RavenUserStore<TUser>>? logger = null)
        where TUser : RavenIdentityUser, new()
    {
        logger ??= new Mock<ILogger<RavenUserStore<TUser>>>().Object;
        return new RavenUserStore<TUser>(session, logger);
    }

    // Helper to verify compare/exchange state
    protected async Task<string?> GetEmailReservationAsync(IDocumentStore store, string normalizedEmail)
    {
        var key = $"emails/{normalizedEmail}";
        var result = await store.Operations.SendAsync(
            new GetCompareExchangeValueOperation<string>(key));
        return result?.Value;
    }

    // Helper to clean up compare/exchange between tests if needed
    protected async Task DeleteEmailReservationAsync(IDocumentStore store, string normalizedEmail)
    {
        var key = $"emails/{normalizedEmail}";
        var existing = await store.Operations.SendAsync(
            new GetCompareExchangeValueOperation<string>(key));
        if (existing != null)
        {
            await store.Operations.SendAsync(
                new DeleteCompareExchangeValueOperation<string>(key, existing.Index));
        }
    }

    protected class TestUser : RavenIdentityUser { }
}
```

**Test Naming Convention**: `MethodName_Scenario_ExpectedBehavior`
- Example: `CreateAsync_WithDuplicateEmail_ReturnsFailed`
- Example: `UpdateAsync_WithEmailChange_MigratesReservation`

**Async Pattern**: All tests marked `async Task`, use `await` consistently

**Cleanup**:
- Leverage RavenTestDriver's automatic store disposal (per-test databases isolate tests)
- Explicitly dispose sessions in test cleanup or using statements
- Use [CallerMemberName] in ALL tests calling GetTestDocumentStore to ensure unique database names
- Compare/exchange values are isolated per database, so no cross-test pollution if databases are unique

### Known Limitations to Document
1. **RavenRoleStore not tested** - stub implementation, all methods throw NotImplementedException
2. **IUserRoleStore not tested** - returns empty/false, awaiting RavenRoleStore implementation
3. **Static indexes not tested** - UseStaticIndexes=false in tests (dynamic queries only)
4. **ID generation conventions not tested** - configured directly on DocumentStore.Conventions by users, not part of RavenDbIdentityOptions
5. **AutoSaveChanges not implemented** - RavenDbIdentityOptions defines this property but RavenUserStore always calls SaveChangesAsync. Tests verify current behavior (always saves).

## Implementation Tasks

### Task 1: Create Test Project Infrastructure
**Files to Create**:
- `tests/RavenDB.AspNetCore.Identity.Tests/RavenDB.AspNetCore.Identity.Tests.csproj`
- `tests/RavenDB.AspNetCore.Identity.Tests/Infrastructure/RavenDbTestBase.cs`

**Actions**:
1. Create test project with PackageReferences:
   - RavenDB.TestDriver (7.2.0+) - MUST match RavenDB.Client major version
   - xUnit (2.9.0+)
   - xUnit.runner.visualstudio (2.8.0+)
   - FluentAssertions (7.0.0+)
   - Moq (4.20.0+)
   - Microsoft.NET.Test.Sdk (17.11.0+)
2. Add project reference to main library
3. Implement RavenDbTestBase with:
   - GetTestDocumentStore helper (uses [CallerMemberName] for unique database names)
   - CreateUserStore helper (accepts session parameter)
   - GetEmailReservationAsync helper (verifies compare/exchange state)
   - DeleteEmailReservationAsync helper (cleanup if needed)
   - TestUser class extending RavenIdentityUser

### Task 2: Test Email Uniqueness & Compare/Exchange
**File**: `tests/RavenDB.AspNetCore.Identity.Tests/Stores/RavenUserStoreEmailTests.cs`

**Test Cases**:
1. `CreateAsync_WithUniqueEmail_CreatesReservation`
2. `CreateAsync_WithDuplicateEmail_ReturnsFailed`
3. `CreateAsync_WithStoreFailure_RollsBackReservation`
4. `UpdateAsync_WithEmailChange_MigratesReservation`
5. `UpdateAsync_WithCaseOnlyChange_DoesNotMigrateReservation`
6. `UpdateAsync_WithDuplicateEmail_ReturnsFailedAndKeepsOldEmail`
7. `DeleteAsync_RemovesEmailReservation`
8. `FindByEmailAsync_BypassesIndexes_UsesCompareExchange`
9. `FindByEmailAsync_WithNonexistentEmail_ReturnsNull`

**Validation**: Each test verifies compare/exchange state using `GetCompareExchangeValueOperation`

### Task 3: Test User CRUD Operations
**File**: `tests/RavenDB.AspNetCore.Identity.Tests/Stores/RavenUserStoreTests.cs`

**Test Cases**:
1. `CreateAsync_WithValidUser_Succeeds`
2. `CreateAsync_WithNullEmail_ThrowsArgumentException`
3. `CreateAsync_WithEmptyEmail_ThrowsArgumentException`
4. `UpdateAsync_WithoutChanges_LogsWarningAndSucceeds`
5. `UpdateAsync_WithPropertyChanges_SavesChanges`
6. `DeleteAsync_WithValidUser_Succeeds`
7. `FindByIdAsync_WithExistingUser_ReturnsUser`
8. `FindByIdAsync_WithNonexistentId_ReturnsNull`
9. `FindByNameAsync_WithExistingUser_ReturnsUser`
10. `FindByNameAsync_WithNonexistentName_ReturnsNull`
11. `GetUserIdAsync_WithNullId_ThrowsInvalidOperationException`
12. `Dispose_SetsDisposedFlag`
13. `CreateAsync_AfterDispose_ThrowsObjectDisposedException`

**Note**: RavenUserStore currently always calls SaveChangesAsync (AutoSaveChanges option is defined but not implemented in the store).

### Task 4: Test Password Operations
**File**: `tests/RavenDB.AspNetCore.Identity.Tests/Stores/RavenUserStorePasswordTests.cs`

**Test Cases**:
1. `SetPasswordHashAsync_SetsHash`
2. `GetPasswordHashAsync_ReturnsHash`
3. `HasPasswordAsync_WithHash_ReturnsTrue`
4. `HasPasswordAsync_WithoutHash_ReturnsFalse`

### Task 5: Test Lockout Operations
**File**: `tests/RavenDB.AspNetCore.Identity.Tests/Stores/RavenUserStoreLockoutTests.cs`

**Test Cases**:
1. `SetLockoutEndDateAsync_SetsDate`
2. `GetLockoutEndDateAsync_ReturnsDate`
3. `IncrementAccessFailedCountAsync_IncrementsCounter`
4. `ResetAccessFailedCountAsync_ResetsToZero`
5. `GetAccessFailedCountAsync_ReturnsCount`
6. `SetLockoutEnabledAsync_SetsFlag`
7. `GetLockoutEnabledAsync_ReturnsFlag`

### Task 6: Test External Login Operations
**File**: `tests/RavenDB.AspNetCore.Identity.Tests/Stores/RavenUserStoreLoginTests.cs`

**Test Cases**:
1. `AddLoginAsync_AddsLogin`
2. `RemoveLoginAsync_RemovesLogin`
3. `GetLoginsAsync_ReturnsAllLogins`
4. `FindByLoginAsync_WithExistingLogin_ReturnsUser`
5. `FindByLoginAsync_WithNonexistentLogin_ReturnsNull`

### Task 7: Test Security & Phone Operations
**File**: `tests/RavenDB.AspNetCore.Identity.Tests/Stores/RavenUserStoreSecurityTests.cs`

**Test Cases**:
1. `SetSecurityStampAsync_SetsStamp`
2. `GetSecurityStampAsync_ReturnsStamp`

**File**: `tests/RavenDB.AspNetCore.Identity.Tests/Stores/RavenUserStorePhoneTests.cs`

**Test Cases**:
1. `SetPhoneNumberAsync_SetsNumber`
2. `GetPhoneNumberAsync_ReturnsNumber`
3. `SetPhoneNumberConfirmedAsync_SetsFlag`
4. `GetPhoneNumberConfirmedAsync_ReturnsFlag`

### Task 7b: Test Session Lifecycle Management
**File**: `tests/RavenDB.AspNetCore.Identity.Tests/Stores/RavenUserStoreSessionTests.cs`

**Test Cases**:
1. `CreateAsync_WithDisposedSession_ThrowsObjectDisposedException`
2. `FindByIdAsync_WithDisposedSession_ThrowsObjectDisposedException`
3. `MultipleOperations_OnSameSession_WorkCorrectly`
4. `CreateThenFind_OnSameSession_ReturnsUser`

**Note**: These tests verify proper session interaction patterns that are critical for RavenDB usage.

### Task 8: Test Value Objects & Models
**File**: `tests/RavenDB.AspNetCore.Identity.Tests/ValueObjects/NormalizedEmailTests.cs`

**Test Cases**:
1. `Constructor_WithValidEmail_NormalizesToLowercase`
2. `Constructor_WithWhitespace_TrimsAndNormalizes`
3. `Constructor_WithNullEmail_ThrowsArgumentException`
4. `Constructor_WithEmptyEmail_ThrowsArgumentException`
5. `Equals_WithSameEmailDifferentCase_ReturnsTrue`
6. `ImplicitConversion_FromString_Works`
7. `ImplicitConversion_ToString_Works`

**File**: `tests/RavenDB.AspNetCore.Identity.Tests/Models/RavenIdentityUserTests.cs`

**Test Cases**:
1. `EmailSetter_NormalizesToLowercase`
2. `EmailSetter_WithMixedCase_StoresLowercase`

### Task 9: Test DI Registration
**File**: `tests/RavenDB.AspNetCore.Identity.Tests/Extensions/IdentityBuilderExtensionsTests.cs`

**Test Cases (Unit tests, no RavenDB required)**:
1. `AddRavenDbIdentityStores_WithBothTypes_RegistersUserStore`
2. `AddRavenDbIdentityStores_WithBothTypes_RegistersRoleStore`
3. `AddRavenDbIdentityStores_UserOnly_RegistersOnlyUserStore`
4. `AddRavenDbIdentityStores_UserOnly_DoesNotRegisterRoleStore`
5. `AddRavenDbIdentityStores_WithOptions_ConfiguresUseStaticIndexes`
6. `AddRavenDbIdentityStores_WithOptions_ConfiguresAutoSaveChanges`

**Note**: These are unit tests verifying service registration. They should:
- Create a ServiceCollection
- Call AddIdentity<TestUser, TestRole>().AddRavenDbIdentityStores(...)
- Verify services are registered (ServiceDescriptor checks)
- NOT require actual RavenDB instances
- NOT inherit from RavenDbTestBase

### Task 10: Update Solution & Documentation
**Actions**:
1. Add test project to `RavenDB.AspNetCore.Identity.sln`
2. Update `CLAUDE.md` with test execution commands and structure
3. Create `.github/workflows/tests.yml` for CI (optional, out of scope if no .github exists)

## Execution Order
Tasks must be executed sequentially: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 7b → 8 → 9 → 10

Tasks 2-9 depend on Task 1 (test infrastructure).
Task 10 depends on all previous tasks completing.

## Success Criteria
1. All tests pass with `dotnet test`
2. Test coverage >80% for RavenUserStore (measured by lines, not required but target)
3. Critical compare/exchange email uniqueness scenarios covered
4. Session lifecycle edge cases tested
5. No flaky tests (all tests deterministic, unique database per test via [CallerMemberName])
6. Tests run in <30 seconds total
7. Clear test names following convention

## Out of Scope
- RavenRoleStore tests (unimplemented)
- IUserRoleStore tests (unimplemented)
- ID generation convention testing (configured on DocumentStore.Conventions by users, not library functionality)
- Performance/load testing
- Static index testing
- Multi-node cluster testing
- Middleware integration tests
- Full DI container integration tests (extension tests are unit-level only)