# BCIP-0005 : Angor - P2P Funding Protocol

```
Number:  BCIP-0005
Title:   Angor - P2P Funding Protocol
Type:    Protocol
Status:  Draft
Authors: Dan Gershony, David Gershony
Created: 2023-02-28
Updated: 2025-06-09
```

## Abstract

Angor - P2P Funding Protocol (P2PFP) is a decentralized funding mechanism on the Bitcoin network. It utilizes time-locked contracts whereby investors lock bitcoin into time-based stages. Each stage, upon reaching its time limit, allows the founders to claim the locked bitcoins (and optionally founders may issue in return ownership interest to investors). Alternatively, investors can recover any remaining coins at any time, however recovery may incur penalties. The system distinguishes two types of investors: lead investors and regular investors. Lead investors, often early investors, always incur penalties during coin recovery. Regular investors seeking to recover their coins have two options: they can either accept the penalty or, if enough lead investors have already recovered their coins with penalty, they can use the lead investors' hashlocks for a penalty-free recovery. This mitigates fraud, and boosts investor confidence by enabling active control over their investments.  

The protocol supports three project types: **Invest** (fixed stages with a fundraising window), **Fund** (dynamic stages with no fundraising window and an optional penalty threshold), and **Subscribe** (dynamic stages with no penalty). This allows founders to model traditional crowdfunding campaigns, open-ended development funds, and subscription-based support models all within the same protocol framework.

## Copyright

MIT

## Motivation

In previous blockchain projects, founders had little incentive to protect investors' interests, such as the value of the issued tokens, once the Bitcoin investment was released. Investors often resorted to lawsuits and social pressure to seek restitution. In this approach, our aim is to protect investors from fraud and mismanagement while also aligning the project incentives with those of the investors.

Moreover, it encourages Bitcoin holders to invest and potentially allocate a portion of their Bitcoin holdings out of HODL mode, thus making it safer for their investments to work for them and for the Bitcoin network as a whole by unlocking more funds for investment.

## Design

This proposal defines an investment protocol where funds are committed to a project into stages, each stage has various spending conditions. The spending conditions for project founders use time-locked contracts and founders keys, while those for investors incorporate a pre-signed multisig recovery transactions (also known as revocations), additionally projects may enable a threshold of hashlocks for fast recovery.

The project founders designate individual stages, each with a defined timeline and expected percentage for each stage. Interested investors lock their Bitcoin according to these criteria but also include mechanisms to recover their investment.

The design identifies two types of investors: early investors, termed lead investors as they initiate the investment (lead investors are optional), and subsequent regular investors. Before publishing the investment transaction, both lead investors and regular investors generate a multisig penalty transaction, pre-signed and locked into a long time-lock commitment, in case of lead investors they will also commit to a secret. This transaction grants the investor the ability to recover their investment at any time by incurring a long timelock penalty.

If a project has lead investors, regular investors can introduce an additional spending path to their investment transaction. This additional spending path is a threshold of hashlocks, investors commit to the hashes of lead investors. Once a sufficient number of lead investors have initiated their recovery transaction, regular investors can exit the investment without a penalty.  

### Protocol versioning

The protocol uses a version number to distinguish between protocol generations:

- **Version 1**: The original protocol supporting only fixed-stage investment projects (Invest type).
- **Version 2**: Adds support for multiple project types (Invest, Fund, Subscribe), dynamic stages, and penalty thresholds.

Version 1 projects are always treated as `Invest` type regardless of any other fields. The version is encoded in the satoshi value of the Project Identifier output in the initialization transaction.

### Project types

The protocol defines three project types, each suited to a different funding model:

| Property | Invest | Fund | Subscribe |
|----------|--------|------|-----------|
| Stage definition | Fixed (predetermined dates and percentages) | Dynamic (calculated per investor from patterns) | Dynamic (calculated per investor from patterns) |
| Investment window | Required (start/end dates) | No fixed window | No fixed window |
| Target amount | Required | Optional | Optional |
| Penalty for early withdrawal | Yes | Yes (with optional threshold) | No |
| Stage amount distribution | Custom per stage (founder-defined percentages) | Equal across all stages | Equal across all stages |
| Fixed subscription price | No | No | Yes (defined by pattern) |

#### Invest (ProjectType = 0)

The traditional investment model. The founder defines fixed stages with predetermined release dates and percentage allocations. Investors commit funds during a defined investment window. A target amount must be reached for the founder to claim funds. If the target is not met, the founder signs release transactions to return funds to investors. Penalties apply for early withdrawal.

#### Fund (ProjectType = 1)

A flexible open-ended funding model. The founder defines one or more dynamic stage patterns (e.g., "6 monthly payments", "12 biweekly payments"). There is no fixed investment window — investors can join at any time. Each investor's stage release dates are calculated individually based on their investment start date and the chosen pattern. Penalties apply for early withdrawal, but a **penalty threshold** can be set to exempt small investments from the penalty mechanism.

#### Subscribe (ProjectType = 2)

A subscription-based model for ongoing support. Similar to Fund but with no penalty for withdrawal — investors can exit freely at any time via the end-of-project script path. The founder defines patterns with fixed amounts (subscription prices). Investors must invest exactly the pattern's specified amount.

#### Interactive Pre-signing of the Recovery Transaction  

The protocol mandates that both the founder and investor co-sign the penalty recovery transaction. An investor will not broadcast the investment transaction until they have the founder's signature as part of the recovery transaction. To recover their funds, investors need to publish the recovery transaction (excluding any already spent utxos).

**Exception — below-threshold Fund investments:** When a Fund project defines a penalty threshold and the investment amount is at or below that threshold, the investor does not require recovery signatures from the founder. Instead, the end-of-project script path is configured with an immediate expiry, allowing the investor to recover funds without the interactive penalty flow.

#### Nostr protocol for communication

The protocol uses Nostr to facilitate communication between founder and investor. Nostr supports a growing variety of decentralized communication methods, and Nostr can be extended to fit any new protocol extensions we may require in the future.
Project information will be published to the Nostr network, specifically direct communication, project information (and metadata), progress updates and notifications.  

The protocol will create Nostr identities for the founder and user from the bitcoin wallet, such identities can be used in other Nostr clients as well.  

## Specification  

We utilize the latest upgrades to the Bitcoin network, namely Taproot and Schnorr. Taproot enables the commitment to multiple execution paths of a Bitcoin output, this allows us to create different conditions on the spending of coins.  

### Project parameters setup

To setup investment transactions certain parameters need to be defined in advance to be able to build the tapscript spending conditions.

#### Project initial parameters:

- **Protocol version** : the version of the protocol (currently version 2).
- **Project type** : one of `Invest`, `Fund`, or `Subscribe`.
- **Campaign start date** : this is so the funds in the first stage can be locked until the start date. Required for `Invest`, optional for `Fund` and `Subscribe`.  
- **Campaign end date** : end of the investment window. Required for `Invest`, not used for `Fund` and `Subscribe`.  
- **Target funding amount** : if target funding was not reached the founder will not be able to spend funds only sign exit transaction (otherwise investors will use the penalty). Required for `Invest`, optional for `Fund` and `Subscribe`.  
- **Stage funding break down and stage dates** : (for `Invest` type) so investors know how many Bitcoin payment stages (outputs) to create and what are the time locks and amounts in each stage.  
- **Dynamic stage patterns** : (for `Fund` and `Subscribe` types) define the available subscription/funding options that investors can choose from.
- **Project expiry date** : to be able to create a condition where investors can take back funds if they are never used.    
- **Penalty days** : the number of days funds are locked during penalty recovery. Applies to `Invest` and `Fund` types. Not used for `Subscribe`.
- **Penalty threshold** : (optional, `Fund` type only) an amount in satoshis below which investments are exempt from penalty and do not require recovery signatures.
- **Founder keys** : the keys of the project founder that will claim the investment when a stage is reached.
- **Founder recovery keys** : the keys of the project founder that will be used for the 2-of-2 recovery multisig.
- **Founder nostr keys** : the keys of the project founder that will be used for Nostr communication. 
- **Network name** : the blockchain network this project operates on (e.g., "Bitcoin", "BitcoinTestnet", "angornet").
- **Lead investor threshold** : (optional) the number of lead investor secrets required for penalty-free recovery.
- **Lead investor secret hashes** : (optional) the list of secret hashes committed by lead investors.

An example of the data published to Nostr event kind 3030 `NIP3030 Decentralized Crowdfunding (Custom NIP)`

**Invest project example (v2):**

```json
{
    "version": 2,
    "projectType": 0,
    "founderKey": "03309b10a078ca8e718693d241b3a57ff31f5aabcd7ec53089bd143a57036332ea",
    "founderRecoveryKey": "03cc053e8fd5bd6cea509df6c58d0f6fe16d9f4bed20a7b15a9447dbd9d6a52d9a",
    "projectIdentifier": "angor1q9j9jvmqwll00gnzf8thu9lrar65ccpu4z5np6j",
    "nostrPubKey": "5a05cc7a38e3875ee3242e5f068304a36c9609c4c15f5baaf7d75e8fcdfe36c5",
    "networkName": "Bitcoin",
    "startDate": "2025-02-07T00:00:00Z",
    "endDate": "2025-06-07T00:00:00Z",
    "penaltyDays": 90,
    "expiryDate": "2026-06-07T00:00:00Z",
    "targetAmount": 5000000000,
    "stages": [
        {
            "amountToRelease": 25,
            "releaseDate": "2025-02-07T00:00:00Z"
        },
        {
            "amountToRelease": 25,
            "releaseDate": "2025-03-09T00:00:00Z"
        },
        {
            "amountToRelease": 25,
            "releaseDate": "2025-04-08T00:00:00Z"
        },
        {
            "amountToRelease": 25,
            "releaseDate": "2025-05-08T00:00:00Z"
        }
    ],
    "projectSeeders": {
        "threshold": 0,
        "secretHashes": []
    },
    "dynamicStagePatterns": []
}
```

**Fund project example (v2):**

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
    "expiryDate": "2027-01-01T00:00:00Z",
    "targetAmount": 0,
    "penaltyThreshold": 1000000,
    "stages": [],
    "projectSeeders": {
        "threshold": 3,
        "secretHashes": ["a1b2c3...", "d4e5f6...", "789abc..."]
    },
    "dynamicStagePatterns": [
        {
            "patternId": 0,
            "name": "6-Month Monthly",
            "description": "6 monthly payments on the 1st of each month",
            "frequency": 2,
            "stageCount": 6,
            "payoutDayType": 1,
            "payoutDay": 1,
            "amount": null
        },
        {
            "patternId": 1,
            "name": "12-Month Monthly",
            "description": "12 monthly payments on the 1st of each month",
            "frequency": 2,
            "stageCount": 12,
            "payoutDayType": 1,
            "payoutDay": 1,
            "amount": null
        }
    ]
}
```

**Subscribe project example (v2):**

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
    "expiryDate": "2027-01-01T00:00:00Z",
    "targetAmount": 0,
    "stages": [],
    "projectSeeders": {
        "threshold": 0,
        "secretHashes": []
    },
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

Additional information can be found in [NIP-3030](nip-3030-crowdfunding.md)

#### Dynamic stage patterns

For `Fund` and `Subscribe` project types, the founder defines one or more dynamic stage patterns. Each pattern specifies:

- **PatternId** (byte, 0-255): Unique identifier for this pattern within the project. This ID is stored in the OP_RETURN of the investment transaction.
- **Frequency** : how often stages are released.
- **StageCount** : number of stages in this pattern.
- **PayoutDayType** : how payout days are calculated.
- **PayoutDay** : specific day value (interpretation depends on PayoutDayType).
- **Amount** (optional for Fund, mandatory for Subscribe): fixed investment amount in satoshis.

**Frequency values:**

| Value | Name | Duration |
|-------|------|----------|
| 0 | Weekly | 7 days |
| 1 | Biweekly | 14 days |
| 2 | Monthly | 30 days |
| 3 | BiMonthly | 60 days |
| 4 | Quarterly | 90 days |

**PayoutDayType values:**

| Value | Name | Description |
|-------|------|-------------|
| 0 | FromStartDate | Fixed intervals from the investment start date |
| 1 | SpecificDayOfMonth | Specific day of month (1-31) |
| 2 | SpecificDayOfWeek | Specific day of week (0=Sunday, 6=Saturday) |

When an investor creates an investment for a Fund or Subscribe project, they select a pattern and specify a start date. The stage release dates are calculated dynamically:

- **FromStartDate**: Each stage releases at `startDate + (frequency_duration * (stage_index + 1))`
- **SpecificDayOfMonth**: Each stage releases on the specified day of the appropriate month
- **SpecificDayOfWeek**: Each stage releases on the specified weekday at the appropriate interval

The investment amount is distributed equally across all stages: `amount_per_stage = total_amount / stage_count`.

#### Penalty threshold (Fund projects)

Fund projects may define an optional **penalty threshold** — an amount in satoshis. This threshold determines whether the interactive recovery signature flow is required:

- **Above threshold** (`investment > penaltyThreshold`): Full penalty mechanism applies. The investor must obtain recovery signatures from the founder before publishing the investment transaction.
- **At or below threshold** (`investment <= penaltyThreshold`): The investor bypasses the penalty mechanism. The end-of-project script is configured with an immediate expiry date, allowing the investor to recover funds without penalty and without founder interaction.
- **No threshold set** (`penaltyThreshold = null`): All investments are treated as above threshold — penalties always apply.

This allows projects to accept small contributions with minimal friction while still protecting larger investments with the full penalty/recovery mechanism.

#### Investor parameters: 

- **Investor key** : the key an investor controls to release their coins during recovery (or if the target was not reached).
- **Lead Investor Secret** : A secret that is used for the lead investor for the threshold hashlocks.
- **Investment start date** : (for `Fund` and `Subscribe` types) the date from which dynamic stage release dates are calculated. Normalized to midnight UTC.
- **Pattern ID** : (for `Fund` and `Subscribe` types) which dynamic stage pattern the investor selected.

Each investor will then generate a transaction with an output for each stage locked under the conditions agreed upon earlier.

### Key derivation path.   

In order to facilitate the retrieval of coins and recovery keys during active investments, we establish a derivation path that specifies the generation of different keys.

All keys must be hardened derivation following `m / purpose' / coin_type' / project_id' / member_type'`

**Angor Key**                                               - `A well-known xpubkey`  
**Founder key**                                            - `m / 5' / n'`   
**upi - Unique Project Identifier**                - `Low64(hash256(founderPubKey))`  
**Project Identifier**                                     - `Bech32(ExtPubkey(AngorKey).Derive(upi))`  
**Founder/Investor Nostr Key (NIP06)**      - `m / 44' / 1237' / upi' / 0 / 0`  
**Nostr Storage Key (NIP06)**       - `m / 44' / 1237' / 1' / 0 / 0`  
**Founder Recovery Key**                            - `m / 5' / 0' / upi' / 1'`  
**Investor Lead Secret**                               - `m / 5' / 0' / upi' / 2'`  
**Investor Key**                                            - `m / 5' / 0' / upi' / 3'`  

n = project index created by the founder, a founder may create a few projects under the same wallet  
upi = an integer extracted from the lower part of the sha256 of the founder key

### Founder initialization transaction

To signal the creation of a project, the founder will publish a transaction on the blockchain.    

#### Project identifier

Every project created in Angor will receive a unique identifier. This identifier is derived from the founder's key and the Angor xpubkey.  

- The project identifier will appear as the first UTXO in both the founder's initialization transaction and the investor's investment transaction.  
- It is represented as a regular SegWit address, but with the Bech32 prefix `angor`, allowing external observers to recognize it as an Angor investment identifier.  
    
Example:  
`angor1q2umav7t03jjtrtr4ejmf7ecwnqx76q9tz544wq`

#### Protocol versioning

To implement protocol versioning, we encode the version number in the satoshis of the Project Identifier output in the initialization transaction.    

- `0.00010001` to represent version 1 of the protocol  
- `0.00010002` to represent version 2 of the protocol  

#### Storing essential metadata in `OP_RETURN`  

##### The initialization transaction will commit to the following essential project details   
- **Founders public key**
- **Nostr event id** containing the project's metadata

Format:  
```
OP_RETURN
<founder-key> 
<keyType>  
<nostr-eventid> 
```

By storing the Nostr event ID, the founder effectively commits to the project parameters.
Once recorded on the blockchain, the founder cannot alter the metadata without modifying the event ID, ensuring immutability.
Additionally, the metadata in the Nostr event is required for generating Taproot scripts when spending coins.

##### Investor Metadata Commitments    

Investors will also store metadata in their investment transactions.  

- For regular investors in **Invest** projects, the OP_RETURN contains:  

```
OP_RETURN
<investor-key> 
```

- For lead investors in **Invest** projects, an additional hash of a secret is included:  

```
OP_RETURN
<investor-key>
<hash_of_secret>
```

- For regular investors in **Fund** or **Subscribe** projects, dynamic stage info is appended:

```
OP_RETURN
<investor-key>
<dynamic-stage-info (4 bytes)>
```

- For lead investors in **Fund** or **Subscribe** projects:

```
OP_RETURN
<investor-key>
<hash_of_secret>
<dynamic-stage-info (4 bytes)>
```

##### Dynamic Stage Info encoding

For Fund and Subscribe investments, a compact 4-byte encoding stores the investor-specific stage parameters in the OP_RETURN:

```
Byte 0-1: Investment start date (days since epoch, little-endian uint16)
Byte 2:   Pattern ID (0-255)
Byte 3:   Stage count (0 = use pattern default, 1-255 = override)
```

The epoch date is **January 1, 2025 UTC**. This allows encoding dates up to approximately 179 years from the epoch using only 2 bytes.

The pattern ID references a `DynamicStagePattern` in the project's metadata. Combined with the investment start date, this provides all necessary information to reconstruct the stage release dates for this investment.

### The taproot spending conditions. 

The spending condition for each stage is represented by the taproot path. To ensure a controlled release of funds as the project advances, different time locks are applied to each stage. This approach allows for a gradual allocation of resources as the project team successfully meets their predetermined goals and objectives.

#### Stage release date calculation

For **Invest** projects, stage release dates are taken directly from the project's predefined `Stages` list.

For **Fund** and **Subscribe** projects, stage release dates are calculated dynamically per investor:

1. The investor's start date and pattern ID are read from the OP_RETURN.
2. The pattern's frequency and payout day type determine the calculation method.
3. Each stage's release date is computed as follows:

**FromStartDate:**
```
release_date[i] = investment_start_date + (frequency_duration * (i + 1))
```

**SpecificDayOfMonth:**
```
release_date[i] = next occurrence of PayoutDay in the (i+1)th period month after start
```

**SpecificDayOfWeek:**
```
release_date[i] = next occurrence of PayoutDay weekday in the (i+1)th period after start
```

#### Founder taproot path

The funds are being held in a time-locked script, which can only be spent after a certain amount of time has elapsed. Additionally, the script requires a signature from the founder key, which ensures that only authorized individuals can spend the funds.

```
<founder-key> 
OP_CHECKSIGVERIFY  
<stage_time_lock> 
OP_CHECKLOCKTIMEVERIFY 
```

For Fund and Subscribe projects, the `stage_time_lock` is the dynamically calculated release date for that specific investor and stage.

#### Investor taproot path with penalty

The funds are being held in a multi-signature script, which requires signatures from the investor and founder before the funds can be spent.  

**Regular investors**

```
<founder-recovery-key> 
OP_CHECKSIGVERIFY   
<investor-key> 
OP_CHECKSIG   
```

**Lead investors**

Lead investor adds the hashlock to the multisig, this forces the lead investor to reveal the secret when recovering into a penalty.
The reveal of the secret allows regular investors to use the secret for recovery without penalty, once sufficient lead investors recover into a penalty.    

```
<founder-recovery-key> 
OP_CHECKSIGVERIFY   
<investor-key> 
OP_CHECKSIGVERIFY   
OP_HASH256
<hash_of_secret>
OP_EQUAL
```

This is the spending condition that needs to be presigned by the founder in order for the investor to be able to recover with a penalty

The script the founder will sign will look like this

```
<investor-key> 
OP_CHECKSIGVERIFY 
<penalty_time_lock> 
OP_CHECKSEQUENCEVERIFY  
```

The founder will provide the investor with the signatures of the output for each stage in the investment transaction, using SIGHASH_SINGLE | SIGHASH_ANYONECANPAY to commit to each input separately. This allows each input to be potentially recovered independently by the investors. The investor must keep a backup of such signatures to be able to recover their funds.

**Note on Subscribe projects:** Subscribe projects set `penaltyDays = 0`, which means this path is effectively unused. Investors in Subscribe projects use the end-of-project path for immediate recovery instead.

#### Investor taproot path no penalty (threshold of hashlocks)

To avoid penalty when recovering funds a regular investor will commit to additional taproot leaves in the investment transaction, this is the threshold of hashlocks of the lead investors.
To achieve that we use taproot's ability to create many commitment leaves.

Let's assume there are 5 lead investors and the threshold is 3 hashlocks  

We would then create a branch for each threshold combination  
The formula for combinations is:  

$$
C(n, k) = \frac{n!}{k!(n - k)!}
$$

Where:
- \( n \) is the total number of lead investors.
- \( k \) is the number of the threshold of lead investors.
- \( ! \) denotes factorial.

So, for example there are 10 combinations of 3 elements out of 5. These combinations are:

    {L1,L2,L3}
    {L1,L2,L4}
    {L1,L2,L5}
    {L1,L3,L4}
    {L1,L3,L5}
    {L1,L4,L5}
    {L2,L3,L4}
    {L2,L3,L5}
    {L2,L4,L5}
    {L3,L4,L5}

An example of 1 taproot branch in the tree would look similar to the following

```
OP_HASH256
<lead1-hash>
OP_HASH256
<lead2-hash>
OP_HASH256
<lead3-hash>
<investor-key>
OP_CHECKSIG
```

When spending using this option only one of the branches is added to the transaction.  

#### Project expiry date

Once the project finish expiry has been reached, the investor may claim any remaining coins that were not claimed by the project.  

```
<investor-key> 
OP_CHECKSIGVERIFY 
<finish_date_time_lock> 
OP_CHECKLOCKTIMEVERIFY  
```

The effective expiry date used in the script depends on the project type and investment amount:

| Scenario | Effective expiry date |
|----------|----------------------|
| Invest project | `projectInfo.expiryDate` |
| Fund project, above penalty threshold | `projectInfo.expiryDate` |
| Fund project, at or below penalty threshold | `projectInfo.startDate` (immediate recovery) |
| Subscribe project | `projectInfo.startDate` (immediate recovery) |

For Subscribe projects and below-threshold Fund investments, setting the expiry date to the project's start date effectively makes this path immediately spendable, allowing investors to recover without penalty at any time.

#### Commitments and proofs

A Bitcoin Pay-to-Taproot (P2TR) output employs a commitment scheme to obscure its underlying scripts. In order to initiate a spend of the funds held in the output, the spender must possess knowledge of the hash of the commitment's leaves. Consequently, the investor must divulge the script to the spender to facilitate spending.

For the investor this is not an issue as all the parameters to create the script and generate the hash of the commitment's leaves is known in advance and is made public by the project.

To enable the project founder to access the public key of the investors, an additional `op_return` output is added to the transaction. This output contains the investor's public key (and hash of the secret for lead investors, and dynamic stage info for Fund/Subscribe investors), which the investor can use to generate the hash of the commitment's leaves and claim the coins.  

To distinguish transactions belonging to a particular project, a public common address is defined as a unique project identifier. A small fee is sent to this address, which allows the project to be easily identified and tracked.  

With this information, a transaction contains all the necessary data for project founders to discover it and access the invested coins. By utilizing specialized block explorers, it is possible to track the total investment raised by a particular project.  

### Transaction structure

#### Invest project (fixed stages)

This is an example of a transaction by one investor in an Invest project; each investor must generate such a transaction.

M(n)  - we assume 5 stages  
FK    - 1 founder key  
FRK   - 1 founder recovery key  
IK    - 1 investor key   
SK(n) - N seeders  
ED    - expiry date  

```ascii
+--------------------------+
| investment tx            |
+--------------------------+
  |  |  |  |  |  |  |
  |  |  |  |  |  |  |
  |  |  |  |  |  |  |
  |  |  |  |  |  |  | project id   
  |  |  |  |  |  |  +------------------------> P2WSH
  |  |  |  |  |  |
  |  |  |  |  |  | investor pubkey   
  |  |  |  |  |  +------------------------> op_return : IK + SK (optional only for seeders)
  |  |  |  |  | 
  |  |  |  |  |                                                        +-------------+
  |  |  |  |  |                  +--> FK + M1 timelock             --> | founder tx  |
  |  |  |  |  |                  |                                     +-------------+
  |  |  |  |  |                  |                                     +-------------+
  |  |  |  |  |                  +--> IK + FRK + optional Hash(SK) --> | investor tx | <-- presigned recovery penalty
  |  |  |  |  |                  |                                     +-------------+
  |  |  |  |  |  Stage 1 output  |                                     +-------------+
  |  |  |  |  +------------------+--> IK + [S1-SN]                 --> | investor tx | <-- optional regular investors
  |  |  |  |                     |                                     +-------------+
  |  |  |  |                     |                                     +-------------+
  |  |  |  |                     +--> IK + ED timelock             --> | investor tx |
  |  |  |  |                                                           +-------------+
  |  |  |  | Stage 2 output                
  |  |  |  +--------------------> same as stage 1 but with M2 timelock 
  |  |  |
  |  |  |                 
  |  |  | Stage 3 output     
  |  |  +--------------------> same as stage 1 but with M3 timelock 
  |  |                    
  |  |
  |  | Stage 4 output
  |  +--------------------> same as stage 1 but with M4 timelock
  |                      
  |                      
  | Stage 5 output      
  +--------------------> same as stage 1 but with M5 timelock
```

#### Fund / Subscribe project (dynamic stages)

For Fund and Subscribe projects, the transaction structure is the same but with the following differences:

1. **OP_RETURN** includes a 4-byte dynamic stage info encoding (start date, pattern ID, stage count).
2. **Stage count** is determined by the chosen pattern (not predefined in project metadata).
3. **Stage amounts** are distributed equally: `total_amount / stage_count` per stage.
4. **Time locks** are calculated dynamically from the investor's start date and pattern frequency.
5. **Expiry date** in the end-of-project script uses the effective expiry (which may be immediate for Subscribe or below-threshold Fund).

```ascii
+--------------------------+
| investment tx            |
+--------------------------+
  |  |  |  ...  |  |
  |  |  |       |  |
  |  |  |       |  | project id   
  |  |  |       |  +------------------------> P2WSH
  |  |  |       |
  |  |  |       | investor info   
  |  |  |       +------------------------> op_return : IK + [SK] + <dynamic-stage-info (4B)>
  |  |  | 
  |  |  |                                                              +-------------+
  |  |  |                    +--> FK + calculated_timelock[1]      --> | founder tx  |
  |  |  |                    |                                         +-------------+
  |  |  |                    |                                         +-------------+
  |  |  |                    +--> IK + FRK + optional Hash(SK)    --> | investor tx | <-- presigned recovery
  |  |  |   Stage 1 output   |                                         +-------------+
  |  |  +--------------------+--> IK + [S1-SN]                    --> | investor tx | <-- optional
  |  |                       |                                         +-------------+
  |  |                       |                                         +-------------+
  |  |                       +--> IK + effective_expiry timelock   --> | investor tx |
  |  |                                                                 +-------------+
  |  | Stage 2 output                
  |  +--------------------> same as stage 1 but with calculated_timelock[2]
  |
  | ...remaining stages (count determined by pattern)
  +--------------------> stage N with calculated_timelock[N]
```

### Investment flows

#### Standard investment flow (above penalty threshold)

| Step | Actor    | Action |
|------|----------|--------|
| 1    | Investor | Creates investment transaction with taproot stage outputs |
| 2    | Investor | Sends the investment transaction to the founder via Nostr (`"Investment offer"`) |
| 3    | Founder  | Builds recovery transaction and signs it |
| 4    | Founder  | Sends recovery signatures back to investor via Nostr (`"Re:Investment offer"`) |
| 5    | Investor | Verifies founder's recovery signatures |
| 6    | Investor | Signs and broadcasts the investment transaction on-chain |
| 7    | Investor | Notifies founder via Nostr (`"Investment completed"`) |

#### Below-threshold investment flow (Fund projects only)

When the investment amount is at or below the penalty threshold, the interactive signature exchange is skipped:

| Step | Actor    | Action |
|------|----------|--------|
| 1    | Investor | Creates investment transaction (with immediate expiry in end-of-project path) |
| 2    | Investor | Signs and broadcasts the investment transaction on-chain |
| 3    | Investor | Notifies founder via Nostr (`"Investment completed"`) |

#### Investor recovery with penalty

| Step | Actor    | Action |
|------|----------|--------|
| 1    | Investor | Adds founder's recovery signatures to the recovery transaction |
| 2    | Investor | Broadcasts the signed recovery transaction on-chain |
| 3    | Investor | Waits for the penalty timelock to expire (`penaltyDays`) |
| 4    | Investor | Builds and signs a release transaction to spend the penalty-locked outputs |
| 5    | Investor | Broadcasts the release transaction to receive their coins |

#### Investor recovery without penalty (end of project)

When the effective expiry date has passed (or is immediate for Subscribe/below-threshold Fund):

| Step | Actor    | Action |
|------|----------|--------|
| 1    | Investor | Builds and signs a transaction spending unclaimed stage outputs via the end-of-project script |
| 2    | Investor | Broadcasts the transaction to receive their coins immediately |

#### Founder claims a stage

| Step | Actor   | Action |
|------|---------|--------|
| 1    | Founder | After the stage timelock expires, builds a transaction spending all investor stage outputs |
| 2    | Founder | Broadcasts the transaction to claim the stage funds |

**Note:** For Invest projects, all investors share the same stage release dates. For Fund/Subscribe projects, each investor has individually calculated stage dates based on their start date and chosen pattern. The founder must track each investor's stage dates independently.

#### Founder releases unfunded coins

When an Invest project does not meet its target:

| Step | Actor    | Action |
|------|----------|--------|
| 1    | Founder  | Builds unsigned release transaction for the investor |
| 2    | Founder  | Signs the release transaction |
| 3    | Founder  | Sends release signatures to investor via Nostr (`"Release transaction signatures"`) |
| 4    | Investor | Verifies founder's release signatures |
| 5    | Investor | Adds both signatures and broadcasts the transaction |

### Nostr communication

All messages use Nostr kind `EncryptedDm` (NIP-04) with a `subject` tag to identify the message type.

| Subject Tag | Direction | Purpose |
|-------------|-----------|---------|
| `"Investment offer"` | Investor -> Founder | Sends investment transaction requesting recovery signatures |
| `"Re:Investment offer"` | Founder -> Investor | Returns recovery signatures |
| `"Investment completed"` | Investor -> Founder | Notifies founder that investment was published on-chain |
| `"Investment canceled"` | Investor -> Founder | Notifies founder that investment request is canceled |
| `"Release transaction signatures"` | Founder -> Investor | Sends release signatures for unfunded coin release |

### Protocol constants

| Constant | Value | Description |
|----------|-------|-------------|
| Min fee rate | 1000 sats/kB | Minimum relay fee (1 sat/vB) |
| Dust threshold | 330 sats | Taproot dust threshold |
| Min penalty days | 10 | Minimum penalty period |
| Max penalty days | 365 | Maximum penalty period (BIP-68 limit) |
| Min days between stages | 1 | Minimum gap between stage release dates |
| Min days until first stage | 1 | Minimum time before first stage releases |
| Min target amount | 100,000 sats | Minimum target amount (0.001 BTC) |
| Dynamic stage epoch | 2025-01-01 UTC | Reference epoch for compact date encoding |

### Acknowledgement

TBD

### Privacy

TBD

## References

- [Angor Nostr Communication Protocol](nostr-communication-protocol.md)
- [NIP-3030: Decentralized Crowdfunding](nip-3030-crowdfunding.md)
- [Angor Reference Implementation](https://github.com/block-core/angor)
- [BIP-341: Taproot](https://github.com/bitcoin/bips/blob/master/bip-0341.mediawiki)
- [BIP-340: Schnorr Signatures](https://github.com/bitcoin/bips/blob/master/bip-0340.mediawiki)
- [BIP-68: Relative Lock-time](https://github.com/bitcoin/bips/blob/master/bip-0068.mediawiki)
