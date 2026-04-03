# SDK-Level Test Proposals

This document describes new tests to add at the SDK level (`src/sdk/Angor.Sdk.Tests/`). These tests use real LiteDB but mock external dependencies (indexer, Nostr relays), so they can run in CI without any network infrastructure.

---

## 1. LiteDB Document Round-Trip Tests (HIGH PRIORITY)

**Why:** The `IGenericDocumentCollection<T>` implementation (`LiteDbGenericDocumentCollection`) has complex expression rewriting logic and wraps/unwraps entities in `Document<T>`. None of the CRUD operations are tested against real LiteDB except for one caching bug test.

**File:** `Angor.Sdk.Tests/Data/LiteDbDocumentRoundTripTests.cs`

### Test Setup

```csharp
public class LiteDbDocumentRoundTripTests : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly IGenericDocumentCollection<T> _collection;
    // Create a temp LiteDB in-memory or temp file
    // Wire up LiteDbDocumentCollection -> LiteDbGenericDocumentCollection
}
```

### Tests for Each Document Type

#### Project

```
- Project_InsertAndFindById_RoundTrips
- Project_InsertMultipleAndFindByIds_ReturnsAll
- Project_InsertAndFindAsync_WithPredicate_FiltersCorrectly
- Project_InsertAndDelete_RemovesDocument
- Project_Upsert_InsertsNew_ThenUpdatesExisting
```

#### DerivedProjectKeys

```
- DerivedProjectKeys_UpsertAndFindById_PreservesAllKeySlots
- DerivedProjectKeys_Upsert_OverwritesExistingKeys
- DerivedProjectKeys_FindById_WhenMissing_ReturnsNull
```

#### InvestmentRecordsDocument

```
- InvestmentRecords_UpsertWithMultipleRecords_PreservesAll
- InvestmentRecords_AddRecordThenRemove_UpdatesCorrectly
```

#### InvestmentHandshake (Composite Key)

```
- InvestmentHandshake_UpsertAndFindById_WithCompositeKey
- InvestmentHandshake_FindAsync_ByStatus_FiltersCorrectly
- InvestmentHandshake_FindAsync_ByProjectId_FiltersCorrectly
```

#### BoltzSwapDocument

```
- BoltzSwap_InsertAndFindById_RoundTrips
- BoltzSwap_UpdateStatus_PersistsChange
- BoltzSwap_Delete_RemovesDocument
```

#### WalletAccountBalanceInfo

```
- WalletAccountBalance_UpsertAndFindById_RoundTrips
- WalletAccountBalance_Delete_RemovesDocument
```

#### QueryTransaction / TransactionHexDocument

```
- QueryTransaction_InsertAndFindById_RoundTrips
- TransactionHex_InsertAndFindById_RoundTrips
```

### Cross-Cutting Tests

```
- DeleteAllAsync_ClearsAllDocuments
- CountAsync_WithNoPredicate_ReturnsTotal
- CountAsync_WithPredicate_ReturnsFilteredCount
- ExistsAsync_WhenExists_ReturnsTrue
- ExistsAsync_WhenMissing_ReturnsFalse
- FindByIdAsync_WithEmptyString_ReturnsFailureOrNull
- InsertAsync_WithDuplicateKey_HandlesGracefully
```

---

## 2. WalletFactory Integration Tests (HIGH PRIORITY)

**Why:** `WalletFactory.CreateWallet` is the core wallet lifecycle method. It persists to both the file store and LiteDB. Currently has zero test coverage.

**File:** `Angor.Sdk.Tests/Wallet/WalletFactoryIntegrationTests.cs`

### Test Setup

Uses real LiteDB (temp file), real `AesWalletEncryption`, an in-memory `IStore`, and real `DerivationOperations` / `WalletOperations` from `TestNetworkFixture`.

### Tests

```
CreateWallet_PersistsEncryptedWallet_ToStore
    - Arrange: Configure InMemoryStore, real WalletFactory
    - Act: CreateWallet("Test Wallet", seedwords, passphrase, "password123", Testnet)
    - Assert: IWalletStore.GetAll() returns 1 wallet with correct Id and Name

CreateWallet_PersistsDerivedProjectKeys_ToLiteDb
    - Act: CreateWallet(...)
    - Assert: IGenericDocumentCollection<DerivedProjectKeys>.FindByIdAsync(walletId) returns 15 key slots

CreateWallet_PersistsAccountBalanceInfo_ToLiteDb
    - Act: CreateWallet(...)
    - Assert: IGenericDocumentCollection<WalletAccountBalanceInfo>.FindByIdAsync(walletId) returns initialized balance

CreateWallet_SameSeedTwice_ProducesSameWalletId
    - Act: CreateWallet twice with same seed words
    - Assert: Both produce the same WalletId
    - Assert: wallets.json has TWO entries with same Id (documenting the bug)

CreateWallet_InvalidSeedPhrase_ReturnsFailure
    - Act: CreateWallet("", "invalid words here", ...)
    - Assert: Result.IsFailure with appropriate error message

CreateWallet_EmptySeedPhrase_ThrowsDomainException
    - Act: CreateWallet("", "", ...)
    - Assert: DomainException thrown from WalletDescriptorFactory

RebuildFounderKeys_UpsertsNewKeys_InLiteDb
    - Arrange: CreateWallet first (keys exist)
    - Act: RebuildFounderKeysAsync with same wallet words
    - Assert: DerivedProjectKeys still has 15 key slots (upserted, not duplicated)

CreateWallet_WithPassphrase_ProducesDifferentWalletId
    - Act: CreateWallet with same seed, no passphrase vs. with passphrase
    - Assert: WalletIds are different
```

---

## 3. DatabaseManagementService Tests (HIGH PRIORITY)

**Why:** `DeleteAllDataAsync` is the nuclear reset option. It operates on all 8 collections but is untested.

**File:** `Angor.Sdk.Tests/Funding/Services/DatabaseManagementServiceTests.cs`

### Tests

```
DeleteAllData_ClearsAllEightCollections
    - Arrange: Insert documents into all 8 collections
    - Act: DeleteAllDataAsync()
    - Assert: All 8 collections are empty (CountAsync == 0)

DeleteAllData_OnEmptyDatabase_Succeeds
    - Arrange: Empty DB
    - Act: DeleteAllDataAsync()
    - Assert: Result.IsSuccess, no errors

DeleteAllData_ReturnsCorrectDeleteCount
    - Arrange: Insert known counts into each collection
    - Act: DeleteAllDataAsync()
    - Assert: Log message shows correct total (verify via mock logger)
```

---

## 4. PortfolioService Integration Tests (MEDIUM PRIORITY)

**Why:** `PortfolioService` has a two-tier cache (local LiteDB + Nostr relay) but only the Nostr path is covered. The local cache path and the add/remove operations are untested.

**File:** `Angor.Sdk.Tests/Funding/Investor/Domain/PortfolioServiceIntegrationTests.cs`

### Test Setup

Real LiteDB, mocked `IRelayService`, mocked `ISeedwordsProvider` / `IEncryptionService`.

### Tests

```
GetByWalletId_WhenLocalExists_ReturnsWithoutRelayCall
    - Arrange: Pre-insert InvestmentRecordsDocument into LiteDB
    - Act: GetByWalletId(walletId)
    - Assert: Returns local data, relay NOT called (verify via Mock)

GetByWalletId_WhenLocalMissing_FetchesFromRelay_CachesLocally
    - Arrange: Empty LiteDB, relay returns encrypted investment records
    - Act: GetByWalletId(walletId)
    - Assert: Returns relay data AND caches to LiteDB

AddOrUpdate_InsertsNew_RecordToCollection
    - Arrange: Empty collection
    - Act: AddOrUpdate(walletId, newRecord)
    - Assert: LiteDB contains InvestmentRecordsDocument with 1 record

AddOrUpdate_UpdatesExisting_RecordInCollection
    - Arrange: Collection has 1 record for projectX
    - Act: AddOrUpdate(walletId, updatedRecordForProjectX)
    - Assert: Collection still has 1 record, but with updated data

AddOrUpdate_WithMultipleProjects_MaintainsList
    - Act: AddOrUpdate for project1, then project2, then project3
    - Assert: Collection has 3 records, all preserved

RemoveInvestmentRecord_RemovesFromLocal
    - Arrange: Collection has 2 records
    - Act: RemoveInvestmentRecordAsync(walletId, record1)
    - Assert: Collection has 1 record (record2 only)

RemoveInvestmentRecord_WhenNotFound_Succeeds
    - Act: RemoveInvestmentRecordAsync with non-existent record
    - Assert: Result.IsSuccess (nothing to remove)
```

---

## 5. DocumentProjectService Integration Tests (MEDIUM PRIORITY)

**Why:** The project caching layer in LiteDB is critical for performance and offline-ish behavior. The cache-hit vs cache-miss paths are untested.

**File:** `Angor.Sdk.Tests/Funding/Services/DocumentProjectServiceIntegrationTests.cs`

### Test Setup

Real LiteDB, mocked `IRelayService`, mocked `IAngorIndexerService`.

### Tests

```
GetAllAsync_CacheHit_ReturnsFromLiteDb_WithoutCallingIndexer
    - Arrange: Insert Project into LiteDB
    - Act: GetAllAsync(projectId)
    - Assert: Returns project from DB, indexer NOT called

GetAllAsync_CacheMiss_FetchesFromIndexerAndNostr_CachesResult
    - Arrange: Empty LiteDB, indexer returns ProjectData, relay returns ProjectInfo + Metadata
    - Act: GetAllAsync(projectId)
    - Assert: Returns project AND caches in LiteDB for next call

GetAllAsync_PartialCacheHit_FetchesMissingOnly
    - Arrange: LiteDB has project1, missing project2
    - Act: GetAllAsync(project1Id, project2Id)
    - Assert: Returns both, indexer called only for project2

TryGetAsync_WhenNotFound_ReturnsNone
    - Arrange: Empty LiteDB, indexer returns null
    - Act: TryGetAsync(unknownId)
    - Assert: Returns Maybe.None

GetAllAsync_WithNullIds_ReturnsFailure
    - Act: GetAllAsync(null)
    - Assert: Result.IsFailure with "ProjectId cannot be null"

GetAllAsync_CachedProject_PreservesAllFields
    - Arrange: Insert project with all fields populated (stages, URIs, dynamic patterns, etc.)
    - Act: FindByIdAsync
    - Assert: All fields match original (validates LiteDB serialization)
```

---

## 6. WalletAppService.DeleteWallet Tests (MEDIUM PRIORITY)

**Why:** Delete is a destructive operation with the known orphaned-data gap. Tests should document what gets cleaned and what doesn't.

**File:** `Angor.Sdk.Tests/Wallet/WalletAppServiceDeleteTests.cs`

### Tests

```
DeleteWallet_RemovesEncryptedWallet_FromStore
    - Arrange: CreateWallet, then DeleteWallet
    - Assert: IWalletStore.GetAll() is empty

DeleteWallet_RemovesAccountBalanceInfo_FromLiteDb
    - Assert: WalletAccountBalanceInfo collection no longer has entry

DeleteWallet_DoesNotRemoveDerivedProjectKeys_FromLiteDb
    - Assert: DerivedProjectKeys STILL exists (documents the gap)

DeleteWallet_DoesNotRemoveFounderProjectsDocument_FromLiteDb
    - Assert: FounderProjectsDocument STILL exists (documents the gap)

DeleteWallet_WhenWalletNotFound_ReturnsFailure
    - Act: DeleteWallet(unknownId)
    - Assert: Result.IsFailure("Wallet not found")

DeleteWallet_ClearsSensitiveDataFromMemory
    - Arrange: CreateWallet (sensitive data cached)
    - Act: DeleteWallet
    - Assert: ISensitiveWalletDataProvider no longer has cached data
```

---

## 7. ScanFounderProjects Tests (MEDIUM PRIORITY)

**Why:** The project scan after wallet import is untested. It involves derived keys, indexer queries, and DB persistence.

**File:** `Angor.Sdk.Tests/Funding/Founder/Operations/ScanFounderProjectsTests.cs`

### Tests

```
ScanFounderProjects_WhenNewProjectFound_AddsToFounderProjectsDocument
    - Arrange: DerivedProjectKeys with 15 slots, indexer returns 1 match
    - Act: Send ScanFounderProjectsRequest
    - Assert: FounderProjectsDocument updated with the found project

ScanFounderProjects_WhenAllProjectsAlreadyKnown_NoIndexerCalls
    - Arrange: All 15 slots already in FounderProjectsDocument
    - Act: Send request
    - Assert: Indexer not called (only local DB read)

ScanFounderProjects_WhenNoProjectsFound_ReturnsEmpty
    - Arrange: DerivedProjectKeys with 15 slots, indexer returns nothing
    - Act: Send request
    - Assert: Empty result, FounderProjectsDocument unchanged

ScanFounderProjects_WhenDerivedKeysNotFound_ReturnsFailure
    - Arrange: No DerivedProjectKeys in DB
    - Act: Send request
    - Assert: Result.IsFailure
```

---

## 8. AesWalletEncryption Round-Trip Tests (LOW PRIORITY)

**Why:** Encryption/decryption is critical but untested at the unit level.

**File:** `Angor.Sdk.Tests/Wallet/AesWalletEncryptionTests.cs`

### Tests

```
EncryptAndDecrypt_RoundTrip_PreservesData
    - Act: Encrypt WalletData, then Decrypt with same key
    - Assert: Decrypted data matches original

Decrypt_WithWrongKey_ReturnsFailureOrThrows
    - Act: Encrypt with key1, Decrypt with key2
    - Assert: Failure (not silent corruption)

Encrypt_ProducesUniqueSaltAndIV_EachTime
    - Act: Encrypt same data twice with same key
    - Assert: Salt and IV differ between the two encrypted results
```

---

## Implementation Priority

| Priority | Test Class | Effort | Dependencies |
|---|---|---|---|
| 1 | `LiteDbDocumentRoundTripTests` | Low | LiteDB only (temp file) |
| 2 | `DatabaseManagementServiceTests` | Low | LiteDB only |
| 3 | `WalletFactoryIntegrationTests` | Medium | LiteDB + InMemoryStore + TestNetworkFixture |
| 4 | `WalletAppServiceDeleteTests` | Medium | Same as above |
| 5 | `PortfolioServiceIntegrationTests` | Medium | LiteDB + Moq (relay, encryption) |
| 6 | `DocumentProjectServiceIntegrationTests` | Medium | LiteDB + Moq (relay, indexer) |
| 7 | `ScanFounderProjectsTests` | Medium | LiteDB + Moq (indexer, project service) |
| 8 | `AesWalletEncryptionTests` | Low | No dependencies |

All of these can run in CI without any network infrastructure. They use real LiteDB (temp files) and mock all external services.
