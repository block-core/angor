# Angor Investment Protocol Flows

This document describes the complete lifecycle of investments in Angor, covering the transaction actions and Nostr messaging between founders and investors.

---

## 1. Investment Flow

The investor creates an investment and coordinates with the founder to obtain recovery signatures before publishing the transaction on-chain.

| Step | Actor    | Action                                                                                         | Nostr Message                          |
|------|----------|------------------------------------------------------------------------------------------------|----------------------------------------|
| 1    | Investor | Calls `InvestorTransactionActions.CreateInvestmentTransaction` to build the investment tx       | —                                      |
| 2    | Investor | Sends the investment transaction to the founder via Nostr                                      | **Subject:** `"Investment offer"`      |
| 3    | Founder  | Calls `InvestorTransactionActions.BuildRecoverInvestorFundsTransaction` to build recovery tx    | —                                      |
| 4    | Founder  | Calls `FounderTransactionActions.SignInvestorRecoveryTransactions` to sign the recovery tx      | —                                      |
| 5    | Founder  | Sends the recovery signatures back to the investor via Nostr                                   | **Subject:** `"Re:Investment offer"`   |
| 6    | Investor | Calls `InvestorTransactionActions.CheckInvestorRecoverySignatures` to verify founder signatures | —                                      |
| 7    | Investor | If signatures are valid, signs and broadcasts the investment transaction on-chain               | **Subject:** `"Investment completed"`  |

### Below-Threshold Investments (No Penalty)

When the investment amount is below the penalty threshold (checked via `InvestorTransactionActions.IsInvestmentAbovePenaltyThreshold`), the investor does **not** need recovery signatures. The investor simply broadcasts the transaction and sends a notification to the founder.

| Step | Actor    | Action                                                                                   | Nostr Message                          |
|------|----------|------------------------------------------------------------------------------------------|----------------------------------------|
| 1    | Investor | Calls `InvestorTransactionActions.CreateInvestmentTransaction`                            | —                                      |
| 2    | Investor | Signs and broadcasts the investment transaction on-chain                                  | —                                      |
| 3    | Investor | Sends a completion notification to the founder                                            | **Subject:** `"Investment completed"`  |

### Investment Cancellation

If the investor decides not to proceed, they can cancel the investment request.

| Step | Actor    | Action                                          | Nostr Message                           |
|------|----------|--------------------------------------------------|-----------------------------------------|
| 1    | Investor | Sends a cancellation notification to the founder | **Subject:** `"Investment canceled"`   |

---

## 2. Investor Recovery (With Penalty)

After investing, the investor can recover their funds using the recovery signatures obtained from the founder. This path includes a penalty timelock — the investor must wait for the penalty period to expire before claiming the released funds.

| Step | Actor    | Action                                                                                                                      |
|------|----------|-----------------------------------------------------------------------------------------------------------------------------|
| 1    | Investor | Calls `InvestorTransactionActions.AddSignaturesToRecoverSeederFundsTransaction` using the founder's recovery signatures       |
| 2    | Investor | Broadcasts the signed recovery transaction on-chain                                                                          |
| 3    | Investor | **Waits** for the penalty timelock to expire (`PenaltyDays`)                                                                 |
| 4    | Investor | Calls `InvestorTransactionActions.BuildAndSignRecoverReleaseFundsTransaction` to spend the penalty-locked outputs             |
| 5    | Investor | Broadcasts the release transaction to receive their coins                                                                    |

---

## 3. Investor Recovery — No Penalty (End of Project)

When the investment is below the penalty threshold, or after the project has expired, the investor can recover remaining funds **without** a penalty timelock using the end-of-project script path.

| Step | Actor    | Action                                                                                                                      |
|------|----------|-----------------------------------------------------------------------------------------------------------------------------|
| 1    | Investor | Calls `InvestorTransactionActions.RecoverEndOfProjectFunds` to build and sign a transaction claiming unclaimed stage outputs  |
| 2    | Investor | Broadcasts the transaction to receive their coins immediately (no penalty wait)                                              |

---

## 4. Founder Releases Unfunded Coins to Investor

When a project does not meet its funding goals, the founder can release the investor's coins back. This requires a signature exchange similar to the initial investment flow.

| Step | Actor    | Action                                                                                                                       | Nostr Message                                      |
|------|----------|------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------|
| 1    | Founder  | Calls `InvestorTransactionActions.BuildUnfundedReleaseInvestorFundsTransaction` to build the release transaction               | —                                                   |
| 2    | Founder  | Calls `FounderTransactionActions.SignInvestorRecoveryTransactions` to generate signatures for the release transaction           | —                                                   |
| 3    | Founder  | Sends the release signatures to the investor via Nostr                                                                        | **Subject:** `"Release transaction signatures"`     |
| 4    | Investor | Calls `InvestorTransactionActions.CheckInvestorUnfundedReleaseSignatures` to verify the founder's release signatures           | —                                                   |
| 5    | Investor | Calls `InvestorTransactionActions.AddSignaturesToUnfundedReleaseFundsTransaction` to add both signatures to the transaction    | —                                                   |
| 6    | Investor | Broadcasts the signed transaction to claim the coins                                                                          | —                                                   |

---

## 5. Founder Claims a Stage

When a stage's timelock expires, the founder can spend the stage outputs from all investment transactions for that stage.

| Step | Actor   | Action                                                                                                                |
|------|---------|-----------------------------------------------------------------------------------------------------------------------|
| 1    | Founder | Calls `FounderTransactionActions.SpendFounderStage` with all investment transaction hex data for the expired stage      |
| 2    | Founder | Broadcasts the resulting transaction to claim the stage funds                                                           |

**Note:** For **Investment** projects, all investors share the same stage numbers. For **Fund/Subscribe** projects, stage release dates are dynamically calculated per investor based on their investment start date and the project's `DynamicStagePatterns`.

---

## Nostr Message Summary

All messages use Nostr kind `EncryptedDm` (NIP-04) with a `subject` tag to identify the message type.

| Subject Tag                           | Direction             | Purpose                                                        |
|---------------------------------------|-----------------------|----------------------------------------------------------------|
| `"Investment offer"`                  | Investor → Founder    | Sends the investment transaction requesting recovery signatures |
| `"Re:Investment offer"`               | Founder → Investor    | Returns recovery signatures for the investment                  |
| `"Investment completed"`              | Investor → Founder    | Notifies the founder that the investment was published on-chain |
| `"Investment canceled"`              | Investor → Founder    | Notifies the founder that the investment request is canceled   |
| `"Release transaction signatures"`    | Founder → Investor    | Sends release signatures for unfunded coin release              |

---

## Method Reference

### `InvestorTransactionActions`

| Method                                              | Description                                                                              |
|-----------------------------------------------------|------------------------------------------------------------------------------------------|
| `CreateInvestmentTransaction`                       | Builds the investment transaction with Taproot stage outputs                              |
| `BuildRecoverInvestorFundsTransaction`              | Builds the unsigned recovery transaction (used by founder to generate sigs)               |
| `BuildUnfundedReleaseInvestorFundsTransaction`      | Builds the unsigned release transaction for unfunded coin release                         |
| `BuildAndSignRecoverReleaseFundsTransaction`        | Spends penalty-locked recovery outputs after the timelock expires                         |
| `RecoverEndOfProjectFunds`                          | Recovers funds via the end-of-project script path (no penalty)                            |
| `RecoverRemainingFundsWithOutPenalty`                | Recovers remaining funds using seeder secrets (no penalty)                                |
| `AddSignaturesToRecoverSeederFundsTransaction`      | Adds founder + investor signatures to the recovery transaction                            |
| `AddSignaturesToUnfundedReleaseFundsTransaction`    | Adds founder + investor signatures to the unfunded release transaction                    |
| `CheckInvestorRecoverySignatures`                   | Verifies founder's recovery signatures are valid                                          |
| `CheckInvestorUnfundedReleaseSignatures`            | Verifies founder's unfunded release signatures are valid                                  |
| `IsInvestmentAbovePenaltyThreshold`                 | Checks if investment amount exceeds the penalty threshold                                 |
| `DiscoverUsedScript`                                | Identifies which script path was used to spend a stage output                             |

### `FounderTransactionActions`

| Method                            | Description                                                                                   |
|-----------------------------------|-----------------------------------------------------------------------------------------------|
| `SignInvestorRecoveryTransactions` | Signs the recovery/release transaction on behalf of the founder (Taproot Schnorr signatures)   |
| `SpendFounderStage`               | Spends all stage outputs for a given stage number after the timelock expires                    |
| `CreateNewProjectTransaction`     | Creates the on-chain project creation transaction with the Angor fee output                     |

### `SignService` (Nostr Communication)

| Method                               | Description                                                              |
|---------------------------------------|--------------------------------------------------------------------------|
| `RequestInvestmentSigs`               | Sends `"Investment offer"` DM from investor to founder                   |
| `NotifyInvestmentCompleted`           | Sends `"Investment completed"` DM from investor to founder               |
| `NotifyInvestmentCancelled`           | Sends `"Investment cancelled"` DM from investor to founder               |
| `SendSignaturesToInvestor`            | Sends `"Re:Investment offer"` DM from founder to investor                |
| `SendReleaseSigsToInvestor`           | Sends `"Release transaction signatures"` DM from founder to investor     |
| `LookupInvestmentRequestsAsync`       | Listens for incoming `"Investment offer"` messages                       |
| `LookupInvestmentNotificationsAsync`  | Listens for incoming `"Investment completed"` messages                   |
| `LookupInvestmentCancellationsAsync`  | Listens for incoming `"Investment cancelled"` messages                   |
| `LookupInvestmentRequestApprovals`    | Listens for outgoing `"Re:Investment offer"` messages                    |
| `LookupSignatureForInvestmentRequest` | Listens for founder's `"Re:Investment offer"` reply to a specific request|
| `LookupReleaseSigs`                   | Listens for `"Release transaction signatures"` for a specific request    |
| `LookupSignedReleaseSigs`             | Listens for all `"Release transaction signatures"` from a founder        |
| `LookupAllInvestmentMessagesAsync`    | Listens for all message types in a single subscription                   |
