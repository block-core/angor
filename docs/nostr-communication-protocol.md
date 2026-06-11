# BCIP-0006 : Angor Nostr Communication Protocol

```
Number:  BCIP-0006
Title:   Angor Nostr Communication Protocol
Type:    Protocol
Status:  Draft
Authors: Dan Gershony, David Gershony
Created: 2025-06-09
```

## Abstract

This document specifies the Nostr event kinds, tags, and message formats used by the Angor P2P Funding Protocol ([P2P Funding Protocol](p2p-funding-protocol.md)) for decentralized communication between founders and investors. The protocol uses Nostr for project discovery, metadata publication, and the interactive investment handshake. It defines a custom event kind `3030` for project creation, leverages NIP-01 profiles and NIP-78 application-specific data for project metadata, and uses NIP-44 encrypted direct messages for the investment signature exchange.

## Motivation

The Angor protocol requires a decentralized communication layer for founders and investors to coordinate without relying on centralized servers. Nostr provides a censorship-resistant relay network suitable for publishing project metadata and exchanging investment messages. This document formalizes the Nostr event structures to ensure interoperability between different Angor client implementations.

## Specification

### Terms

- **founder** : creator of the funding project.
- **investor** : participant who locks bitcoin into a project's time-locked stages.
- **seeder** (lead investor) : an early investor who commits a secret, enabling penalty-free recovery for regular investors.
- **projectIdentifier** : the unique on-chain identifier for a project (e.g., `angor1q...`).

### Event kinds

The protocol uses the following Nostr event kinds:

| Kind | Standard | Usage |
|------|----------|-------|
| 0 | NIP-01 Metadata | Project profile identity (name, description, avatar) |
| 4 | NIP-04/NIP-44 Encrypted DM | Investment messaging (offers, approvals, notifications) |
| 3030 | Custom (this spec) | Project creation event |
| 10002 | NIP-65 Relay List Metadata | Relay list for the project identity |
| 30078 | NIP-78 Application-Specific Data | Project content, FAQ, members, media |

### Event `3030`: Project creation

The founder publishes a kind `3030` event to announce a new project on the Nostr network. The event ID is then committed on-chain in the founder's initialization transaction via `OP_RETURN`, binding the Nostr metadata to the blockchain.

This is an addressable (replaceable) event, keyed by the `d` tag.

**Tags:**

```json
[
  ["d", "<projectIdentifier>"]
]
```

**Content:**

The content is a JSON-serialized `ProjectInfo` object. Property names use camelCase. Dates are serialized as Unix timestamps (seconds). Null values are omitted.

#### Version 1 (legacy) content

```json
{
    "founderKey": "<string, Bitcoin compressed public key of the founder (hex)>",
    "founderRecoveryKey": "<string, Bitcoin compressed public key for recovery multisig (hex)>",
    "projectIdentifier": "<string, unique project identifier (bech32, angor1...)>",
    "nostrPubKey": "<string, Nostr public key for the project (hex)>",
    "startDate": "<int, Unix timestamp of campaign start>",
    "penaltyDays": "<int, number of days funds are locked during penalty recovery>",
    "expiryDate": "<int, Unix timestamp when remaining funds can be reclaimed>",
    "targetAmount": "<int, target funding amount in satoshis>",
    "stages": [
        {
            "amountToRelease": "<decimal, percentage of total to release at this stage>",
            "releaseDate": "<int, Unix timestamp of stage release>"
        }
    ],
    "projectSeeders": {
        "threshold": "<int, number of seeder secrets required for penalty-free recovery>",
        "secretHashes": ["<string, hex-encoded hash of seeder secret>"]
    }
}
```

#### Version 2 content

Version 2 adds support for multiple project types, dynamic stages, and penalty thresholds. All version 1 fields remain, with the following additions:

```json
{
    "version": 2,
    "projectType": "<int, 0=Invest, 1=Fund, 2=Subscribe>",
    "founderKey": "<string, Bitcoin compressed public key of the founder (hex)>",
    "founderRecoveryKey": "<string, Bitcoin compressed public key for recovery multisig (hex)>",
    "projectIdentifier": "<string, unique project identifier (bech32, angor1...)>",
    "nostrPubKey": "<string, Nostr public key for the project (hex)>",
    "networkName": "<string, blockchain network name (e.g. 'Bitcoin', 'BitcoinTestnet', 'angornet')>",
    "startDate": "<int, Unix timestamp of campaign start (required for Invest, optional otherwise)>",
    "endDate": "<int, Unix timestamp of campaign end (required for Invest, unused otherwise)>",
    "penaltyDays": "<int, penalty lock days (Invest/Fund) or 0 (Subscribe)>",
    "expiryDate": "<int, Unix timestamp when remaining funds can be reclaimed>",
    "targetAmount": "<int, target amount in satoshis (required for Invest, optional otherwise)>",
    "penaltyThreshold": "<int|null, optional threshold in satoshis (Fund only)>",
    "stages": [
        {
            "amountToRelease": "<decimal, percentage>",
            "releaseDate": "<int, Unix timestamp>"
        }
    ],
    "projectSeeders": {
        "threshold": "<int>",
        "secretHashes": ["<string>"]
    },
    "dynamicStagePatterns": [
        {
            "patternId": "<int, 0-255, unique pattern identifier>",
            "name": "<string, display name>",
            "description": "<string, description>",
            "frequency": "<int, 0=Weekly, 1=Biweekly, 2=Monthly, 3=BiMonthly, 4=Quarterly>",
            "stageCount": "<int, number of stages>",
            "payoutDayType": "<int, 0=FromStartDate, 1=SpecificDayOfMonth, 2=SpecificDayOfWeek>",
            "payoutDay": "<int, day value (context depends on payoutDayType)>",
            "amount": "<int|null, fixed amount in satoshis (mandatory for Subscribe, optional for Fund)>"
        }
    ]
}
```

**Field requirements by project type:**

| Field | Invest | Fund | Subscribe |
|-------|--------|------|-----------|
| `version` | Required (2) | Required (2) | Required (2) |
| `projectType` | 0 | 1 | 2 |
| `startDate` | Required | Optional | Optional |
| `endDate` | Required | Omitted | Omitted |
| `targetAmount` | Required (> 0) | Optional (0 if unused) | Optional (0 if unused) |
| `penaltyDays` | Required (10-365) | Required (10-365) | 0 |
| `penaltyThreshold` | Omitted | Optional | Omitted |
| `stages` | Required (fixed list) | Empty list | Empty list |
| `dynamicStagePatterns` | Empty list | Required (at least one) | Required (at least one, each with `amount` set) |

**Version detection:**

- If `version` is absent or `< 2`, the event is treated as version 1 and the project type is always `Invest`.
- If `version >= 2`, the `projectType` field determines the project type.

#### Example: Invest project (version 2)

```json
{
    "version": 2,
    "projectType": 0,
    "founderKey": "03309b10a078ca8e718693d241b3a57ff31f5aabcd7ec53089bd143a57036332ea",
    "founderRecoveryKey": "03cc053e8fd5bd6cea509df6c58d0f6fe16d9f4bed20a7b15a9447dbd9d6a52d9a",
    "projectIdentifier": "angor1q9j9jvmqwll00gnzf8thu9lrar65ccpu4z5np6j",
    "nostrPubKey": "5a05cc7a38e3875ee3242e5f068304a36c9609c4c15f5baaf7d75e8fcdfe36c5",
    "networkName": "Bitcoin",
    "startDate": 1738886400,
    "endDate": 1749254400,
    "penaltyDays": 90,
    "expiryDate": 1780790400,
    "targetAmount": 5000000000,
    "stages": [
        {"amountToRelease": 25, "releaseDate": 1738886400},
        {"amountToRelease": 25, "releaseDate": 1741564800},
        {"amountToRelease": 25, "releaseDate": 1744156800},
        {"amountToRelease": 25, "releaseDate": 1746835200}
    ],
    "projectSeeders": {"threshold": 0, "secretHashes": []}
}
```

#### Example: Fund project (version 2)

```json
{
    "version": 2,
    "projectType": 1,
    "founderKey": "03309b10a078ca8e718693d241b3a57ff31f5aabcd7ec53089bd143a57036332ea",
    "founderRecoveryKey": "03cc053e8fd5bd6cea509df6c58d0f6fe16d9f4bed20a7b15a9447dbd9d6a52d9a",
    "projectIdentifier": "angor1q9j9jvmqwll00gnzf8thu9lrar65ccpu4z5np6j",
    "nostrPubKey": "5a05cc7a38e3875ee3242e5f068304a36c9609c4c15f5baaf7d75e8fcdfe36c5",
    "networkName": "Bitcoin",
    "penaltyDays": 60,
    "expiryDate": 1798761600,
    "targetAmount": 0,
    "penaltyThreshold": 1000000,
    "stages": [],
    "projectSeeders": {"threshold": 3, "secretHashes": ["a1b2c3...","d4e5f6...","789abc..."]},
    "dynamicStagePatterns": [
        {
            "patternId": 0,
            "name": "6-Month Monthly",
            "description": "6 monthly payments on the 1st of each month",
            "frequency": 2,
            "stageCount": 6,
            "payoutDayType": 1,
            "payoutDay": 1
        },
        {
            "patternId": 1,
            "name": "12-Month Monthly",
            "description": "12 monthly payments on the 1st of each month",
            "frequency": 2,
            "stageCount": 12,
            "payoutDayType": 1,
            "payoutDay": 1
        }
    ]
}
```

#### Example: Subscribe project (version 2)

```json
{
    "version": 2,
    "projectType": 2,
    "founderKey": "03309b10a078ca8e718693d241b3a57ff31f5aabcd7ec53089bd143a57036332ea",
    "founderRecoveryKey": "03cc053e8fd5bd6cea509df6c58d0f6fe16d9f4bed20a7b15a9447dbd9d6a52d9a",
    "projectIdentifier": "angor1q9j9jvmqwll00gnzf8thu9lrar65ccpu4z5np6j",
    "nostrPubKey": "5a05cc7a38e3875ee3242e5f068304a36c9609c4c15f5baaf7d75e8fcdfe36c5",
    "networkName": "Bitcoin",
    "penaltyDays": 0,
    "expiryDate": 1798761600,
    "targetAmount": 0,
    "stages": [],
    "projectSeeders": {"threshold": 0, "secretHashes": []},
    "dynamicStagePatterns": [
        {
            "patternId": 0,
            "name": "6-Month Monthly",
            "description": "6 monthly payments on the 1st of each month",
            "frequency": 2,
            "stageCount": 6,
            "payoutDayType": 1,
            "payoutDay": 1,
            "amount": 500000
        }
    ]
}
```

### Event `30078`: Project metadata (NIP-78)

Project metadata beyond the core `ProjectInfo` is stored in NIP-78 application-specific data events (`kind: 30078`). Each metadata category is a separate replaceable event, keyed by the `d` tag. These events are published under the project's Nostr identity (derived from the founder key via [P2P Funding Protocol](p2p-funding-protocol.md) key derivation).

| d-tag | Content format | Description |
|-------|----------------|-------------|
| `angor:project` | Plain text string | Project description/about content |
| `angor:faq` | JSON array | FAQ items |
| `angor:members` | JSON object | Team member pubkeys |
| `angor:media` | JSON array | Media gallery items |

**Tags (all metadata events):**

```json
[
  ["d", "<d-tag value>"]
]
```

#### `angor:project`

Free-text project description. May contain HTML or markdown.

```
This is a detailed description of the project goals, roadmap, and team.
```

#### `angor:faq`

```json
[
    {"question": "What is this project?", "answer": "A decentralized..."},
    {"question": "How do I invest?", "answer": "Use the Angor app..."}
]
```

#### `angor:members`

```json
{
    "pubkeys": [
        "npub1abc...",
        "npub1def..."
    ]
}
```

#### `angor:media`

```json
[
    {"url": "https://example.com/image1.png", "type": "image"},
    {"url": "https://example.com/video1.mp4", "type": "video"}
]
```

### Event `0`: Project profile (NIP-01)

The project's Nostr identity publishes a standard NIP-01 `kind: 0` profile metadata event containing:

```json
{
    "name": "<string, project name>",
    "display_name": "<string, display name>",
    "about": "<string, short project description>",
    "website": "<string, project URL>",
    "picture": "<string, avatar URL>",
    "banner": "<string, banner image URL>",
    "nip05": "<string, NIP-05 identifier>",
    "lud16": "<string, Lightning address>"
}
```

### Event `10002`: Relay list (NIP-65)

The project identity publishes a `kind: 10002` event listing preferred relays, following NIP-65:

```json
{
    "tags": [
        ["r", "wss://relay1.example.com"],
        ["r", "wss://relay2.example.com"]
    ]
}
```

### Campaign browser

Campaign browser software should be entirely client-side, either as a stand-alone app or as a purely frontend webpage.

To discover projects, the browser should:

1. Query relays for `kind: 3030` events to find published projects.
2. Use the `nostrPubKey` field in the event content to query for `kind: 0` (NIP-01 profile) and `kind: 30078` (NIP-78 metadata) events authored by that pubkey.
3. Query a blockchain indexer to find the founder's initialization transaction that commits to the Nostr event ID via `OP_RETURN`.
4. Verify the on-chain commitment matches the Nostr event to ensure metadata integrity.

These event kinds can be fetched in a single relay subscription:

```json
{
    "kinds": [3030, 30078, 0],
    "authors": ["<nostrPubKey>"]
}
```

### Investment messaging (Encrypted DMs)

All investment communication between founders and investors uses Nostr encrypted direct messages. The `subject` tag identifies the message type.

**Encryption:**

- Messages SHOULD be encrypted with NIP-44 (ECDH + HKDF + ChaCha20-Poly1305).
- Implementations SHOULD support decrypting NIP-04 (AES-CBC) messages for backward compatibility. NIP-04 ciphertext is detected by the presence of `?iv=` in the encrypted payload.
- Cancellation notifications MAY be sent unencrypted as they contain only public identifiers.

**Tags:**

All DM events include:

```json
[
  ["p", "<recipient pubkey (hex)>"],
  ["subject", "<message type>"]
]
```

Reply messages (founder to investor) additionally include an `e` tag referencing the original request:

```json
[
  ["p", "<recipient pubkey (hex)>"],
  ["e", "<original request event ID>"],
  ["subject", "<message type>"]
]
```

#### Message types

##### `Investment offer` (Investor --> Founder)

The investor sends the unsigned investment transaction to the founder, requesting recovery signatures.

**Content** (NIP-44 encrypted):

```json
{
    "projectIdentifier": "<string, project identifier>",
    "investmentTransactionHex": "<string, hex-encoded unsigned investment transaction>"
}
```

##### `Re:Investment offer` (Founder --> Investor)

The founder returns recovery signatures for the investment transaction.

**Tags** include `e` tag referencing the original `Investment offer` event.

**Content** (NIP-44 encrypted):

```json
{
    "projectIdentifier": "<string, project identifier>",
    "signatures": [
        {"stageIndex": 0, "signature": "<string, hex-encoded Schnorr signature>"},
        {"stageIndex": 1, "signature": "<string, hex-encoded Schnorr signature>"}
    ],
    "timeOfSignatureRequest": "<int, Unix timestamp>",
    "signatureRequestEventId": "<string, event ID of the original request>",
    "signatureType": 1
}
```

Where `signatureType`: `0` = Release, `1` = Recovery.

##### `Investment completed` (Investor --> Founder)

Notification that the investment transaction has been broadcast on-chain.

**Content** (NIP-44 encrypted):

```json
{
    "projectIdentifier": "<string, project identifier>",
    "transactionId": "<string, hex-encoded Bitcoin transaction ID>"
}
```

##### `Investment canceled` (Investor --> Founder)

Notification that the investor has canceled the investment request.

**Content** (unencrypted or NIP-44 encrypted):

```json
{
    "projectIdentifier": "<string, project identifier>",
    "requestEventId": "<string, event ID of the original request>"
}
```

##### `Release transaction signatures` (Founder --> Investor)

The founder sends release signatures for unfunded coin release (when an Invest project does not meet its target).

**Tags** include `e` tag referencing the original investment request event.

**Content** (NIP-44 encrypted):

```json
{
    "projectIdentifier": "<string, project identifier>",
    "signatures": [
        {"stageIndex": 0, "signature": "<string, hex-encoded Schnorr signature>"},
        {"stageIndex": 1, "signature": "<string, hex-encoded Schnorr signature>"}
    ],
    "timeOfSignatureRequest": "<int, Unix timestamp>",
    "signatureRequestEventId": "<string, event ID of the original request>",
    "signatureType": 0
}
```

#### Message summary

| Subject tag | Direction | Content | Encrypted |
|-------------|-----------|---------|-----------|
| `"Investment offer"` | Investor --> Founder | Investment transaction hex | NIP-44 |
| `"Re:Investment offer"` | Founder --> Investor | Recovery signatures | NIP-44 |
| `"Investment completed"` | Investor --> Founder | Transaction ID | NIP-44 |
| `"Investment canceled"` | Investor --> Founder | Request event ID | Optional |
| `"Release transaction signatures"` | Founder --> Investor | Release signatures | NIP-44 |

#### Querying messages

To retrieve investment messages for a founder, clients use these filters:

**Incoming messages (to founder):**

```json
{
    "kinds": [4],
    "#p": ["<founder nostr pubkey>"],
    "since": "<timestamp>"
}
```

**Outgoing messages (from founder):**

```json
{
    "kinds": [4],
    "authors": ["<founder nostr pubkey>"],
    "since": "<timestamp>"
}
```

**Specific signature response (investor waiting for founder approval):**

```json
{
    "kinds": [4],
    "authors": ["<project nostr pubkey>"],
    "#p": ["<investor nostr pubkey>"],
    "#e": ["<request event ID>"],
    "since": "<request sent timestamp>",
    "limit": 1
}
```

Message type dispatch is done client-side by inspecting the `subject` tag value.

### Below-threshold investments

For Fund projects with a penalty threshold, when an investment amount is at or below the threshold, the investor does not need recovery signatures. In this case the investor skips the `Investment offer` / `Re:Investment offer` exchange and directly broadcasts the transaction, then sends only an `Investment completed` notification.

See [P2P Funding Protocol](p2p-funding-protocol.md) for the full penalty threshold specification.

## References

- [Angor - P2P Funding Protocol](p2p-funding-protocol.md)
- [NIP-01: Basic protocol flow description](https://github.com/nostr-protocol/nips/blob/master/01.md)
- [NIP-04: Encrypted Direct Message](https://github.com/nostr-protocol/nips/blob/master/04.md)
- [NIP-44: Versioned Encryption](https://github.com/nostr-protocol/nips/blob/master/44.md)
- [NIP-65: Relay List Metadata](https://github.com/nostr-protocol/nips/blob/master/65.md)
- [NIP-78: Application-specific data](https://github.com/nostr-protocol/nips/blob/master/78.md)
- [Angor Reference Implementation](https://github.com/block-core/angor)
