# NIP-3030 (TBD)

Decentralized Crowdfunding
---

`draft` `optional`

Implemented in [Angor](https://github.com/block-core/angor), [Angor Hub](https://github.com/block-core/angor-hub) and [Angor Profile](https://github.com/block-core/angor-profile).

## Terms

- `founder` - creator of the crowdfunding campaign
- `investor` - investor of the crowdfunding campaign
- `seeder` (lead investor) - an early investor who commits a secret, enabling penalty-free recovery for regular investors
- `projectIdentifier` - the unique identifier of the project on the specific implementation of the protocol

## Decentralized Crowdfunding Protocol

The protocol utilizes the Nostr network to create a decentralized crowdfunding platform. The protocol allows founders to create crowdfunding campaigns and investors to invest in them. The protocol is relying on standard Nostr events for the majority of metadata, except for the specific events defined in this NIP.

The protocol supports three project types:

| Type | Value | Description |
|------|-------|-------------|
| **Invest** | 0 | Fixed stages with investment window and target amount. Penalty applies. |
| **Fund** | 1 | Dynamic stages, no investment window, optional target. Penalty applies (with optional threshold). |
| **Subscribe** | 2 | Dynamic stages, no investment window, fixed subscription price. No penalty. |

### Campaign Setup

The `founder` creates a funding campaign by sending a `3030` event to the Nostr network. Then a Bitcoin transaction that includes the `event identifier` is sent to the blockchain with the identifier as OP_RETURN value.

Any software can implement the protocol, with different security models for wallets.

### Campaign Browser

`Campaign` software should be entirely clientside, either as a stand-alone app, or as a purely frontend webpage.

It should find all the published `kind: 3030` events, then use the `nostrPubKey` field in the content to query for profile [NIP-01](01.md) (`kind: 0`) and [NIP-78](78.md) data.

Tags (`d`) used by the protocol on `kind: 30078` events are:

- `['d', 'angor:project']` - project description content
- `['d', 'angor:faq']` - FAQ items (JSON array of `{question, answer}` objects)
- `['d', 'angor:members']` - team member pubkeys (JSON object `{pubkeys: [...]}`)
- `['d', 'angor:media']` - media gallery (JSON array of `{url, type}` objects)

It should additionally query an indexer of the blockchain (e.g. Bitcoin) to find the transactions with the `event identifier` in the OP_RETURN field.

### Event `3030`: Create a funding campaign.

A `kind: 3030` event is an addressable (replaceable) event keyed by the `d` tag set to the `projectIdentifier`.

**Tags:**

```json
[["d", "<projectIdentifier>"]]
```

**Event Content**

The content is a JSON-serialized project information object. Property names use camelCase. Dates are Unix timestamps. Null values are omitted.

#### Version 1 (legacy)

```json
{
    "founderKey": "<string, Bitcoin compressed public key of the founder (hex)>",
    "founderRecoveryKey": "<string, Bitcoin compressed public key for recovery multisig (hex)>",
    "projectIdentifier": "<string, unique identifier of the project>",
    "nostrPubKey": "<string, Nostr public key for the project (hex)>",
    "startDate": "<int, Unix timestamp of campaign start>",
    "penaltyDays": "<int, penalty lock days>",
    "expiryDate": "<int, Unix timestamp when remaining funds can be reclaimed>",
    "targetAmount": "<int, target amount in satoshis>",
    "stages": [
        {"amountToRelease": "<decimal, percentage>", "releaseDate": "<int, Unix timestamp>"}
    ],
    "projectSeeders": {
        "threshold": "<int, number of seeder secrets required>",
        "secretHashes": ["<string, hex-encoded hash>"]
    }
}
```

Events without a `version` field or with `version < 2` are treated as version 1 and always as `Invest` type.

#### Version 2

Version 2 adds `version`, `projectType`, `networkName`, `endDate`, `penaltyThreshold`, and `dynamicStagePatterns`.

```json
{
    "version": 2,
    "projectType": "<int, 0=Invest, 1=Fund, 2=Subscribe>",
    "founderKey": "<string, Bitcoin compressed public key of the founder (hex)>",
    "founderRecoveryKey": "<string, Bitcoin compressed public key for recovery multisig (hex)>",
    "projectIdentifier": "<string, unique project identifier>",
    "nostrPubKey": "<string, Nostr public key for the project (hex)>",
    "networkName": "<string, blockchain network (e.g. 'Bitcoin', 'BitcoinTestnet', 'angornet')>",
    "startDate": "<int, Unix timestamp (required for Invest, optional otherwise)>",
    "endDate": "<int, Unix timestamp (required for Invest, unused otherwise)>",
    "penaltyDays": "<int, penalty lock days (Invest/Fund) or 0 (Subscribe)>",
    "expiryDate": "<int, Unix timestamp when remaining funds can be reclaimed>",
    "targetAmount": "<int, target amount in satoshis (required for Invest, optional otherwise)>",
    "penaltyThreshold": "<int|null, threshold in satoshis (Fund only, optional)>",
    "stages": [
        {"amountToRelease": "<decimal, percentage>", "releaseDate": "<int, Unix timestamp>"}
    ],
    "projectSeeders": {
        "threshold": "<int>",
        "secretHashes": ["<string>"]
    },
    "dynamicStagePatterns": [
        {
            "patternId": "<int, 0-255>",
            "name": "<string, display name>",
            "description": "<string>",
            "frequency": "<int, 0=Weekly, 1=Biweekly, 2=Monthly, 3=BiMonthly, 4=Quarterly>",
            "stageCount": "<int, number of stages>",
            "payoutDayType": "<int, 0=FromStartDate, 1=SpecificDayOfMonth, 2=SpecificDayOfWeek>",
            "payoutDay": "<int, day value>",
            "amount": "<int|null, fixed amount in satoshis (mandatory for Subscribe)>"
        }
    ]
}
```

**Field requirements by project type:**

| Field | Invest | Fund | Subscribe |
|-------|--------|------|-----------|
| `startDate` | Required | Optional | Optional |
| `endDate` | Required | Omitted | Omitted |
| `targetAmount` | Required (> 0) | Optional | Optional |
| `penaltyDays` | Required (10-365) | Required (10-365) | 0 |
| `penaltyThreshold` | Omitted | Optional | Omitted |
| `stages` | Required (fixed list) | Empty | Empty |
| `dynamicStagePatterns` | Empty | Required (>= 1) | Required (>= 1, each with `amount`) |

#### Example: Invest project

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

#### Example: Fund project

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
        }
    ]
}
```

#### Example: Subscribe project

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

### Investment messaging (Encrypted DMs)

Investment communication between founders and investors uses Nostr encrypted direct messages (`kind: 4`) with NIP-44 encryption. A `subject` tag identifies the message type.

| Subject tag | Direction | Content | Encrypted |
|-------------|-----------|---------|-----------|
| `"Investment offer"` | Investor --> Founder | Investment transaction hex | NIP-44 |
| `"Re:Investment offer"` | Founder --> Investor | Recovery signatures | NIP-44 |
| `"Investment completed"` | Investor --> Founder | Transaction ID notification | NIP-44 |
| `"Investment canceled"` | Investor --> Founder | Cancellation notification | Optional |
| `"Release transaction signatures"` | Founder --> Investor | Release signatures | NIP-44 |

Implementations SHOULD support decrypting NIP-04 messages for backward compatibility. NIP-04 ciphertext is detected by the `?iv=` separator.

Reply messages include an `e` tag referencing the original request event.

See [Nostr Communication Protocol](nostr-communication-protocol.md) for full message format specifications.

## Additional

The full P2P Funding Protocol can be found at [P2P Funding Protocol](p2p-funding-protocol.md).

The full Nostr communication protocol specification can be found at [Nostr Communication Protocol](nostr-communication-protocol.md).
