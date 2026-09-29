# Angor App — Manual Test Guide

This document is the manual (human-driven) equivalent of the automated UAT test suite in
`src/design/App.Test.Uat/`. Use it when you don't have a dev environment to run the automated
tests, or when you want a human to sanity-check the same flows visually in the real UI.

Each section below mirrors one automated test file (named in the heading) so that if a manual
test fails, a developer can find/re-run the matching automated test for a deeper investigation.

> All tests run against **Angornet** (Angor's signet-based test network), never Mainnet, unless a
> step explicitly says otherwise. Never use real funds for these tests.

## General setup (do this once per test run)

1. Install/launch the Angor App (Desktop build of `src/design/App.Desktop`).
2. Go to **Settings**:
   - If you want a totally clean start, use **Wipe Data** (and tick **Purge recovery files** if you
     also want previously-created test wallets forgotten).
   - Set **Network** to **Angornet**.
   - Enable **Debug Mode** (several test-only features — auto-approve toggle, faucet button —
     only appear in debug mode).
3. Create a wallet: **Funds** → **Add Wallet** → **Generate** → confirm. Write down the seed
   words shown (you'll need them for recovery tests).
4. Fund the wallet: on the wallet screen use the **Get Test Coins** (faucet) button. Wait for the
   balance to update (may take up to a minute).

For multi-user tests (founder + one or more investors) you need **separate app
instances/profiles** running at the same time — e.g. install the app on a second device, run a
second user profile/VM, or use separate app data directories if the build supports a profile
switch. Treat each "user" in the steps below as its own wallet + its own app session.

---

## 1. Create & Edit Project (`CreateProjectTest`)

**Purpose:** Founder can create Investment and Fund-type projects, upload images, and edit the
project profile with changes reflected publicly.

**Preconditions:** Funded wallet, internet access.

**Steps:**
1. Go to **My Projects** → **Create Project**.
2. Choose type **Investment**. Enter a name and "about" text. For images, either upload a photo
   or paste an image URL for both banner and profile picture. Set a target amount (e.g. 1 BTC)
   and an end date. Let the wizard generate a 3-month funding stage schedule. Click **Deploy**.
   - ✅ Verify the project appears in **My Projects** with type "Investment" after deployment.
3. Repeat, this time choosing type **Fund**, frequency **Monthly**, 6 installments, target 1 BTC,
   threshold 0.01 BTC, payout day = today's day of month. Deploy.
   - ✅ Verify it appears with type "Fund" / Monthly.
4. Repeat again with frequency **Weekly**, 3 installments, target 0.5 BTC, threshold 0.005 BTC,
   payout day = today's weekday. Deploy.
   - ✅ Verify it appears with type "Fund" / Weekly.
5. Open the **Investment** project from step 2 → **Edit Profile**. Upload a new banner image and
   a new profile image (these upload to the Blossom image host) — wait for each upload to
   complete and show a preview.
6. Edit the name, about text, website, and markdown description. Save.
7. Wait ~10-15 seconds (Nostr relay propagation), then close and reopen the project's public
   profile page.
   - ✅ Verify every field you changed (name, about, picture, banner, website, description)
     shows the new values, not the old ones.

---

## 2. Fund Project Full Lifecycle (`MultiFundClaimAndRecoverTest`)

**Purpose:** Exercise a Fund-type project across auto-approval, manual approval, staged
claiming, and all recovery types.

**Preconditions:** 1 founder + at least 2 investor sessions (4 recommended), funded wallets.

**Steps:**
1. Founder: create a **Fund** project — Monthly, 6 installments, target 1 BTC, threshold
   0.01 BTC, penalty days 0, payout day = today, start date = yesterday. Deploy.
2. Investor A: invest a small amount **below the threshold** (e.g. 0.001 BTC), choosing the
   6-stage funding pattern.
   - ✅ Verify this is **auto-approved** immediately (no founder action needed) — check
     **Funded** tab shows it as approved/active without visiting the Funders screen.
3. Investor B: invest another below-threshold amount (e.g. 0.002 BTC) with a 3-stage pattern.
   - ✅ Also auto-approved.
4. Investor C: invest **above the threshold** (e.g. 0.02 BTC), 3-stage pattern.
5. Investor D: invest above threshold (e.g. 0.03 BTC), 6-stage pattern.
   - ✅ Verify C and D show as **"waiting for approval"**.
6. Founder: go to **Funders**, filter by "waiting" — approve both C's and D's requests.
7. Investors C and D: go to **Funded** → **Manage** → **Confirm Investment**.
   - ✅ Verify each reaches the "active" state (Step 3).
8. Founder: **My Projects** → project → **Manage** → **Claim Stage 1**.
   - ✅ Verify the claim screen lists 6 stages and shows 4 claimable UTXOs (one per investor).
   - Click claim, confirm.
   - ✅ Success modal appears.
9. Investor C: **Funded** → **Manage** — wait for status to change to "recovery" → click
   **Recover Funds** → **Confirm Recovery**.
   - ✅ Succeeds, investment shows "In Penalty".
10. Investor C: open the **Penalties** popup (button on Funded tab).
    - ✅ Verify it lists exactly 1 penalty entry, correct project name, 0 days remaining, status
      "Penalty release available now". Close popup.
11. Investor C: wait for status "penaltyRelease" → **Recover Funds** → **Confirm Release**.
    - ✅ Succeeds.
12. Founder: **Manage** → **Release Funds** (releases remaining unclaimed stages).
13. Investor D: wait for status "unfundedRelease" → **Recover Funds** → **Confirm Release**.
    - ✅ Succeeds.
14. Investors A and B: wait for status "belowThreshold" → **Recover Funds** → **Confirm
    Recovery**.
    - ✅ Succeeds for both.
15. Founder: reopen the **Claim** view for the project.
    - ✅ Verify it still loads without error, still shows 6 stages, and no rows are missing/blank
      even though investors have since spent their own UTXOs.

---

## 3. Investment Project Full Lifecycle (`MultiInvestClaimAndRecoverTest`)

**Purpose:** Exercise an Investment-type project: cancel-before-approval, cancel-after-approval
+ reinvest, auto-approve toggle, claim, release, recovery.

**Preconditions:** 1 founder + 4 investor sessions.

**Steps:**
1. Founder: deploy a default **Investment**-type project.
2. Investor A: invest 0.02 BTC, then immediately click **Cancel Investment** (the "before
   approval" cancel button) — then invest 0.02 BTC again (don't confirm yet).
3. Investor B: invest 0.02 BTC. Founder: approve just Investor B's request on **Funders**.
   Investor B: click **Cancel Investment** (the "after approval" cancel button) — then invest
   0.02 BTC again.
4. Investor C: invest 0.02 BTC normally.
5. Investor D: invest 0.03 BTC normally.
6. Founder: go to **Funders** → toggle on **Auto-Approve** (only visible in Debug Mode).
   - ✅ Wait a short while and verify all 4 outstanding investment requests get approved
     automatically without clicking anything else.
7. All 4 investors: **Funded** → **Manage** → **Confirm Investment**.
   - ✅ Verify each reaches "active" (Step 3).
8. Founder: **Claim Stage 1**.
   - ✅ Verify 4 claimable UTXOs shown, claim succeeds.
9. Founder: **Release Funds**.
10. All 4 investors, one at a time: **Recover Funds** ("unfundedRelease") → **Confirm Release**.
    - ✅ Each succeeds.
11. Founder: reopen **Claim** view.
    - ✅ Verify it still loads correctly with a non-empty stage list and no missing UTXO rows.

---

## 4. One-Click Invoice Payments (`OneClickInvestInvoiceTest`)

**Purpose:** Verify paying via a generated invoice (on-chain address or Lightning BOLT11) works
for both deploying a project and investing, and that a wallet is auto-created the first time it's
needed.

**Preconditions:** A Lightning wallet/app (e.g. a phone Lightning wallet or ThunderHub access)
capable of paying a BOLT11 invoice. **Do not pre-create wallets** for founder or investor — the
point of this test is that the app creates them automatically.

**Steps:**
1. Launch a fresh Founder app session (no wallet) and a fresh Investor app session (no wallet).
   Wipe data, switch to Angornet, enable Debug Mode on both.
2. Founder: **My Projects** → **Create Project** (Investment type). At the deploy-payment step,
   choose **On-chain**. An address is shown.
   - Pay that address using the faucet.
   - ✅ Wait up to 5 minutes; verify the project deploy completes once payment is detected, and
     that a wallet now exists for this session.
3. Founder: create a second project (Fund type, Monthly, 3 installments). Choose **Lightning** as
   the deploy-payment method.
   - ✅ Verify a BOLT11 invoice is shown (starts with "ln...").
   - Pay it with your Lightning wallet.
   - ✅ Verify deploy completes once payment is detected.
4. Investor: open the Fund project from step 3, invest 0.001 BTC, choose **Lightning**.
   - ✅ Verify a wallet is auto-created for the investor session and a BOLT11 invoice is shown.
   - Pay it with your Lightning wallet.
   - ✅ Verify the investment succeeds once payment is detected.
5. Investor: open the Investment project from step 2, invest 0.001 BTC, choose **On-chain**.
   - ✅ Verify an address is shown; pay via faucet; verify investment succeeds.

---

## 5. Wallet Send/Receive Stress Test (`SendFundsTest`)

**Purpose:** Verify sending/receiving between wallets works reliably, balances never exceed the
funded total, and "Send All" sweeps a wallet to zero.

**Preconditions:** 3 funded wallets/sessions (User A, B, C).

**Steps:**
1. Fund all 3 wallets via faucet; wait until balances stop changing. Note each balance and the
   combined total.
2. Repeat 5 times:
   - Get a fresh receive address for each user.
   - Send 0.005 BTC: A→B, B→C, C→A (use a manual fee rate around 2 sats/vB if the option is
     available).
   - ✅ Verify each send reports success with a transaction ID.
   - Wait ~10 seconds, refresh balances.
   - ✅ Verify the sum of all 3 balances never exceeds the original combined total (only
     transaction fees should ever be lost).
3. After all rounds, verify all 3 users still show a positive balance.
4. **Sweep test:** User C sends their **entire** balance to User A using the **Send All / 100%**
   button (don't type a manual amount) at a low fee rate.
   - ✅ Verify User C's balance drops to exactly 0.
   - ✅ Verify User A's balance increases by C's swept amount (minus network fee).

---

## 6. Settings — Theme, Backup, Network Switch (`SettingsTest`)

**Purpose:** Verify theme toggle persistence, seed word backup/reveal, and network switching
doesn't break the project list or lose the wallet.

**Preconditions:** A wallet (doesn't need funding).

**Steps:**
1. Create a wallet, write down the seed words shown at creation time.
2. Go to **Settings**. Toggle **Dark Theme** on/off.
   - ✅ Verify the UI switches theme immediately, and the toggle stays in the new position after
     leaving and re-entering Settings.
   - Toggle back to the original value.
3. In Settings, click **Reveal Seed** (or "Backup Account").
   - ✅ Verify the seed words shown exactly match those captured in step 1.
   - ✅ Verify a **Download Seed** option becomes available.
   - Click **Reveal** again to hide the words.
4. Go to **Find Projects** while on Angornet; wait until at least one project loads (up to 2
   minutes).
5. Go to **Settings** → switch **Network** to **Mainnet**.
   - ✅ Verify the network label updates to "Mainnet".
   - Go to **Find Projects** again.
   - ✅ Verify the list reloads with Mainnet projects (not the old Angornet list).
6. Switch network back to **Angornet**.
   - ✅ Verify the label updates back and **Find Projects** reloads Angornet projects again.
7. Go to **Funds**.
   - ✅ Verify the wallet created in step 1 still exists after switching networks twice.

---

## 7. Wallet & Investment Recovery from Seed Words (`WalletRecoveryTest`)

**Purpose:** Verify a wiped app can fully recover a wallet from seed words, and that an active
investment automatically reappears from network data (no manual re-entry).

**Preconditions:** Founder + Investor sessions, funded wallets.

**Steps:**
1. Founder: create/fund a wallet, note the seed words and wallet ID (visible on wallet details).
2. Founder: deploy an Investment-type project.
3. Investor: create/fund a wallet, note its seed words and wallet ID.
4. Investor: invest 0.02 BTC in the founder's project.
5. Founder: **Funders** → approve the investor's request.
6. Investor: **Funded** → **Manage** → **Confirm Investment**.
   - ✅ Verify it reaches "active" (Step 3).
7. Founder: go to **Settings** → **Wipe Data** (do NOT purge recovery files). Switch to Angornet.
   Then **Funds** → **Add Wallet** → **Import** → paste the founder's seed words.
   - ✅ Verify the restored wallet's ID exactly matches the wallet ID noted in step 1.
8. Investor: same wipe + import process using the investor's seed words.
   - ✅ Verify the restored wallet ID matches step 3.
9. Investor: go to **Funded** tab and wait (poll/refresh over up to 5 minutes if needed).
   - ✅ Verify the investment from step 4 **reappears automatically** in the portfolio for the
     same project — with no manual re-entry of any investment details.

---

## 8. Wipe Data vs. Purge Recovery Files (`WipeDataRecoveryTest`)

**Purpose:** Verify the difference between a standard "Wipe Data" (keeps wallet recovery files
for restore) and "Wipe Data + Purge Recovery Files" (deletes them entirely).

**Preconditions:** None (wallets don't need funding).

**Steps:**
1. Start from a clean slate: **Settings** → **Wipe Data** with **Purge recovery files** ticked.
   Switch to Angornet, enable Debug Mode.
2. Create wallet #1 (leave unfunded). Note its ID.
3. Create wallet #2 (use the "create new" / force-new option so it doesn't reuse #1). Note its
   ID — confirm it's different from #1.
4. Sanity check: switch network to Mainnet, wait a moment, switch back to Angornet.
5. **Settings** → **Wipe Data** (this time WITHOUT the purge option). Switch back to Angornet.
6. Go to **Funds** → **Add Wallet** → **Import** → expand **Restore from backup** (or similarly
   named list of previously-known wallets).
   - ✅ Verify **2** entries are listed (both wallets survived the standard wipe).
7. Select wallet #1's entry to restore it.
   - ✅ Verify it restores successfully with the same wallet ID as step 2.
8. **Settings** → **Wipe Data** WITH **Purge recovery files** ticked this time. Switch to
   Angornet.
9. Go to **Funds** → **Add Wallet** → **Import** → **Restore from backup**.
   - ✅ Verify the list is now **empty** (0 entries) — the purge deleted all recovery files.
10. Create a fresh wallet to confirm the app still works normally after the full purge.

---

## 9 & 10. Large-Scale Load Tests (`BigFundTest`, `BigInvestTest`)

**Purpose:** Confirm the app handles a project with **15 concurrent investors** without errors
in claiming, approval, or recovery.

> These are long-running (many minutes) and resource-heavy (15 simultaneous investor sessions).
> **Skip in routine manual testing** — only run before a major release or when specifically
> investigating a scale-related bug. If you do run them, follow the same steps as Test 2
> (`BigFundTest` ≈ Fund project lifecycle) or Test 3 (`BigInvestTest` ≈ Investment project
> lifecycle) above, but with 15 investor sessions instead of 2-4, and expect the claim screen to
> show 15 claimable UTXOs instead of 2-4.

---

## Reporting Issues

For each failed step, capture:
- Which numbered test and step failed.
- Screenshot(s) of the unexpected state.
- The network (should always be Angornet) and approximate time.
- Any error message/toast text shown by the app.

File the issue against the matching automated test name (e.g. "MultiFundClaimAndRecoverTest step
9 — claim screen") so a developer can correlate it with `src/design/App.Test.Uat/` and re-run the
automated version for a full stack trace.
