# ZapStore Publisher Identity

How to provision, operate and rotate the Nostr identity used by CI to publish
Angor Android releases to [ZapStore](https://zapstore.dev). Companion to
`.github/workflows/release-avalonia.yml` (the `publish-zapstore` job) and
`zapstore.yaml` at the repo root.

## Why a dedicated identity

The nsec that signs ZapStore release events lives in a bunker that GitHub
Actions can reach. Three properties drive every decision below:

1. **Blast radius.** A compromise of the publishing key must not affect any
   personal or social Nostr identity. The publisher's worst case is rogue app
   updates; nothing more.
2. **Permanent cert binding.** NIP-C1 binds the APK signing certificate hash
   to the publishing npub forever (on the relay's whitelist). Tangling that
   permanent binding with a human's personal pubkey is regrettable.
3. **Multi-maintainer access.** The bunker can hand out scoped tokens to
   multiple maintainers; a personal nsec cannot.

Do not reuse:

- The npub used for personal Nostr presence by any maintainer.
- The npub used for the Angor *project* social account (kind 1 community
  posts about Angor itself live elsewhere).
- Any npub already linked to a different APK signing certificate via NIP-C1.

## One-time provisioning

### 1. Generate the publisher keypair

Use [nak](https://github.com/fiatjaf/nak) on a trusted machine ÔÇö never on a
laptop you also use for casual browsing.

```bash
nak key generate
# sec: nsec1...        ÔåÉ write down, never commit, never paste
# pub: npub1angor...   ÔåÉ this is what users see on ZapStore
```

Record the npub. The nsec must be entered exactly once: into the bunker.

### 2. Publish the kind 0 profile

ZapStore renders the publisher block from this metadata. Keep it tight and
official-looking ÔÇö this is the "About the publisher" panel users will see.

```bash
NSEC=nsec1...
nak event --sec "$NSEC" --kind 0 \
  --content '{
    "name":"angor",
    "display_name":"Angor",
    "picture":"https://blossom.angor.io/<icon-sha256>",
    "website":"https://angor.io",
    "nip05":"_@angor.io",
    "about":"Publisher account for Angor app releases. Decentralized Bitcoin funding for founders and investors."
  }' \
  wss://relay.zapstore.dev \
  wss://relay.angor.io \
  wss://relay2.angor.io
```

Optionally serve `/.well-known/nostr.json` from `angor.io` so the `_@angor.io`
NIP-05 verifies; ZapStore shows a tick mark when it does.

### 3. Park the nsec in a bunker

Two acceptable patterns:

- **nsecBunker (self-hosted)** ÔÇö runs on a small VPS, authorises connections
  by policy. Preferred for team setups. https://github.com/kind-0/nsecbunker
- **Amber (Android)** ÔÇö runs on a phone, authorises connections by tap.
  Acceptable for solo maintainers; the phone must be online at release time.

Either way you end with a URL of the form:

```
bunker://<bunker-pubkey>?relay=wss://...&secret=<one-time-secret>
```

Test the bunker before continuing:

```bash
SIGN_WITH="bunker://..." zsp --version  # any zsp command that signs will exercise the link
```

### 4. Link the APK signing certificate (NIP-C1)

This proves the publisher npub controls the keystore that signs the APK.
Without it, ZapStore treats subsequent release events as untrusted.

```bash
# Run once, against the same keystore CI uses to sign the APK
SIGN_WITH="bunker://..." zsp identity --link-key path/to/android.keystore
```

The expected certificate hash for the Angor APK is documented at the top of
the release event preview; today it is

```
1bd41f0101a42ab3b9ad99f2a15c8050b33090cf7026d8cf690a00fec782baec
```

If you regenerate the keystore (lost, leaked, expired) you must repeat this
step from the same publisher npub. End users on Android also cannot
auto-update across a keystore change ÔÇö that's an OS-level constraint, not a
ZapStore one.

### 5. Seed the first publish manually

Do this before adding the secret to CI. It establishes the
`(publisher_npub, d=io.angor.app)` claim on the relay's whitelist so the
unattended CI job has a clean lane.

```bash
SIGN_WITH="bunker://..." zsp publish -q zapstore.yaml
```

Verify on `https://zapstore.dev/app/io.angor.app` ÔÇö icon, name, description,
license, version should all populate within a few minutes.

### 6. Wire the bunker URL into CI

```bash
gh secret set ZAPSTORE_BUNKER_URL --body "bunker://..."
```

From this point onward every stable tag push triggers
`publish-zapstore` in `.github/workflows/release-avalonia.yml`, which
re-runs `zsp publish -q zapstore.yaml` with the bunker doing the signing.
Prereleases (versions containing `-`) are skipped by design.

## What CI can and cannot do

| Action | CI | Manual only |
|---|:-:|:-:|
| Publish a new release of the current app | Ô£à | |
| Update icon / description / tags in `zapstore.yaml` | Ô£à (via repo edit + next release) | |
| Re-link the keystore via NIP-C1 | | Ô£à |
| Rotate the publisher keypair | | Ô£à |
| Change the `d` tag (`io.angor.app`) | | Ô£à + ZapStore admin coordination |

CI must never see the nsec directly. The only secret it holds is the bunker
URL; signing happens out-of-band.

## Rotation procedure

If the bunker is compromised or you need to move maintainers:

1. Generate a new keypair and a fresh bunker URL.
2. From the **old** publisher identity, publish a NIP-26 delegation or a
   kind 5 deletion of recent events, signalling the change. (Optional but
   recommended for audit trail.)
3. Coordinate with ZapStore (`@franzap` / matrix.zapstore.dev) to reassign
   the `io.angor.app` whitelist entry to the new npub ÔÇö the relay enforces
   one publisher per `d` tag.
4. Re-run step 4 (NIP-C1 link) with the new bunker against the existing
   keystore ÔÇö the keystore does not change.
5. `gh secret set ZAPSTORE_BUNKER_URL` with the new URL.
6. Cut a stable release; verify on `zapstore.dev`.

Steps 1, 4, 5, 6 are mechanical. Step 3 requires a human at ZapStore.

## Loss-of-key recovery

If the publisher nsec is unrecoverably lost (no bunker, no backup) the
publisher account is effectively dead. Recovery options:

- **Reclaim the same `d`**: only possible with ZapStore admin intervention.
  Plan on hours-to-days of coordination, not a self-service flow.
- **Republish under a new `d`** (e.g. `io.angor.app2`): clean but breaks
  every existing ZapStore install ÔÇö they cannot auto-update across `d` tag
  changes. Users have to find the new app manually and reinstall.

In short: **back up the publisher nsec** even if the day-to-day signing
happens through the bunker. A printed paper backup in a safe is cheap
insurance against losing a $0-revenue identity that controls the install
base.

## References

- `zapstore.yaml` ÔÇö repo-root manifest read by `zsp publish`
- `.github/workflows/release-avalonia.yml` ÔÇö `publish-zapstore` job
- `src/design/App.Android/App.Android.csproj` ÔÇö Android `ApplicationId`
  must match the `d` tag (`io.angor.app`)
- [NIP-82](https://github.com/nostr-protocol/nips/blob/master/82.md) ÔÇö event
  kinds 32267 / 30063 / 3063
- [NIP-C1](https://github.com/nostr-protocol/nips/blob/master/C1.md) ÔÇö
  identity proofs (the cert binding)
- [zsp README](https://github.com/zapstore/zsp) ÔÇö CLI flags and signing
  methods
