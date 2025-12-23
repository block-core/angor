# Method Naming Consistency Review

## Angor Investment Flow Method Mapping

### Flow Step → Method Mapping

| Step | Description | Interface | Actual Method(s) |
|------|-------------|-----------|------------------|
| **1** | Founder creates project (invest/fund/subscribe) | `IProjectAppService`, `IFounderAppService` | `CreateNewProjectKeysAsync()` → `CreateProjectProfile()` → `CreateProjectInfo()` → `CreateProject()` |
| **2** | Investor invests in project | `IInvestmentAppService` | `CreateInvestmentDraft()` |
| **3** | Below threshold - no penalty needed | `IInvestmentAppService` | `IsInvestmentAbovePenaltyThreshold()` → `ConfirmInvestment()` (direct publish) |
| **4** | Above threshold - request via Nostr | `IInvestmentAppService` | `IsInvestmentAbovePenaltyThreshold()` → `SubmitInvestment()` |
| **4b** | Investor cancels pending investment | `IInvestmentAppService` | `CancelInvestment()` |
| **5** | Founder lists & approves requests | `IFounderAppService` | `GetInvestments()` → `ApproveInvestment()` |
| **6** | Investor asks for approved projects | `IInvestmentAppService` | `GetInvestorProjects()` |
| **7** | Investor publishes approved investment | `IInvestmentAppService` | `ConfirmInvestment()` |
| **8** | Founder gets list of all investments | `IFounderAppService`, `IProjectInvestmentsService` | `GetClaimableTransactions()` / `ScanFullInvestments()` |
| **9** | Founder spends unlocked stage UTXOs | `IFounderAppService` | `GetClaimableTransactions()` → `Spend()` → `SubmitTransactionFromDraft()` |
| **10** | Investor claims back funds | `IInvestmentAppService` | `GetInvestorProjectRecovery()` |
| **11** | Below threshold - claim immediately | `IInvestmentAppService` | `BuildReleaseInvestorFunds()` → `SubmitTransactionFromDraft()` |
| **12** | Above threshold - penalty transaction | `IInvestmentAppService` | `BuildRecoverInvestorFunds()` → `SubmitTransactionFromDraft()` |
| **13** | After penalty timelock - claim | `IInvestmentAppService` | `GetPenalties()` → `BuildClaimInvestorEndOfProjectFunds()` → `SubmitTransactionFromDraft()` |
| **14** | Founder releases funds to investor | `IFounderAppService` | `GetReleasableTransactions()` → `ReleaseInvestorTransactions()` |

---

## High Priority Issues

| Current Name | Issue | Proposed Name |
|--------------|-------|---------------|
| `GetReleasableTransactions` | Misspelled ("Releaseable" in type) | Fix typo: `GetReleaseableTransactionsRequest` → `GetReleasableTransactionsRequest` |
| `SubmitInvestment` | Confusing - actually requests signatures, not publishes | `RequestInvestmentApproval` |
| `ConfirmInvestment` | Actually publishes to blockchain | `PublishInvestment` |
| `SubmitTransactionFromDraft` (both interfaces) | "Submit" is vague | `PublishTransaction` |

---

## Medium Priority Issues

| Current Name | Issue | Proposed Name |
|--------------|-------|---------------|
| `Latest` | Missing verb | `GetLatestProjects` |
| `Spend` | Missing noun, too generic | `SpendStageFunds` or `ClaimStageFunds` |
| `CreateInvestmentDraft` | Inconsistent with `Build*` pattern | `BuildInvestmentDraft` (align with other Build methods) |
| `BuildRecoverInvestorFunds` | "Recover" is verb, should match pattern | `BuildRecoveryTransaction` |
| `BuildReleaseInvestorFunds` | Redundant "Investor" (already in interface) | `BuildReleaseTransaction` |
| `BuildClaimInvestorEndOfProjectFunds` | Too long, redundant role | `BuildEndOfProjectClaim` |

---

## Proposed Consistent Naming Conventions

**1. Verb Categories:**
- `Get*` - Query/retrieve data
- `Build*` - Create transaction drafts (not yet published)
- `Publish*` - Broadcast to blockchain
- `Request*` - Send request to another party (Nostr)
- `Approve*` / `Cancel*` - State change actions
- `Scan*` / `Check*` - Blockchain state queries

**2. Remove redundant role prefixes** when method is already in a role-specific interface:
- `IInvestmentAppService.GetInvestorProjects` → `GetProjects`
- `IInvestmentAppService.GetInvestorProjectRecovery` → `GetProjectRecovery`
- `IFounderAppService.ReleaseInvestorTransactions` → `ReleaseTransactions`

**3. Use Request/Response DTO pattern consistently** across all interfaces (IProjectAppService currently mixes styles)

---

## Full Proposed Rename Table (Flow Order)

| Step | Current | Proposed | Rationale |
|------|---------|----------|-----------|
| 1 | `CreateNewProjectKeysAsync` | `CreateProjectKeys` | Remove "New", drop Async suffix (C# convention) |
| 1 | `CreateProjectProfile` | ✓ Keep | |
| 1 | `CreateProjectInfo` | ✓ Keep | |
| 1 | `CreateProject` | ✓ Keep | |
| 2 | `CreateInvestmentDraft` | `BuildInvestmentDraft` | Align with other Build* methods |
| 3 | `IsInvestmentAbovePenaltyThreshold` | `CheckPenaltyThreshold` | Consistent with Check* pattern |
| 4 | `SubmitInvestment` | `RequestInvestmentApproval` | Clarifies it's a Nostr request |
| 4b | `CancelInvestment` | `CancelInvestmentRequest` | Clarifies what's being cancelled |
| 5 | `GetInvestments` | `GetPendingApprovals` | Clarifies these are pending requests |
| 5 | `ApproveInvestment` | ✓ Keep | |
| 6 | `GetInvestorProjects` | `GetInvestments` | Simpler, role is in interface |
| 7 | `ConfirmInvestment` | `PublishInvestment` | Clarifies blockchain action |
| 8 | `GetClaimableTransactions` | ✓ Keep | |
| 8 | `ScanFullInvestments` | `ScanInvestments` | Remove "Full" |
| 9 | `Spend` | `SpendStageFunds` | Add missing noun |
| 9 | `SubmitTransactionFromDraft` | `PublishTransaction` | Simpler, clearer |
| 10 | `GetInvestorProjectRecovery` | `GetRecoveryStatus` | Simpler |
| 11 | `BuildReleaseInvestorFunds` | `BuildReleaseTransaction` | Remove redundant role |
| 12 | `BuildRecoverInvestorFunds` | `BuildRecoveryTransaction` | Noun form consistency |
| 13 | `GetPenalties` | ✓ Keep | |
| 13 | `BuildClaimInvestorEndOfProjectFunds` | `BuildPenaltyClaim` | Shorter, clearer |
| 14 | `GetReleasableTransactions` | `GetReleasableTransactions` | Just fix typo in DTO |
| 14 | `ReleaseInvestorTransactions` | `ReleaseFunds` | Shorter |

---

## Open Questions

1. **Breaking changes**: These renames would be breaking changes. Do you want a migration strategy (e.g., add new methods with old as `[Obsolete]`)?

2. **Request/Response DTO alignment**: `IProjectAppService` uses direct parameters while others use DTOs. Should we align all to the DTO pattern?

3. **"Build" vs "Create" decision**: Proposed aligning on `Build*` for transaction drafts. Do you prefer `Create*Draft` instead for all?
