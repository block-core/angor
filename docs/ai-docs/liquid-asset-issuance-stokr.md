# Security Token Issuance -- Integration Plan for Angor

## Overview

Angor enables **Investor-Controlled Capital Release (ICCR)** -- a Bitcoin-native fundraising model where investors retain control over the staged release of capital after committing funds. When a founder claims Bitcoin from a staged investment, the protocol should release security tokens to investors proportional to each investor's contribution.

This document evaluates how Angor can integrate with regulated tokenization platforms and blockchain networks to issue security tokens tied to staged Bitcoin releases.

## Core Concept: Investor-Controlled Capital Release (ICCR)

Traditional startup investing transfers control of funds to founders immediately:

```
Traditional:  Investor -> Capital committed -> Founder controls funds
Angor:        Investor -> Capital committed -> Investor controls capital release
```

This is more than milestone funding. It is a new investment primitive that reduces trust requirements on founders while maintaining access to capital. The model is particularly suited to startup and project financing, where investors want stronger protection and accountability.

> Angor is a Bitcoin-native fundraising protocol that enables Investor-Controlled Capital Release (ICCR), reducing founder trust requirements while preserving startup access to funding.

The security token integration extends ICCR to regulated securities: shares are progressively unlocked as each stage's timelock expires and the founder claims the corresponding Bitcoin. Investors who exit early forfeit the share tokens for recovered stages.

## What a Tokenization Partner Handles

Angor needs a regulated issuance partner to handle the legal and compliance side. Regardless of which partner is chosen, their role covers:

- Legal entity structuring (SPV, token offering docs)
- Securities regulation compliance (jurisdiction-specific)
- KYC/AML for investors
- Prospectus/offering memorandum
- Ongoing regulatory reporting
- Secondary market compliance
- Token issuance on the chosen blockchain

## Tokenization Platform Options

### STOKR (Liquid Network)

A **Luxembourg-regulated digital securities platform** that issues tokens on the Liquid Network via Blockstream AMP.

**Strengths:**
- End-to-end tokenization or Tokenization-as-a-Service (TaaS)
- Liquid Federation member with proven issuance track record (BMN2 ~$877M, CMSTR ~$7.4M)
- Bitcoin-native ecosystem (Liquid is a Bitcoin sidechain)
- Blockstream AMP provides REST API for issuance, distribution, and ownership tracking

**Limitations:**
- Primarily focused on funds, RWAs, and structured investment products
- Serves qualified/high-net-worth investors (typically 100,000 USD minimum)
- Less focused on startup financing and retail investors
- Transfer Restricted assets require Blockstream Green wallet

**Fit for Angor:** STOKR's current market focus is funds and RWAs, where investor-controlled release is less applicable because capital is managed continuously by fund managers. The open question is whether STOKR views ICCR as unsuitable only for their current market or for startup financing broadly. STOKR remains a viable option if the offering targets qualified investors and the Liquid Network.

### Brickken (Ethereum)

Most closely aligned with startup and SME fundraising.

**Strengths:**
- Focus on SMEs and private companies
- Tokenization infrastructure with investor onboarding
- Cap table management
- More retail-oriented than STOKR

**Limitations:**
- Ethereum-focused
- Limited secondary market liquidity

**Fit for Angor:** Strong alignment with Angor's startup financing use case. Retail orientation could enable broader investor participation.

### Securitize (Multi-chain)

One of the largest regulated tokenization providers.

**Strengths:**
- Strong regulatory infrastructure (SEC-registered transfer agent)
- Retail and institutional experience
- Secondary trading capabilities
- Multi-chain support

**Limitations:**
- Expensive
- More focused on institutional-grade offerings
- Potentially overkill for early-stage startup fundraising

**Fit for Angor:** Best suited if Angor targets larger offerings or needs institutional credibility. Secondary trading support is a significant advantage.

### Tokeny (Ethereum, ERC-3643)

Compliance-focused platform built around the ERC-3643 security token standard.

**Strengths:**
- Strong compliance framework (ERC-3643 is specifically designed for regulated securities)
- Flexible architecture
- Allows external platforms to own more of the user experience

**Limitations:**
- Requires legal issuance partners (Tokeny provides tooling, not the legal wrapper)
- Ethereum-centric

**Fit for Angor:** Good option if Angor wants to control more of the user experience while relying on a compliance layer. Requires pairing with a separate legal/issuance partner.

## Blockchain Considerations

The blockchain for token issuance should ideally support both Bitcoin alignment and access to existing legal/tokenization infrastructure. The blockchain itself is likely secondary to the legal and regulatory framework.

| Blockchain | Bitcoin Alignment | EVM Compatible | Tokenization Ecosystem | Liquidity |
|------------|-------------------|----------------|------------------------|-----------|
| **Liquid** | Native (sidechain) | No | STOKR, Blockstream AMP | Smaller |
| **Rootstock (RSK)** | Strong (merge-mined) | Yes | Ethereum tooling reusable | Growing |
| **Stacks** | Strong (Bitcoin-native) | No (Clarity) | Would need rebuilding | Small |
| **Ethereum / Base** | Weak | Yes | Largest ecosystem | Best |

**Rootstock (RSK)** offers the most attractive compromise: Bitcoin-aligned ecosystem with EVM compatibility, allowing existing Ethereum token standards and legal/tokenization provider tooling to be reused.

**Liquid** is the most established Bitcoin security-token ecosystem but has a smaller overall ecosystem and lower liquidity.

**Ethereum / Base** has the strongest legal ecosystem and most tokenization providers, but is less appealing to Bitcoin-native audiences.

## The Technical Stack: Blockstream AMP (Liquid Path)

If the Liquid Network is chosen, Blockstream AMP (Asset Management Platform) is the API layer for issuing and managing assets. It provides:

- **REST API** for issuing, distributing, reissuing, and burning assets
- **Transfer Restricted assets** -- issuer must whitelist recipients (KYC'd via partner) before they can receive tokens
- **Issuer Tracked assets** -- anyone with a Blockstream Green "Managed Assets" account can receive them
- **Ownership tracking** -- AMP tracks balances by Green Account ID (GAID)
- **Issuer Authorization Override** -- custom transfer logic via an API endpoint you define
- **Testnet** available at `https://amp-test.blockstream.com/api/`
- **C# SDK** -- straightforward `HttpClient`-based integration

For securities, **Transfer Restricted** is the correct AMP asset type.

## Integration Approaches

These approaches are blockchain-agnostic in concept, though the examples reference Liquid. The same patterns (API-driven, HTLC, hybrid) apply on any chain that supports scripting or smart contracts.

### Approach 1: API-Driven Distribution (Trust-Based)

The issuance platform API is issuer-authenticated. The founder holds credentials and triggers token distribution after claiming Bitcoin.

**Flow:**
1. Founder holds issuance platform credentials
2. Founder claims BTC stage on Angor
3. Founder (or Angor acting on their behalf) calls the platform API to distribute tokens to investor wallets
4. Investor sees tokens appear in their wallet

**Problem:** This is trust-based. The investor trusts that the founder will actually call the API after claiming BTC. There is no atomic link between the Bitcoin claim and the token release.

### Approach 2: Cross-Chain HTLC (Trustless)

Uses a hashlock (HTLC-style) pattern across Bitcoin and the token chain -- essentially an atomic swap.

**Flow:**
1. Founder issues tokens on the token chain
2. Founder locks tokens in an HTLC script tied to a hash H
3. The Bitcoin stage-claim transaction requires revealing the preimage of H
4. When founder claims BTC (reveals preimage on-chain), investor extracts the preimage
5. Investor uses that preimage to claim tokens from the HTLC

**Advantages:**
- Truly atomic -- no trust required
- Aligns with Angor's existing timelock-based contract model

**Tradeoffs:**
- May lose platform-level transfer restrictions and ownership tracking
- Compliance must be handled separately
- Requires script-level integration on the token chain

### Approach 3: Hybrid -- Platform for Compliance, Covenant for Release

Combines regulatory benefits of a tokenization platform with trustless release mechanics.

**Flow:**
1. Tokenization platform handles issuance, KYC, whitelisting
2. Tokens are distributed to an escrow address (not directly to the investor)
3. The escrow uses a covenant or multisig where the release condition is tied to the Bitcoin stage claim (hashlock)
4. Platform tracks ownership after the investor claims from escrow

**Advantages:**
- Trustless release tied to Bitcoin claim
- Retains regulatory compliance and ownership tracking

**Tradeoffs:**
- Most complex to implement
- Platform-specific constraints may conflict with escrow scripts

## What Angor Needs to Build

Regardless of approach and chain, the following components are needed:

1. **Token chain wallet integration** -- via the appropriate SDK for the chosen chain (LWK for Liquid, web3 libraries for EVM chains)
2. **Platform API client** (if using Approach 1 or 3) -- REST calls for asset creation, assignment, distribution
3. **Mapping layer** -- connect Angor's Bitcoin-side stage claims to token-side distributions in `FounderAppService`
4. **Investor onboarding** -- collect the investor's token chain wallet address during the investment flow

## Key Constraints

- **Investors need a wallet on the token chain** in addition to their Bitcoin wallet (Angor)
- **Liquid peg-in takes ~17 hours** (102 Bitcoin confirmations) if using Liquid and BTC needs conversion to L-BTC
- **Liquid supports hashlocks** (`OP_SHA256 OP_EQUAL`), making cross-chain HTLCs feasible today
- **EVM chains** support HTLCs via smart contracts, with broader tooling and developer ecosystem

## Decision Matrix

| Criteria | Approach 1 (API) | Approach 2 (HTLC) | Approach 3 (Hybrid) |
|---|---|---|---|
| Trustlessness | No | Yes | Yes |
| Regulatory compliance | Yes (via platform) | No (manual) | Yes (via platform) |
| Implementation complexity | Low | Medium | High |
| Investor wallet requirement | Platform-specific | Any compatible wallet | Platform-specific |
| Atomic BTC-to-Token release | No | Yes | Yes |

## Recommended Next Steps

1. **Evaluate tokenization partners** -- prioritize Brickken and Tokeny for startup/retail focus; keep STOKR as an option for qualified-investor offerings on Liquid
2. **Select the token chain** -- Rootstock (RSK) offers the best Bitcoin alignment with EVM compatibility; Liquid is viable for STOKR-specific integrations
3. **Prototype Approach 1** on testnet with the selected partner to validate the end-to-end flow
4. **Prototype Approach 2** (HTLC) independently to validate the cross-chain atomic pattern
5. **Define the legal and regulatory framework** -- identify which jurisdictions and investor types (retail vs. qualified) the initial offering will target

## The Real Challenge

The primary challenge for Angor is not blockchain selection. The real challenge is finding a legal and regulatory framework that supports:

- Retail investors (not just qualified/high-net-worth)
- Startup financing (not just funds and RWAs)
- Global participation
- Secondary trading
- Bitcoin-based fundraising

The strongest positioning is:

> Angor provides Investor-Controlled Capital Release for startup financing, while a regulated issuance partner handles compliance, token issuance, investor onboarding, and secondary market requirements.

## References

- Angor: https://angor.io
- STOKR: https://stokr.io
- Brickken: https://brickken.com
- Securitize: https://securitize.io
- Tokeny: https://tokeny.com
- Blockstream AMP docs: https://docs.liquid.net/docs/blockstream-amp-overview
- Liquid technical overview: https://docs.liquid.net/docs/technical-overview
- LWK (Liquid Wallet Kit): https://docs.liquid.net/docs/lwk-overview-and-examples
- Liquid Asset Registry: https://docs.liquid.net/docs/blockstream-liquid-asset-registry
- AMP API spec: https://docs.liquid.net/docs/blockstream-amp-api-specification
- AMP testnet: https://amp-test.blockstream.com/api/
- Rootstock (RSK): https://rootstock.io
- ERC-3643: https://erc3643.org
