# Exception Interface Questions Checklist

Purpose: use this as the product/design QA checklist while reviewing the running app and the temporary Exception UX Lab. Each row asks what the interface should show when that condition occurs.

Source tracker: `docs/ai-docs/EXCEPTION_SUCCESS_TRACKER.md`

## New App

| ID | Interface Question |
|---|---|
| DESIGN-CREATE-001 | If the user tries to continue without choosing a project type, should this stay as inline validation beside the project type choice? |
| DESIGN-CREATE-002 | If the project name is blank or too long, should the field show a short inline error and keep the user on the same step? |
| DESIGN-CREATE-003 | If the project description is blank or too long, should the field show a short inline error and keep the user on the same step? |
| DESIGN-CREATE-004 | If an amount is missing, zero, below minimum, or above maximum, should the amount field show the exact acceptable range? |
| DESIGN-CREATE-005 | If the funding end date is missing, too soon, or too far out, should the date field explain the allowed window? |
| DESIGN-CREATE-006 | If penalty days are invalid, should this be field-level validation with the production minimum and maximum shown? |
| DESIGN-CREATE-007 | If no stages or payout schedule exists, should the stage/payout step show a blocking inline error with the action needed? |
| DESIGN-CREATE-008 | If stage dates conflict with the funding timeline, should each bad row show its own date error plus a top summary? |
| DESIGN-CREATE-009 | If deploy cannot start because wizard data is internally invalid, should the user see a friendly blocking form error instead of DTO/technical wording? |
| DESIGN-DEPLOY-001 | If project data is missing when deploy starts, should this be a blocking flow error that asks the user to go back to the wizard? |
| DESIGN-DEPLOY-002 | If founder project keys cannot be created, should the deploy flow explain wallet/key setup failed and offer retry/unlock guidance? |
| DESIGN-DEPLOY-003 | If project profile publishing fails, should the flow say Nostr relays could not save the profile and offer retry? |
| DESIGN-DEPLOY-004 | If project info publishing fails, should the flow say project details could not be published to relays and offer retry? |
| DESIGN-DEPLOY-005 | If the Bitcoin deploy transaction cannot be built, should the flow say the wallet could not prepare the transaction and suggest checking balance/fee/wallet state? |
| DESIGN-DEPLOY-006 | If the deploy transaction cannot be broadcast, should the flow say the network/indexer rejected broadcast and expose optional technical details? |
| DESIGN-DEPLOY-007 | If deploy throws unexpectedly, should the user see generic safe copy plus optional technical details, not raw exception text? |
| DESIGN-DEPLOY-008 | If invoice deploy monitoring has no wallet, should the flow tell the user a wallet is required to watch for payment? |
| DESIGN-DEPLOY-009 | If invoice monitoring is cancelled, should this be silent/info state rather than an error? |
| DESIGN-DEPLOY-010 | On successful deploy, is the success modal wording enough and should it be the only success component? |
| DESIGN-PAY-001 | If direct wallet payment callback returns failure, should the payment modal show a friendly inline error with retry? |
| DESIGN-PAY-002 | If direct wallet payment throws, should the modal hide raw exception details unless expanded? |
| DESIGN-PAY-003 | If auto-wallet creation for invoice payment fails, should the modal explain wallet creation/reload failed and what the user can do next? |
| DESIGN-PAY-004 | If wallet refresh or receive address generation fails, should the modal say it could not prepare a receive address and avoid method names like `GetNextReceiveAddress`? |
| DESIGN-PAY-005 | If on-chain invoice monitoring fails, should the modal distinguish setup failure, timeout, and monitor/network failure? |
| DESIGN-PAY-006 | If Lightning setup fails, should the modal explain invoice setup failed and fall back to on-chain when possible? |
| DESIGN-PAY-007 | If Lightning payment monitor or claim fails after invoice creation, should the modal explain whether payment is still safe/pending and what to do next? |
| DESIGN-PAY-008 | If payment is detected but invest/deploy callback fails, should the modal use a serious blocking error that says payment was received but final processing failed? |
| DESIGN-PAY-009 | When payment is detected, should `Payment received` and `Processing` be clearly intermediate, not final success? |
| DESIGN-FUNDS-001 | If wallet creation fails in Funds, should the create-wallet modal show a friendly error and keep entered data? |
| DESIGN-FUNDS-002 | If one wallet cannot refresh, should Funds show a non-blocking warning for that wallet rather than silently logging? |
| DESIGN-FUNDS-003 | If faucet/test coins fail, should the user see faucet-specific wording and whether it is a balance-limit or service issue? |
| DESIGN-FUNDS-004 | If sending funds fails, should the send modal keep the transaction form open and show fee/broadcast-specific copy? |
| DESIGN-FUNDS-005 | On successful send, should the user see txid, copy/open actions, and a clear pending-confirmation state? |
| DESIGN-PORTFOLIO-001 | If portfolio investments cannot load, should Portfolio show a retryable error/empty state rather than silently hiding investments? |
| DESIGN-RECOVERY-001 | If recover/release/claim transaction building fails, should the recovery modal show action-specific copy and optional technical details? |
| DESIGN-MYPROJECTS-001 | If My Projects cannot load, should the section show a retryable error state instead of an empty list? |
| DESIGN-MANAGE-001 | If claimable/releasable data cannot load, should Manage Project show a scoped error on the relevant modal/table? |
| DESIGN-SHELL-001 | If settings/network/profile saving fails, should Shell show a toast/banner or only log it? |

## Recovery UX Lab Cases

| Case | Interface Question |
|---|---|
| Recover without penalty | Founder signatures are available. Should this be framed as a safe `Release` action rather than `Recovery`? |
| End-of-project claim | The project has ended. Should this be framed as a normal `Claim` action with no penalty warning? |
| Below-threshold direct recovery | The investment is below penalty threshold. Should the UI skip penalty-lock warnings and use direct recovery wording? |
| Recover to penalty | Funds can be recovered now but locked during penalty. Is the warning strong enough and specific enough? |
| Recover from penalty | Penalty has ended. Should this use release wording and avoid repeating the original recovery warning? |

## Primary Avalonia App

| ID | Interface Question |
|---|---|
| AV-WALLET-001 | If seed words are empty, wrong length, or invalid, should the import flow show inline validation and keep Continue disabled? |
| AV-WALLET-002 | If wallet creation/import fails, should the error dialog use friendly recovery guidance instead of raw service text? |
| AV-WALLET-003 | On wallet creation success, is the success dialog needed or would a toast/navigation state be better? |
| AV-WALLET-004 | If seed backup file picking/saving fails, should the dialog distinguish picker unavailable, picker cancelled, and write failed? |
| AV-WALLET-005 | On seed backup success, should this be a success toast and should it remind the user to store it safely? |
| AV-WALLET-006 | If password/passphrase is missing or invalid, should the prompt use field validation and avoid raw encryption-key wording? |
| AV-WALLET-007 | If wallet history/balance cannot load, should the wallet surface show retryable inline state rather than a generic notification? |
| AV-WALLET-008 | If faucet fails or wallet has too many test coins, should the notification be warning/error styled, not generic failure? |
| AV-WALLET-009 | If faucet succeeds, should there be explicit success copy or just balance refresh? |
| AV-SEND-001 | If send amount/address is invalid, should validation be inline and prevent preview? |
| AV-SEND-002 | If transaction preview cannot be created, should the preview screen explain fee/UTXO preparation failed? |
| AV-SEND-003 | If broadcast/send fails, should the failure include user-safe reason plus optional technical details? |
| AV-SEND-004 | On send success, should success include txid/open explorer/copy actions? |
| AV-CREATE-001 | If project creation starts without a wallet, should the UI route to wallet setup instead of generic failure? |
| AV-CREATE-002 | If project type is unsupported, should this be impossible in UI or shown as a user-safe unsupported feature message? |
| AV-CREATE-003 | If project profile fields are invalid, should validation be inline and block Next? |
| AV-CREATE-004 | If funding target/date/penalty is invalid, should the wizard show exact accepted values? |
| AV-CREATE-005 | If stages are invalid, should the error summary link/highlight the rows needing correction? |
| AV-CREATE-006 | If auto-generation lacks inputs, should the UI show a hint instead of silently doing nothing? |
| AV-CREATE-007 | If fund payout settings are missing, should Next be blocked until payout settings are complete? |
| AV-CREATE-008 | If image select/upload fails, should upload status hide raw exceptions and offer retry/change file? |
| AV-CREATE-009 | On image selection/upload success, is inline status sufficient? |
| AV-DEPLOY-001 | If deploy preview creation fails, should transaction previewer use deploy-specific copy? |
| AV-DEPLOY-002 | If deploy broadcast fails, should the generic `submit investment offer` text be replaced with deploy wording? |
| AV-DEPLOY-003 | On deploy success, should duplicate notification/dialog be collapsed into one success state? |
| AV-DEPLOY-004 | If the user cancels project creation preview, should no error be shown? |
| AV-FIND-001 | If latest projects fail to load, should Find Projects show a retryable error instead of silent empty state? |
| AV-FIND-002 | If JSON serialization fails, should the dialog say details cannot be shown and hide raw exception unless expanded? |
| AV-FIND-003 | If backend fields are missing, should debug placeholders be replaced with missing-data UI? |
| AV-INVEST-001 | If investment is outside funding window, should the button be disabled with visible reason? |
| AV-INVEST-002 | If project lookup/navigation fails, should the investment flow show a retryable project-load error? |
| AV-INVEST-003 | If amount is below min or requires approval, should the footer explain the reason before submit? |
| AV-INVEST-004 | If wallet payment/draft/request/publish fails, should the payment screen show action-specific failure copy? |
| AV-INVEST-005 | If approval is required and request succeeds, should success clearly say pending founder approval? |
| AV-INVEST-006 | If direct publish succeeds, should success clearly say investment is submitted/active and pending confirmations? |
| AV-INVOICE-001 | If receive address generation fails, should invoice setup show retry guidance and avoid raw errors? |
| AV-INVOICE-002 | If Lightning invoice setup fails, should the UI fall back to on-chain and show warning severity? |
| AV-INVOICE-003 | If invoice payment monitoring fails, should the UI tell the user whether payment may still arrive? |
| AV-INVOICE-004 | If payment arrives but investment build/submit/publish fails, should this be a high-severity finalization error? |
| AV-INVOICE-005 | When invoice payment is detected, should the UI show payment received as intermediate, not final investment success? |
| AV-FUNDED-001 | If funded projects fail to load, should the section show retry/error rather than silent empty state? |
| AV-FUNDED-002 | If cancel investment fails, should title be error-specific instead of `Canceled` for both success and failure? |
| AV-FUNDED-003 | If confirm investment fails, should title be error-specific instead of `Confirmed` for both success and failure? |
| AV-FUNDED-004 | If no recovery action is available, should the command be hidden/disabled with reason instead of failing silently? |
| AV-FUNDED-005 | If claim/recovery/release draft or commit fails, should transaction previewer use claim/recovery-specific copy? |
| AV-MYPROJECTS-001 | If founder projects cannot load, should My Projects show a retryable section error? |
| AV-MANAGE-001 | If management stats cannot load, should the project management page show scoped retry controls? |
| AV-MANAGE-002 | If releasing signatures fails/succeeds, should no-selection, no-items, failure, and success each have distinct copy? |
| AV-MANAGE-003 | If founder claim/spend fails or is cancelled, should cancellation be informational and failures action-specific? |
| AV-FUNDERS-001 | If approval/rejection fails, should the confirmation result be shown with correct severity and raw details hidden? |
| AV-SETTINGS-001 | If UI preference load/save fails, should this stay log-only or show a low-priority warning? |
| AV-SETTINGS-002 | If fee API is unavailable, should the fee picker show fallback estimates are being used? |
| AV-SETTINGS-003 | If network change succeeds/cancels/fails, should each outcome have explicit feedback? |
| AV-SETTINGS-004 | If indexer/relay refresh throws, should dialogs hide raw exception unless expanded? |
| AV-SETTINGS-005 | If data wipe partially fails, should ignored delete failures be surfaced? |
| AV-SETTINGS-006 | If seed backup fails, should dialog distinguish no wallet, unlock failure, and backup unavailable? |

## SDK And Shared Messages That Bubble To UI

| ID | Interface Question |
|---|---|
| SDK-WALLET-001 | When wallet unlock/private seed access fails, what common user-facing password/unlock message should callers show? |
| SDK-WALLET-002 | When wallet history fetch throws, should callers show `Could not load wallet history` with retry? |
| SDK-WALLET-003 | When preview/send lacks change address or fee estimate, should callers explain wallet cannot prepare a transaction yet? |
| SDK-WALLET-004 | When receive address generation fails, should callers say the wallet could not create a receive address and suggest unlock/retry? |
| SDK-WALLET-005 | When broadcast fails, should callers map indexer/raw HTTP errors into user-safe broadcast failure copy? |
| SDK-WALLET-006 | When wallet delete/wipe has multi-errors, should UI show partial failure details and what remains? |
| SDK-WALLET-007 | When test coin request fails, should UI distinguish balance too high, faucet down, and balance unavailable? |
| SDK-WALLET-008 | When restore/migration rebuild fails, should UI say wallet restore needs attention rather than exposing document names? |
| SDK-WALLET-009 | When mnemonic/restore data is invalid, should UI use seed-word validation copy and avoid decrypted-data wording? |
| SDK-TX-001 | When broadcast is rejected or guarded, should UI show safe summary plus expandable raw transaction/indexer details? |
| SDK-PROJECT-001 | When a project cannot be found, should UI show not-found/removed/unavailable copy with retry/back? |
| SDK-PROJECT-002 | When latest projects fail/time out, should UI show retryable network/relay empty state? |
| SDK-PROJECT-003 | When profile data partially fails, should UI show partial profile with missing-field placeholders? |
| SDK-PROJECT-004 | When project investment/stage scan fails, should UI show scanning/retry state instead of raw output/tx errors? |
| SDK-FOUNDER-001 | When founder keys are missing or slots are used, should UI guide the founder to load projects or explain max slots? |
| SDK-FOUNDER-002 | When profile publish fails/times out, should UI show relay publish failure with retry? |
| SDK-FOUNDER-003 | When project info validation/publish fails, should UI map required-field/unsupported-type errors to wizard fields? |
| SDK-FOUNDER-004 | When on-chain project transaction build/save has issues, should UI distinguish build failure from local save warning? |
| SDK-FOUNDER-005 | When project transaction publish is cancelled/rejected, should UI treat cancellation separately from network failure? |
| SDK-FOUNDER-006 | When profile update partially fails, should UI show what was saved and what was not? |
| SDK-FOUNDER-007 | When founder project scan finds no keys/projects, should UI show empty state or setup guidance? |
| SDK-FOUNDER-008 | When founder investment request loading fails, should UI show retryable request-load error? |
| SDK-FOUNDER-009 | When approve investment fails with blank/technical errors, should UI use a default approval-failed message? |
| SDK-FOUNDER-010 | When release signatures has partial/decrypt failures, should UI show partial results and skipped items? |
| SDK-FOUNDER-011 | When spending a stage fails, should UI map multiple-stage/key/address/change-address errors to actionable steps? |
| SDK-INVEST-001 | When investment draft build fails, should UI distinguish already invested, amount/pattern invalid, no UTXOs, and signing errors? |
| SDK-INVEST-002 | When threshold check is invalid/unavailable, should UI show approval-status unavailable instead of raw operation text? |
| SDK-INVEST-003 | When signature request fails, should UI explain duplicate investment, missing release address, or relay send failure? |
| SDK-INVEST-004 | When approved investment publish fails, should UI distinguish missing stored transaction, signature mismatch, and broadcast reject? |
| SDK-INVEST-005 | When cancel investment fails, should UI distinguish missing record, already on-chain, and local removal failure? |
| SDK-PORTFOLIO-001 | When portfolio processing finds mismatches/exceptions, should UI show partial portfolio plus warning or block the view? |
| SDK-RECOVERY-001 | When recovery status cannot load, should UI show recovery actions unavailable with retry? |
| SDK-RECOVERY-002 | When release signature check finds none/times out, should UI show waiting for founder signatures rather than error? |
| SDK-RECOVERY-003 | When recovery transaction build fails, should UI map missing signatures, invalid signatures, missing tx, and change address to recovery-specific copy? |
| SDK-RECOVERY-004 | When unfunded release build fails, should UI use release-specific copy and avoid thrown validation details? |
| SDK-RECOVERY-005 | When penalty release fails, should UI explain the prerequisite recovery transaction/confirmation state? |
| SDK-RECOVERY-006 | When end-of-project claim fails, should UI explain missing investment/output/indexer data as claim-specific retry copy? |
| SDK-LN-001 | When Boltz/Lightning calls fail, should UI distinguish invoice creation, status check, claim signature, and deserialization/service errors? |

## Legacy Blazor App

| ID | Interface Question |
|---|---|
| WEB-NOTIFY-001 | Should web error notifications hide stack traces by default and expose technical details only on demand? |
| WEB-NOTIFY-002 | Should web notifications support info/warning/error/success severity instead of always green success? |
| WEB-PASSWORD-001 | Should wallet password errors be inline and consistent across web flows? |
| WEB-MESSAGE-001 | Should direct messaging failures distinguish missing keys/contact, connection failure, send failure, refresh failure, and copy failure? |
| WEB-WALLET-001 | Should create/recover wallet validation be inline and successes use one clear success component? |
| WEB-WALLET-002 | Should refresh balance avoid showing success from `finally` after an error? |
| WEB-WALLET-003 | Should seed reveal/copy handle no password, invalid password, no words, and copy success with distinct copy? |
| WEB-WALLET-004 | Should receive address copy failure/success be distinct and not use generic notifications? |
| WEB-WALLET-005 | Should send coins distinguish validation, fee estimate, password expiry, broadcast reject, and success? |
| WEB-WALLET-006 | Should faucet balance-limit and faucet failure be warning/error styled, not success styled? |
| WEB-CREATE-001 | Should project profile/info publish failures show relay/NIP-65/profile-info specific copy? |
| WEB-CREATE-002 | Should project transaction deployment distinguish missing Nostr info, fee failure, build failure, publish reject, and missing txid? |
| WEB-CREATE-003 | Should locked-field edits be disabled with explanation before the user attempts change? |
| WEB-CREATE-004 | Should form validation be inline by field/step rather than aggregated notification only? |
| WEB-CREATE-005 | Should auto deployment show step-by-step progress and identify exactly which step failed? |
| WEB-INVEST-001 | Should closed investment window be warning/info styled and explain dates/reason? |
| WEB-INVEST-002 | Should balance/stats/signature loading failures show scoped retry states? |
| WEB-INVEST-003 | Should amount/stage/npub validation be inline and block submit? |
| WEB-INVEST-004 | Should transaction build/recalculate failures hide raw exception details by default? |
| WEB-INVEST-005 | Should signature request relay failures show which relay failed and whether any succeeded? |
| WEB-INVEST-006 | Should publish-after-signing failures distinguish missing details, invalid signatures, broadcast reject, and success? |
| WEB-INVEST-007 | Should auto-wallet/invoice payment distinguish wallet creation, receive address, timeout, payment detected, and final build failure? |
| WEB-INVEST-008 | Should Lightning payment distinguish fee quote, swap creation, payment timeout/failure, lockup missing, claim failure, and funds not detected? |
| WEB-SIGN-001 | Should founder signature request failures show password, fetch, decrypt, approve, and approve-all states separately? |
| WEB-UNFUNDED-001 | Should unfunded release approval show password, refresh/decrypt, approve failure, and partial success states? |
| WEB-RELEASE-001 | Should release funds distinguish password, signature type, validation, fee, publish, success, and exception? |
| WEB-SPEND-001 | Should spend/claim stage coins distinguish refresh, build, fee, publish, copy, and success states? |
| WEB-RECOVER-001 | Should recovery/recovery-release/end-of-project web flows use the same wording/components as the new recovery lab? |
| WEB-SETTINGS-001 | Should indexer/relay/explorer URL validation be inline and distinguish offline/wrong-network/duplicate? |
| WEB-SETTINGS-002 | Should settings refresh/wipe/network change success and failure use correct severity? |
| WEB-VIEW-001 | Should project view/profile/republish failures distinguish invalid password, not founder, missing key, partial republish, and full failure? |

## Cross-Cutting Decisions

| ID | Interface Question |
|---|---|
| X-RAW-EXCEPTION | What standard component shows user-safe error copy with optional technical details? |
| X-WARNING-AS-SUCCESS | What severity variants should notifications support across apps? |
| X-SILENT-FAILURE | Which failures are acceptable as log-only, and which require visible UI? |
| X-DUPLICATE-SUCCESS | Should each flow have only one success component, and which one wins? |
| X-WRONG-CONTEXT | How should shared transaction previewer copy be parameterized by operation? |
| X-CANCEL-AS-ERROR | Should user cancellation be silent, informational, or explicit status? |
| X-TECHNICAL-PLACEHOLDERS | What placeholder/empty-state copy replaces backend/TODO/method-name text? |
