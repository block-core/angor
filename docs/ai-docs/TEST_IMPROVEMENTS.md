# Improvements to Existing Integration Tests

> **Last updated:** April 2026 -- re-analyzed after new E2E tests added.

This document describes specific improvements to the existing E2E integration tests in `src/design/App.Test.Integration/`.

**Current E2E test count: 9** (was 7).

---

## 1. SendToSelfTest

**Current coverage:** Wallet create (generate), faucet fund, send to self, verify balance change.

### Missing Assertions

#### 1.1 Database entries after wallet creation

After `CreateWallet` completes, resolve services from DI and assert:

```csharp
// Verify DerivedProjectKeys persisted with 15 founder key slots
var derivedKeysCollection = serviceProvider.GetRequiredService<IGenericDocumentCollection<DerivedProjectKeys>>();
var keysResult = await derivedKeysCollection.FindByIdAsync(walletId);
keysResult.IsSuccess.Should().BeTrue();
keysResult.Value.Should().NotBeNull();
keysResult.Value.Keys.Should().HaveCount(15);

// Verify WalletAccountBalanceInfo initialized in DB
var balanceCollection = serviceProvider.GetRequiredService<IGenericDocumentCollection<WalletAccountBalanceInfo>>();
var balanceResult = await balanceCollection.FindByIdAsync(walletId);
balanceResult.IsSuccess.Should().BeTrue();
balanceResult.Value.Should().NotBeNull();
```

> **Note:** The new `WalletImportAndProjectScanTest` validates some of these assertions (WalletId determinism, balance after import), but `SendToSelfTest` would benefit from explicit DB assertions at each step of its own flow.

#### 1.2 Transaction list after send

Navigate to the transaction history section and verify the send transaction:
- Appears in the list
- Shows the correct amount (0.0001 BTC)
- Shows the correct direction (outgoing)
- Has a valid TxId

#### 1.3 Wallet metadata persistence

```csharp
// Verify EncryptedWallet saved to store
var walletStore = serviceProvider.GetRequiredService<IWalletStore>();
var walletsResult = await walletStore.GetAll();
walletsResult.Value.Should().Contain(w => w.Id == walletId);
```

#### 1.4 Stronger UTXO reservation assertion

While the send transaction is pending, assert specific values:

```csharp
// While pending:
availableSats.Should().BeLessThan(totalBalanceSats, "spent UTXO should be reserved");
// After confirmation:
availableSats.Should().Be(totalBalanceSats, "reservation should be released after confirmation");
```

---

## 2. CreateProjectTest

**Current coverage:** Creates an Invest-type project through the 6-step wizard, deploys, validates `ProjectDto` fields via SDK.

### Missing Assertions

#### 2.1 FounderProjectsDocument in DB

```csharp
// After deploy, verify the founder project record was persisted
var founderProjectsCollection = serviceProvider.GetRequiredService<IGenericDocumentCollection<FounderProjectsDocument>>();
var docResult = await founderProjectsCollection.FindByIdAsync(walletId);
docResult.IsSuccess.Should().BeTrue();
docResult.Value.Projects.Should().Contain(p => p.ProjectIdentifier == projectIdentifier);
docResult.Value.Projects.First().CreationTransactionId.Should().NotBeNullOrEmpty();
```

> **Note:** The new `WalletImportAndProjectScanTest` validates that `FounderProjectsDocument` is populated after project scan, but `CreateProjectTest` should validate it is populated immediately after deploy.

#### 2.2 DerivedProjectKeys slot consumed

```csharp
// Verify one of the 15 derived key slots matches the project
var keysResult = await derivedKeysCollection.FindByIdAsync(walletId);
keysResult.Value.Keys.Should().Contain(k => k.ProjectIdentifier == projectIdentifier);
```

#### 2.3 Project appears in browse/discovery

After deploy, navigate to "Find Projects" and verify the project appears. This validates the full Nostr publish -> indexer round-trip -> DB cache pipeline.

#### 2.4 Create a second project

After the first project is created, create a second one and verify:
- It uses a different derived key slot (different index)
- Both projects appear in "My Projects"
- Both `FounderProjectRecord` entries exist in `FounderProjectsDocument`

---

## 3. FundAndRecoverTest

**Current coverage:** Fund-type project, invest above threshold, founder approves, investor confirms, founder spends stage 1, investor recovers remaining.

### Missing Assertions

#### 3.1 InvestmentRecordsDocument after investing

```csharp
// After investor confirms, verify the investment record in DB
var investmentRecordsCollection = serviceProvider.GetRequiredService<IGenericDocumentCollection<InvestmentRecordsDocument>>();
var recordResult = await investmentRecordsCollection.FindByIdAsync(investorWalletId);
recordResult.IsSuccess.Should().BeTrue();
var record = recordResult.Value.Investments.First();
record.ProjectIdentifier.Should().Be(projectIdentifier);
record.InvestmentTransactionHash.Should().NotBeNullOrEmpty();
record.InvestedAmountSats.Should().Be(investedAmount);
```

> **Note:** The new `InvestmentCancellationTest` validates portfolio/investment record state during cancel/re-invest cycles, but `FundAndRecoverTest` would benefit from explicit DB assertions at the investment and recovery stages.

#### 3.2 InvestmentHandshake status progression

At each stage of the handshake, verify the `InvestmentHandshake` document in the DB:

```csharp
// After request: Status = Pending
// After approval: Status = Approved, ApprovalEventId not null
// After confirm: Status = Invested, InvestmentTransactionId not null
```

> **Note:** The new `InvestmentCancellationTest` validates the handshake status becomes "Cancelled" and that re-invested handshakes progress through "Pending Approval" -> "Approved" -> confirmed, but explicit handshake status assertions at each intermediate step are still missing from the simpler `FundAndRecoverTest`.

#### 3.3 Exact balance deltas

Instead of just checking "balance changed", compute and assert exact satoshi deltas:

```csharp
// Founder after stage 1 spend:
founderBalanceAfter.Should().BeApproximately(founderBalanceBefore + stage1Amount - fees, tolerance);

// Investor after recovery:
investorBalanceAfter.Should().BeApproximately(investorBalanceBefore + investedAmount - stage1Amount - penaltyFee - txFees, tolerance);
```

#### 3.4 Recovery amount verification

When the investor recovers with penalty, assert the recovered amount against the expected calculation:

```csharp
recoveredAmount.Should().Be(investedAmount - stage1Claimed - penaltyDeduction);
```

---

## 4. MultiFundClaimAndRecoverTest

**Current coverage:** 3 profiles (Founder + below-threshold + above-threshold investor), PenaltyDays=0.

### Key Issue: PenaltyDays=0 Bypasses Penalty Logic

The test uses `PenaltyDays = 0`, which means the penalty time-lock is effectively zero. The penalty recovery path executes but the actual penalty *calculation* (proportional to remaining lock time) is never tested.

### Missing Assertions

#### 4.1 Cross-profile DB validation

After each profile performs an action, switch to the other profile and verify:

```csharp
// After Investor1 invests: switch to Founder, verify InvestmentHandshake appears
using (new TestProfileScope("founder"))
{
    var handshakes = await handshakeCollection.FindAsync(h => h.ProjectId == projectId);
    handshakes.Value.Should().Contain(h => h.InvestorNostrPubKey == investor1NostrPubKey);
}
```

#### 4.2 Threshold boundary validation

Assert that below-threshold investments are auto-approved (no founder action needed) vs above-threshold requiring explicit approval:

```csharp
// Below threshold: handshake goes directly to Approved/Invested without founder action
belowThresholdHandshake.IsDirectInvestment.Should().BeTrue();

// Above threshold: handshake requires founder approval
aboveThresholdHandshake.IsDirectInvestment.Should().BeFalse();
aboveThresholdHandshake.ApprovalEventId.Should().NotBeNullOrEmpty();
```

> **Note:** The new `InvestmentCancellationTest` validates `IsAutoApproved` is false for above-threshold investments, but below-threshold auto-approval assertion is still only partially covered.

---

## 5. MultiInvestClaimAndRecoverTest

**Current coverage:** 3 profiles, Invest-type project, stages in past, founder claims stage 1, releases remaining, investors claim.

### Missing Assertions

#### 5.1 Per-investor balance accounting

After the founder releases remaining stages and both investors claim:

```csharp
// Each investor's claim should be proportional to their investment
investor1Claimed.Should().BeApproximately(investor1Invested * releasedRatio, tolerance);
investor2Claimed.Should().BeApproximately(investor2Invested * releasedRatio, tolerance);
```

#### 5.2 Final DB state

At the end of the test, verify the complete state across all three profiles:

```csharp
// Founder: FounderProjectsDocument with project, all handshakes completed
// Investor1: InvestmentRecordsDocument with EndOfProjectTransactionId set
// Investor2: InvestmentRecordsDocument with EndOfProjectTransactionId set
```

---

## 6. WalletImportAndProjectScanTest (NEW)

**Current coverage:** Import wallet from seed, verify same WalletId, scan projects, verify balance. ~20+ assertions including DB and balance checks.

### Already Covered
- WalletId determinism (imported == created)
- Balance discovery after import (UTXO refresh)
- FounderProjectsDocument populated after ScanFounderProjects
- Project name and identifier match across profiles

### Missing Assertions

#### 6.1 DerivedProjectKeys comparison

```csharp
// Verify the imported wallet's DerivedProjectKeys match the creator's exactly
var importedKeys = await derivedKeysCollection.FindByIdAsync(importedWalletId);
importedKeys.Value.Keys.Should().BeEquivalentTo(creatorKeys.Value.Keys);
```

#### 6.2 InvestmentRecordsDocument after import

If the creator had investments, verify they are discoverable after import and scan.

---

## 7. InvestmentCancellationTest (NEW)

**Current coverage:** 8-phase E2E with ~40+ assertions. Validates cancel before approval, cancel after approval, re-invest, and confirm.

### Already Covered
- Cancel before founder approval (status becomes "Cancelled", balance released)
- Cancel after founder approval (status becomes "Cancelled", balance released)
- Re-invest after cancellation succeeds
- Confirm after approval reaches Step 3 (active)
- Portfolio investment records updated at each stage

### Missing Assertions

#### 7.1 Founder-side cancellation visibility

After investor cancels, verify the founder's view:

```csharp
// Switch to founder profile, verify cancelled investment no longer appears as pending
var pendingRequests = await investmentRequestsCollection.FindAsync(r => r.Status != "Cancelled");
pendingRequests.Should().NotContain(r => r.InvestorId == cancelledInvestorId);
```

#### 7.2 Nostr DM cancellation notification

Verify the `NotifyFounderOfCancellation` MediatR request was dispatched (this is covered by `CancelInvestmentRequestTests` at the unit level but not verified E2E).

---

## 8. General Improvements for All Tests

### 8.1 Add DB assertion helper methods

Create shared helper methods in `TestHelpers.cs`:

```csharp
public static async Task<T> GetDocumentFromDb<T>(IServiceProvider sp, string id) where T : class
{
    var collection = sp.GetRequiredService<IGenericDocumentCollection<T>>();
    var result = await collection.FindByIdAsync(id);
    result.IsSuccess.Should().BeTrue($"Expected document {typeof(T).Name} with id {id} to exist");
    return result.Value!;
}

public static async Task AssertDocumentExists<T>(IServiceProvider sp, string id) where T : class
{
    var collection = sp.GetRequiredService<IGenericDocumentCollection<T>>();
    var result = await collection.ExistsAsync(id);
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().BeTrue($"Expected document {typeof(T).Name} with id {id} to exist");
}

public static async Task AssertDocumentNotExists<T>(IServiceProvider sp, string id) where T : class
{
    var collection = sp.GetRequiredService<IGenericDocumentCollection<T>>();
    var result = await collection.ExistsAsync(id);
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().BeFalse($"Expected document {typeof(T).Name} with id {id} to NOT exist");
}
```

### 8.2 Add balance snapshot helpers

```csharp
public static async Task<long> GetBalanceSats(IServiceProvider sp, string walletId)
{
    var collection = sp.GetRequiredService<IGenericDocumentCollection<WalletAccountBalanceInfo>>();
    var result = await collection.FindByIdAsync(walletId);
    return result.Value?.AccountBalanceInfo?.TotalBalance ?? 0;
}
```

### 8.3 Log DB state at end of each test

For debugging flaky tests, log the full DB state at the end:

```csharp
[AfterTest]
private async Task DumpDbState()
{
    var projects = await projectsCollection.FindAllAsync();
    output.WriteLine($"Projects in DB: {projects.Value.Count()}");
    // ... for each collection
}
```
