# Liquid Asset Issuance via STOKR — Integration Plan for Angor

## Overview

When a founder claims Bitcoin from a staged investment in Angor, they should release security tokens on the Liquid Network to investors, proportional to each investor's contribution. STOKR handles the legal and regulatory side of asset issuance, while Angor handles the technical creation and release of assets.

## What STOKR Is

STOKR is a **Luxembourg-regulated digital securities platform** that handles the legal and compliance side of tokenizing real-world assets as security tokens on the Liquid Network. They offer two services:

- **End-to-End Tokenization** — STOKR handles everything: legal structuring, regulatory compliance (EU/Luxembourg), investor onboarding (KYC/AML), and the token issuance itself.
- **Tokenization-as-a-Service (TaaS)** — STOKR provides the legal/regulatory wrapper while the integrator handles the technical side.

STOKR is a Liquid Federation member and has issued major assets on Liquid, including BMN2 (Blockstream Mining Notes, ~$877M), CMSTR (MicroStrategy tracker, ~$7.4M), and Aquarius Fund (~$15M).

### What STOKR Handles (So Angor Doesn't Have To)

- Legal entity structuring (SPV, token offering docs)
- Securities regulation compliance (EU MiFID II, Luxembourg CSSF)
- KYC/AML for investors
- Prospectus/offering memorandum
- Ongoing regulatory reporting
- Secondary market compliance

## The Technical Stack: Blockstream AMP

Blockstream AMP (Asset Management Platform) is the API layer for issuing and managing assets on Liquid. It provides:

- **REST API** for issuing, distributing, reissuing, and burning assets
- **Transfer Restricted assets** — issuer must whitelist recipients (KYC'd via STOKR) before they can receive tokens
- **Issuer Tracked assets** — anyone with a Blockstream Green "Managed Assets" account can receive them
- **Ownership tracking** — AMP tracks balances by Green Account ID (GAID), enabling proof-of-balance/transfer for regulators
- **Issuer Authorization Override** — custom transfer logic via an API endpoint you define
- **Testnet** available at `https://amp-test.blockstream.com/api/`
- **C# SDK** — straightforward `HttpClient`-based integration

For securities (which is what Angor projects would issue), **Transfer Restricted** is the correct AMP asset type.

## Integration Approaches

### Approach 1: AMP API-Driven Distribution (Trust-Based)

The AMP API is issuer-authenticated. The founder holds AMP credentials and a reissuance token in their Liquid wallet.

**Flow:**
1. Founder holds AMP credentials + reissuance token in their Liquid wallet
2. Founder claims BTC stage on Angor
3. Founder (or Angor acting on their behalf) calls AMP API with their auth token to distribute tokens to investor GAIDs
4. Investor sees tokens appear in their Blockstream Green wallet

**Problem:** This is custodial/trust-based. The investor trusts that the founder will actually call the API after claiming BTC. There is no atomic link between the Bitcoin claim and the Liquid token release.

### Approach 2: Cross-Chain HTLC (Trustless, Raw Liquid Assets)

Uses a hashlock (HTLC-style) pattern across Bitcoin and Liquid — essentially an atomic swap.

**Flow:**
1. Founder issues tokens directly on Liquid (raw `issueasset` via Elements/LWK)
2. Founder locks tokens in a Liquid HTLC script tied to a hash H
3. The Bitcoin stage-claim transaction is constructed so that spending it requires revealing the preimage of H
4. When founder claims BTC (reveals preimage on-chain), investor watches Bitcoin, extracts the preimage
5. Investor uses that preimage to claim tokens from the Liquid HTLC

**Advantages:**
- Truly atomic — no trust required
- The Bitcoin unlock reveals the secret that allows the investor to claim tokens
- Aligns with Angor's existing timelock-based contract model

**Tradeoffs:**
- Loses AMP's transfer restrictions, ownership tracking, and STOKR's regulatory wrapper
- Compliance must be handled separately
- Requires Liquid script-level integration (hashlocks via `OP_SHA256 OP_EQUAL`)

### Approach 3: Hybrid — AMP for Compliance, Covenant for Release

Combines the regulatory benefits of STOKR/AMP with trustless release mechanics.

**Flow:**
1. STOKR/AMP handles issuance, KYC, whitelisting
2. Tokens are distributed to a Liquid escrow address (not directly to the investor)
3. The escrow uses a covenant or multisig where the release condition is tied to the Bitcoin stage claim (hashlock)
4. AMP tracks ownership after the investor claims from escrow

**Advantages:**
- Trustless release tied to Bitcoin claim
- Retains STOKR regulatory compliance and AMP tracking

**Tradeoffs:**
- Most complex to implement
- AMP's Transfer Restricted assets go through Green's co-signing, which may conflict with raw Liquid scripts in the escrow

## What Angor Needs to Build

Regardless of approach, the following components are needed:

1. **Liquid wallet integration** — via LWK (Liquid Wallet Kit, Rust bindings) or GDK (Green Development Kit, C bindings)
2. **AMP API client** (if using Approach 1 or 3) — REST calls for asset creation, assignment, distribution, user management
3. **Mapping layer** — connect Angor's Bitcoin-side stage claims to Liquid-side token distributions in `FounderAppService`
4. **Investor onboarding** — collect the investor's GAID (from Blockstream Green wallet) during the investment flow

## Key Constraints

- **Investors need Blockstream Green wallet** for AMP Transfer Restricted assets (hard requirement)
- **Peg-in takes ~17 hours** (102 Bitcoin confirmations) if BTC needs to be converted to LBTC for Liquid operations
- **Liquid supports hashlocks** (`OP_SHA256 OP_EQUAL`) in scripts, making cross-chain HTLCs technically feasible today
- **AMP testnet** at `https://amp-test.blockstream.com/api/` is available for development

## Decision Matrix

| Criteria                     | Approach 1 (AMP API) | Approach 2 (HTLC) | Approach 3 (Hybrid) |
|------------------------------|----------------------|--------------------|---------------------|
| Trustlessness                | No                   | Yes                | Yes                 |
| Regulatory compliance        | Yes (STOKR/AMP)     | No (manual)        | Yes (STOKR/AMP)    |
| Implementation complexity    | Low                  | Medium             | High                |
| Investor wallet requirement  | Green only           | Any Liquid wallet  | Green only          |
| Atomic BTC↔Token release     | No                   | Yes                | Yes                 |

## Recommended Next Steps

1. Reach out to STOKR about their TaaS offering to understand legal structure, pricing, and API automation capabilities
2. Prototype Approach 2 (HTLC) on Liquid testnet to validate the cross-chain atomic pattern
3. Evaluate whether AMP's co-signing model in Green can accommodate escrow/covenant scripts (Approach 3 feasibility)
4. Decide on approach based on regulatory requirements vs. trustlessness priority

## References

- STOKR: https://stokr.io
- Blockstream AMP docs: https://docs.liquid.net/docs/blockstream-amp-overview
- Liquid technical overview: https://docs.liquid.net/docs/technical-overview
- LWK (Liquid Wallet Kit): https://docs.liquid.net/docs/lwk-overview-and-examples
- Liquid Asset Registry: https://docs.liquid.net/docs/blockstream-liquid-asset-registry
- AMP API spec: https://docs.liquid.net/docs/blockstream-amp-api-specification
- AMP testnet: https://amp-test.blockstream.com/api/
