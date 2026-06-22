# Angor: Staged Bitcoin Investment with Regulated Security Token Release

**Integration Proposal for Tokenization Partners**

## 1. Executive Summary

This document proposes an integration model between **Angor**, a Bitcoin-native fundraising protocol with Investor-Controlled Capital Release (ICCR), and **regulated tokenization platforms** that can issue security tokens tied to Angor's staged Bitcoin releases.

The core idea: investors commit Bitcoin into Angor's time-locked contracts. Security tokens (shares in an SPV or equivalent legal structure) are released to investors incrementally as each stage's timelock expires and the founder claims the Bitcoin. The release of shares can be made cryptographically atomic -- the act of claiming Bitcoin on the Bitcoin blockchain reveals a secret that unlocks the corresponding share tokens on the issuance chain.

This creates a structure where:

- A **tokenization partner** handles all legal, regulatory, and compliance requirements (entity formation, KYC/AML, prospectus, securities law).
- **Angor** provides the trustless Bitcoin delivery mechanism with investor protections built into the protocol.
- The **issuance chain** (Liquid, Rootstock, Ethereum, or other) hosts the tokenized shares that are progressively unlocked as Bitcoin is released.

The result is a regulated securities offering where the Bitcoin payment is not a single lump-sum transfer but a staged, investor-protected commitment -- giving both founders and investors stronger guarantees than either system provides alone.

## 2. Investor-Controlled Capital Release (ICCR)

Traditional startup investing transfers control of funds to founders immediately after investment:

```
Traditional:  Investor -> Capital committed -> Founder controls funds
Angor:        Investor -> Capital committed -> Investor controls capital release
```

This is more than milestone funding. It is a new investment primitive that reduces trust requirements on founders while maintaining access to capital.

**Why ICCR matters for securities integration:**

In a traditional securities offering, once the investor sends payment, they rely entirely on legal recourse if the project fails. With ICCR, the investor has a **technical mechanism** to recover uncommitted capital -- reducing risk and making offerings more attractive.

The staged release structure means founders are incentivized to deliver results at each stage, because investors can exit if milestones are not met. This alignment is stronger than legal obligations alone.

**Where ICCR applies and where it does not:**

ICCR is particularly relevant to **startup and project financing**, where investors want greater protection and accountability. It is less applicable to **funds and RWA structures**, which typically rely on managers, auditors, or custodians for capital management.

> Angor is a Bitcoin-native fundraising protocol that enables Investor-Controlled Capital Release (ICCR), reducing founder trust requirements while preserving startup access to funding.

## 3. What is Angor

Angor is a decentralized peer-to-peer funding protocol built on Bitcoin. It allows founders to raise capital and investors to commit funds using time-locked Bitcoin contracts, without requiring intermediaries, custodians, or trust in the founder.

### How it works

1. **A founder creates a project** by publishing project parameters to the Nostr network and broadcasting a Bitcoin initialization transaction. The project defines stages -- each stage has a timelock (a future date after which the Bitcoin for that stage can be claimed) and a percentage of the total investment.

2. **An investor commits Bitcoin** by constructing a special Bitcoin transaction. This transaction creates one Taproot output per stage, each containing multiple spending paths:
   - **Founder claim**: the founder can spend the output after the stage's timelock expires (using `OP_CHECKLOCKTIMEVERIFY` and the founder's key).
   - **Penalty recovery**: the investor can recover funds at any time by publishing a pre-signed recovery transaction, but must wait through a penalty timelock.
   - **Founder release**: the founder can sign a transaction releasing the remaining funds back to the investor without penalty.
   - **End-of-project recovery**: after the project's expiry date, the investor can reclaim any unclaimed funds.

3. **As each stage's timelock expires**, the founder claims the Bitcoin for that stage. Investors who are unhappy with the project's progress can recover their remaining funds before later stages unlock, forfeiting the share tokens allocated to the recovered stages.

### Key properties

- **No custodian**: Bitcoin is locked in scripts on the Bitcoin blockchain, not held by any third party.
- **Investor protection**: investors can exit at any time (with or without penalty depending on conditions).
- **Staged release**: the founder does not receive all funds at once -- they are released incrementally over time, aligning founder incentives with project delivery.
- **Decentralized**: the protocol uses Bitcoin for value transfer and Nostr for communication. There are no central servers or intermediaries.

### Example

A founder creates a project with 10 stages, each releasing 10% of the investment over 10 months. An investor commits 5 BTC:

| Stage | Timelock | Amount | Cumulative Released |
|-------|----------|--------|---------------------|
| 1     | Month 1  | 0.5 BTC | 10%                |
| 2     | Month 2  | 0.5 BTC | 20%                |
| 3     | Month 3  | 0.5 BTC | 30%                |
| ...   | ...      | ...     | ...                |
| 10    | Month 10 | 0.5 BTC | 100%               |

After month 1, the founder claims 0.5 BTC. If the investor loses confidence at month 3, they can recover the remaining 3.5 BTC (stages 4-10) either with a penalty wait or via a founder-signed release.

## 4. The Integration: Staged Bitcoin Payment with Progressive Share Release

### The Problem Each System Solves Alone

**A tokenization platform alone**: An investor buys tokenized securities by sending Bitcoin (or fiat) to an SPV. The payment is a one-time transfer. Once the Bitcoin is sent, the investor has no mechanism to recover it if the project underperforms. The investor's only protection is the legal structure and regulatory oversight. Additionally, once Bitcoin is transferred to the SPV, it is exposed to custodial risks -- key loss, hacks, or mismanagement by the entity holding the funds.

**Angor alone**: An investor locks Bitcoin into time-locked contracts that protect them from founder fraud or underperformance. But there is no regulated security issued in return -- the investment is purely a Bitcoin-native commitment with no legal recognition as a securities purchase.

### What the Integration Creates

By combining the two systems, we create a model where:

- The **Bitcoin payment** is not a lump-sum transfer but a **staged, revocable commitment** (via Angor's ICCR).
- The **security tokens** (shares in the SPV) are not delivered all at once but are **progressively unlocked** as the founder earns the Bitcoin stage by stage.
- The unlock of shares is **cryptographically tied** to the claim of Bitcoin, making it trustless.

### How It Works

#### Setup Phase

1. The **founder** works with a tokenization partner to create an SPV (or equivalent legal entity) and structure the securities offering (prospectus, legal entity, terms).
2. The partner issues tokenized shares on the chosen blockchain. The shares are transfer-restricted -- only KYC-verified investors can hold them.
3. The founder creates an **Angor project** with defined stages (e.g., 10 stages over 10 months, 10% per stage). The Angor project metadata references the securities offering, linking the two systems.
4. The partner onboards and KYC-verifies investors.

#### Investment Phase

5. A KYC-verified investor commits Bitcoin via Angor's protocol (e.g., 5 BTC locked into 10 stages of 0.5 BTC each).
6. The SPV **allocates** shares proportional to the total investment amount. However, these shares are **not immediately accessible** to the investor. They are locked on the issuance chain, with each portion's release tied to the corresponding Angor stage.

#### Progressive Release Phase

7. When stage 1's timelock expires, the **founder claims 0.5 BTC** from the Angor contract. This claim can be structured to reveal a cryptographic secret (a hashlock preimage).
8. That same secret **unlocks 10% of the investor's allocated shares** on the issuance chain. The investor (or an automated process) uses the revealed secret to claim their share tokens.
9. This repeats for each stage. After stage 5, the investor holds 50% of their shares and the founder has claimed 50% of the Bitcoin. After all 10 stages, 100% of shares are released.

#### Early Exit

10. If the investor decides to recover their remaining Bitcoin via Angor's recovery mechanism (e.g., recovering stages 6-10), the corresponding 50% of shares that were never unlocked remain **permanently locked or burned**. The founder never received that Bitcoin, so the SPV does not release those shares.

### Illustrated Example

An investor commits **5 BTC** to a project with 10 equal stages. The SPV allocates 1,000 shares total:

| Event | Bitcoin | Shares Unlocked | Investor Holds |
|-------|---------|-----------------|----------------|
| Investment made | 5 BTC locked | 0 of 1,000 | 0 shares |
| Stage 1 claimed by founder | 0.5 BTC released | 100 shares | 100 shares |
| Stage 2 claimed | 0.5 BTC released | 100 shares | 200 shares |
| Stage 3 claimed | 0.5 BTC released | 100 shares | 300 shares |
| Investor recovers stages 4-10 | 3.5 BTC returned | -- | 300 shares (final) |
| Shares for stages 4-10 | -- | Burned/locked | -- |

The investor paid 1.5 BTC and received 300 shares. The founder received 1.5 BTC and the SPV issued 300 shares. The remaining 700 shares are never released.

## 5. Share Release Models

The progressive share release can be implemented with different trust assumptions. The choice depends on the parties' appetite for technical complexity versus trust minimization. These models are not mutually exclusive -- different offerings could use different models, or a project could start with a simpler model and upgrade later.

### Model 1: Founder Release (founder-trusted)

The simplest model. The founder is responsible for releasing share tokens to investors after claiming each Bitcoin stage.

**How it works:**
1. The founder claims Bitcoin from a stage via Angor.
2. The founder (or Angor acting on the founder's behalf) calls the tokenization platform API to distribute the corresponding share tokens to the investor's wallet.

**Trust assumption:** The investor trusts the founder to release the shares after claiming the Bitcoin. If the founder claims Bitcoin but does not release shares, the investor's recourse is legal (via the regulatory framework and the SPV's obligations).

**Complexity:** Low. No protocol changes required. Uses existing platform APIs.

**Best for:** Initial proof of concept, situations where the legal framework provides sufficient guarantees, or where the founder is a well-established entity.

### Model 2: Escrow Release (shared trust)

An escrow party (the tokenization partner, a neutral third party, or a multisig arrangement) co-signs the share release alongside the founder. Neither party alone can release the shares -- both must agree.

**How it works:**
1. Share tokens for each stage are locked on the issuance chain in a multisig or smart contract requiring both the founder and the escrow party.
2. The founder claims Bitcoin from a stage via Angor.
3. The founder signs the release transaction and sends it to the escrow.
4. The escrow verifies that the founder has legitimately claimed the Bitcoin stage on-chain, then co-signs the release.
5. The investor receives the share tokens.

**Trust assumption:** Trust is distributed. The escrow prevents the founder from withholding shares. The founder prevents the escrow from releasing shares prematurely. Collusion is the remaining risk, mitigated by choosing an independent escrow party.

**Complexity:** Medium. Requires multisig or smart contract scripts on the issuance chain and an escrow verification process, but no changes to Angor's Bitcoin-side protocol.

**Best for:** Production deployments where fully trustless atomic release is not yet available, or where regulatory requirements favor an identifiable release authority.

### Model 3: Atomic Release (zero trust)

The fully trustless model. The release of shares is cryptographically tied to the claim of Bitcoin, so neither party has to trust the other -- the protocol enforces it.

**How it works:**
1. For each stage, the founder generates a **secret** (a random preimage) and commits its **hash** to both the Angor Bitcoin contract and an escrow script on the issuance chain.
2. On the **Bitcoin side**: the stage-claim spending path requires the founder to reveal this preimage (in addition to their key and the timelock expiry).
3. On the **issuance chain side**: the allocated shares for that stage are locked in a script/contract that requires the same preimage plus the investor's key to spend.
4. When the founder claims the Bitcoin (revealing the preimage on the Bitcoin blockchain), the investor observes the revealed preimage and uses it to claim their shares.

This is effectively an **atomic swap**: the founder cannot claim the Bitcoin without revealing the secret, and the revealed secret is exactly what the investor needs to unlock their shares. Neither party can cheat.

Crucially, **the investor's key is also required** to claim the shares. This means the founder cannot claim the shares even after revealing the secret -- only the investor can.

**Trust assumption:** None. The cryptography enforces the exchange.

**Complexity:** High. Requires a protocol extension to Angor (adding a hashlock to the founder's stage-claim spending path), HTLC scripts or smart contracts on the issuance chain, and a monitoring service for investors to detect revealed preimages on the Bitcoin blockchain. This is a natural extension of Angor's existing tapscript structure.

**Best for:** The long-term target. Provides the strongest guarantees and aligns with the trustless ethos of both Bitcoin and Angor.

### Comparison

| | Model 1: Founder Release | Model 2: Escrow Release | Model 3: Atomic Release |
|---|---|---|---|
| Trust required | Founder | Founder + Escrow (shared) | None |
| Complexity | Low | Medium | High |
| Protocol changes | None | None (issuance-chain only) | Angor protocol extension |
| Regulatory clarity | High (identifiable releaser) | High (identifiable co-signers) | Lower (automated, no human in the loop) |
| Settlement risk | Founder could withhold shares | Collusion risk only | Zero |

We recommend starting with **Model 1** for a proof of concept, moving to **Model 2** for production deployments, and developing **Model 3** as the long-term trustless solution.

## 6. Why This Benefits a Tokenization Partner

### Investor protection as a differentiator

In a traditional securities offering, once the investor sends payment, they rely entirely on legal recourse if the project fails. With Angor's ICCR, the investor has a **technical mechanism** to recover uncommitted funds -- reducing risk and potentially making offerings more attractive to investors.

### Aligned incentives

The staged release structure means founders are incentivized to deliver results at each stage, because investors can exit if milestones are not met. This is a stronger alignment than legal obligations alone and reduces the risk of projects that raise capital and underperform.

### Access to Bitcoin-native capital

Angor opens access to Bitcoin holders who want to invest capital but are reluctant to make lump-sum, irrevocable transfers. The staged, recoverable model lowers the barrier for Bitcoin holders to participate in regulated securities offerings.

### Flexible trust models

The share release mechanism supports a spectrum from founder-trusted (simplest) through escrow-based (shared trust) to fully atomic/trustless (zero trust). The partner and founder can choose the model that matches their regulatory requirements and technical readiness, with a clear upgrade path toward zero-trust atomic release.

### Expandable investor base

In the future, there is potential for a model where a KYC-verified lead investor uses Angor to aggregate capital from smaller participants, investing under their name while giving smaller investors economic exposure to the share price. This would require additional legal structuring but could significantly expand the addressable market.

## 7. Tokenization Partner Options

### STOKR

- **Jurisdiction:** Luxembourg (EU MiFID II, CSSF)
- **Issuance chain:** Liquid Network (Blockstream AMP)
- **Focus:** Funds, RWAs, structured products; qualified/high-net-worth investors (100K USD minimum)
- **Status:** STOKR's current focus on funds and RWAs means investor-controlled release is less aligned with their typical product. They favor proof-based approaches (managers, auditors, custodians). STOKR remains viable for qualified-investor offerings on Liquid but is not currently pursuing startup/retail markets.

### Brickken

- **Jurisdiction:** Spain (EU framework)
- **Issuance chain:** Ethereum
- **Focus:** SMEs, private companies, retail investors
- **Status:** Most closely aligned with Angor's startup financing use case. Cap table management and investor onboarding built in. Limited secondary market liquidity is the main drawback.

### Securitize

- **Jurisdiction:** USA (SEC-registered transfer agent)
- **Issuance chain:** Multi-chain (Ethereum, Polygon, Avalanche, etc.)
- **Focus:** Institutional and retail; secondary trading (Securitize Markets)
- **Status:** Strongest regulatory infrastructure and secondary trading capabilities. More expensive and potentially overkill for early-stage startup fundraising. Best suited for larger offerings requiring institutional credibility.

### Tokeny

- **Jurisdiction:** Luxembourg (ERC-3643 compliance framework)
- **Issuance chain:** Ethereum
- **Focus:** Compliance tooling; requires separate legal issuance partner
- **Status:** Good option if Angor wants to control more of the user experience while relying on an external compliance layer. Would need to be paired with a legal partner.

### Summary

| Partner | Startup Focus | Retail Investors | Bitcoin Alignment | Secondary Trading | Complexity |
|---------|---------------|------------------|-------------------|-------------------|------------|
| STOKR | Low | No (qualified only) | High (Liquid) | Limited | Low |
| Brickken | High | Yes | Low (Ethereum) | Limited | Medium |
| Securitize | Medium | Yes | Low (multi-chain) | Yes | High |
| Tokeny | Medium | Configurable | Low (Ethereum) | Configurable | Medium |

## 8. Blockchain Options

| Blockchain | Bitcoin Alignment | EVM Compatible | Tokenization Ecosystem | Liquidity |
|------------|-------------------|----------------|------------------------|-----------|
| **Liquid** | Native (sidechain) | No | STOKR, Blockstream AMP | Smaller |
| **Rootstock (RSK)** | Strong (merge-mined) | Yes | Ethereum tooling reusable | Growing |
| **Stacks** | Strong (Bitcoin-native) | No (Clarity) | Would need rebuilding | Small |
| **Ethereum / Base** | Weak | Yes | Largest ecosystem | Best |

**Rootstock (RSK)** offers the most attractive compromise: Bitcoin-aligned with EVM compatibility, so existing Ethereum token standards and provider tooling can be reused.

**Liquid** is the most established Bitcoin security-token ecosystem with proven issuance (STOKR/AMP), but has a smaller ecosystem and lower liquidity.

**Ethereum / Base** has the strongest legal ecosystem and most tokenization providers, but is less appealing to Bitcoin-native audiences.

**Stacks** has strong Bitcoin-native branding but uses Clarity instead of Solidity, meaning most legal/tokenization tooling would need to be rebuilt.

## 9. Technical Considerations

### What needs to be built

The components required depend on the chosen release model (see Section 5) and blockchain:

| Component | Model 1 | Model 2 | Model 3 | Description |
|-----------|---------|---------|---------|-------------|
| Platform API integration | Required | Required | Required | Issuance, KYC whitelisting, token distribution |
| Stage-share mapping | Required | Required | Required | Map each Angor stage to corresponding share allocation |
| Investor token wallet | Required | Required | Required | Investor needs a wallet on the issuance chain |
| Escrow multisig/contracts | -- | Required | -- | Multisig or smart contract requiring founder + escrow |
| Escrow verification service | -- | Required | -- | Monitors Bitcoin chain, verifies stage claims, co-signs |
| Protocol extension (hashlock) | -- | -- | Required | Add hashlock to founder stage-claim spending path |
| HTLC escrow scripts | -- | -- | Required | Lock share tokens per stage with hashlock + investor key |
| Preimage monitoring service | -- | -- | Required | Watch Bitcoin chain for stage claims, extract preimages |

### Key constraints

- **Investors need both a Bitcoin wallet (Angor) and a wallet on the issuance chain** to participate. In the future, Angor could add native token support, but for an initial version, these are separate.
- **Liquid peg-in takes ~17 hours** (102 Bitcoin confirmations) if BTC needs to be converted to L-BTC. This does not affect the share token flow directly, but is relevant if any part of the integration requires L-BTC.
- **AMP Transfer Restricted assets require Blockstream Green wallet** -- this is a hard requirement from Blockstream for regulated securities on Liquid.

### Implementation path

1. **Start with Model 1 (Founder Release)** for a proof of concept on testnet. This validates the end-to-end flow with minimal engineering.
2. **Move to Model 2 (Escrow Release)** for production deployments. This adds an escrow co-signer for stronger guarantees without changes to Angor's Bitcoin-side protocol.
3. **Develop Model 3 (Atomic Release)** as the long-term solution. This requires the Angor protocol extension (hashlock on stage claims) and HTLC scripts, but eliminates all trust requirements.

## 10. Open Questions

1. **Legal structure of locked shares**: How does the SPV legally represent shares that are allocated but not yet released? Are they held in escrow by the SPV, or are they issued but transfer-locked?

2. **Burned/unreleased shares**: When an investor recovers Bitcoin early, what is the legal treatment of the corresponding unreleased shares? Are they burned (reducing total supply) or returned to the SPV's treasury?

3. **Platform compatibility with escrow scripts**: Can the tokenization platform's transfer-restriction model accommodate shares being held in on-chain scripts (multisig or HTLC escrow) rather than directly in investor wallets? This affects the feasibility of Models 2 and 3.

4. **Investor aggregation model**: Is there a legal structure that would allow a KYC-verified investor to aggregate capital from smaller participants via Angor, investing under their name? What are the regulatory implications? *(Future exploration.)*

5. **Multi-investor stages**: In Angor, a single stage may have contributions from many investors. The founder claims all of them at once. The share release mechanism needs to handle per-investor allocations correctly when the founder does a single stage claim.

6. **Minimum investment threshold**: Some platforms (e.g., STOKR) require high minimum investments (~100,000 USD). Given that Angor's ICCR mechanism provides built-in investor protection, could this minimum be reduced? The protocol-level safeguards may satisfy some of the risk concerns that high minimums are designed to address.

## 11. The Real Challenge

The primary challenge is not blockchain selection. The real challenge is finding a legal and regulatory framework that supports:

- **Retail investors** (not just qualified/high-net-worth)
- **Startup financing** (not just funds and RWAs)
- **Global participation**
- **Secondary trading**
- **Bitcoin-based fundraising**

The strongest positioning is:

> Angor provides Investor-Controlled Capital Release for startup financing, while a regulated issuance partner handles compliance, token issuance, investor onboarding, and secondary market requirements.

## 12. Next Steps

1. **Evaluate tokenization partners** -- prioritize Brickken and Tokeny for startup/retail focus; keep STOKR as an option for qualified-investor offerings on Liquid; consider Securitize for larger offerings requiring secondary trading.
2. **Select the issuance chain** -- Rootstock (RSK) for Bitcoin alignment with EVM compatibility; Liquid for STOKR-specific integrations; Ethereum/Base for broadest partner ecosystem.
3. **Agree on the initial release model** -- Model 1 (founder-trusted) is recommended for the proof of concept.
4. **Prototype Model 1** on testnet with the selected partner to validate the basic flow: Angor stage claim triggers token distribution.
5. **Prototype Model 3** (atomic HTLC) independently to validate the cross-chain release mechanism.
6. **Define the protocol extension** for adding hashlocks to Angor's stage-claim spending path (required for Model 3).
7. **Clarify the legal treatment** of locked/escrowed shares and the burned-share scenario with legal counsel.
8. **Evaluate the investor aggregation model** as a future expansion.

## Appendix: References

- **Angor Protocol**: [P2P Funding Protocol Specification](https://github.com/block-core/angor/blob/main/docs/p2p-funding-protocol.md)
- **Angor Website**: https://angor.io
- **STOKR**: https://stokr.io
- **Brickken**: https://brickken.com
- **Securitize**: https://securitize.io
- **Tokeny**: https://tokeny.com
- **Blockstream AMP**: https://docs.liquid.net/docs/blockstream-amp-overview
- **Liquid Network**: https://docs.liquid.net/docs/technical-overview
- **Blockstream Green**: https://blockstream.com/green/
- **Rootstock (RSK)**: https://rootstock.io
- **ERC-3643**: https://erc3643.org
