# UI Testing Comments

Tracked issues and improvements found during manual testing of the new Avalonia app (`src/design/`).

---

## Create Wallet
1. ~~**Continue button needs spinner** — When creating a wallet, the popup hangs with no feedback. The Continue button should show a spinner and be disabled once clicked.~~ ✅ Fixed

## Find Projects
2. ~~**Refresh button should spin / show loading state** — The refresh button should spin while loading. If there are no projects loaded yet, show a large spinner.~~ ✅ Fixed
20. **Fund project: Missing instalment selector** — When funding a project of type Fund, there is no option to select which instalment pattern to use (e.g. 3-month or 6-month). Should show the available instalments, and once one is selected, show the payout breakdown schedule.
21. **Fund project: Show penalty threshold status** — Should indicate whether the funding amount is above or below the penalty threshold (i.e. whether it requires founder approval or not).

## Funder Tab
3. ~~**Missing refresh button** — No refresh button on the Funder tab; had to restart the app to see investors to approve.~~ ✅ Fixed
4. **Verify 'Approve All' button** — The 'Approve All' button needs to be checked if it works correctly. Add coverage in an integration test.

## Funded > Manage Project
5. ~~**Missing refresh button** — No refresh button on the Manage Project view.~~ ✅ Fixed
6. ~~**'View Transaction' link doesn't work** — The link to view a transaction does nothing.~~ ✅ Fixed
7. ~~**Refresh button doesn't work while waiting approval** — The refresh button on the manage project page doesn't update while waiting for approval. Had to go back to the main list and refresh there.~~ ✅ Fixed
8. ~~**Stages don't refresh after investing** — After investing, stages don't refresh automatically. Have to navigate away and back to the manage page to see changes.~~ ✅ Fixed
9. ~~**Invest button needs spinner** — The Invest button should spin and be disabled after clicking to prevent double-clicks.~~ ✅ Fixed
18. ~~**Stage percentage shows 0%** — The stage percentage in the list of stages shows 0%.~~ ✅ Fixed
23. **Penalty button is a mockup** — The penalty button does not show real pending penalties, it is currently a mockup.
24. ~~**Recover shows penalty popup when below threshold** — Clicking the recover button shows a penalty days popup even when the investment is below the threshold (no penalty applies). Should skip penalty and go straight to recovery.~~ ✅ Fixed

## My Projects (Founder) > Manage Project
10. **Missing 'Release Funds to Investor' button** — The release funds button is missing. Need to replicate from the avalonia app implementation (`src/avalonia/`). Also need an integration test for this.
17. ~~**Spend Stage popup disappears before confirmation** — When spending a stage as founder, the popup disappears before the confirmation popup appears, leaving the user unsure what happened.~~ ✅ Fixed
22. ~~**Claimable stage shows no info** — When funds are claimable, the stage shows nothing. Should show the number of UTXOs available to claim out of total (currently only shows 'available in X days' when not yet claimable).~~ ✅ Fixed

## Create Project
11. **Advanced editor not working** — The advanced editor in the create project wizard is not functional.
12. ~~**Debug prefill should use realistic stage dates** — In debug mode, the prefill button for invest should make stage 1 spendable immediately (today) but stages 2 and 3 should have future release dates to better simulate a real scenario.~~ ✅ Fixed
19. **Fund type: Selected instalments missing from summary** — When creating a Fund project and selecting two instalment patterns, the review summary doesn't show which instalments were selected.

## Investor > Manage Project (Recovery)
13. ~~**Recover/Penalty popup shows wrong stage count** — Shows all 3 stages even when some are already spent by the founder. Should only show the stages actually being recovered. Also doesn't show the penalty days.~~ ✅ Fixed
14. ~~**Show actual error messages in spending popups** — Recovery and other spending popups should show the actual error message (e.g. 'Not enough funds, expected 0.000072 BTC') instead of a generic failure like 'transaction may not be final'.~~ ✅ Fixed
15. ~~**Auto-refresh wallet before recovery** — The recovery flow should auto-refresh wallet balance before attempting recovery, or at minimum show the current wallet balance in the popup so the user knows if they have enough for fees.~~ ✅ Fixed
16. ~~**Stage status blank after recovery** — After recovering from penalty, stage status shows nothing. Should show 'Spent by investor'.~~ ✅ Fixed

---

## Completed Fixes
- [x] **Stage date bug** — Stage release dates were stored as ordinal display strings (e.g. "20th April 2026") which `DateTime.TryParse` couldn't parse, silently falling back to `startDate + N months`. Fixed by adding typed `ReleaseDateValue` (DateOnly) to `ProjectStageViewModel`.
- [x] **Hardened BuildCreateProjectDto** — Removed all silent defaults in project deployment. Target amount, project type, start date, stage percentage, payout day, and project name now throw `InvalidOperationException` instead of silently defaulting.
