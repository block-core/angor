
<p align="center">
    <img  height="100" alt="Angor" src="https://github.com/user-attachments/assets/6e20b391-e067-4617-aa38-38aa42cdcd91" />
    <img-old src="https://github.com/user-attachments/assets/6e20b391-e067-4617-aa38-38aa42cdcd91" height="100" alt="Angor" />
</p>

<h3 align="center">
    P2P Funding Protocol
</h3>

<br>

## What is Angor?

Angor is a Bitcoin funding protocol with two unique features:

1. Angor is **fully decentralized**, meaning there is no middleman involved in the investment process. Angor has no backend – the platform leverages the **Bitcoin network** for transaction processing, while **Nostr** is being used for decentralized storage of projects' metadata and direct communication with founders. Angor uses Bitcoin's Taproot upgrade for improved efficiency and privacy.

2. Bitcoin is released to the founders in **predetermined stages** via so-called time-lock contracts, allowing investors to **recover unspent funds at any time**. This framework provides investors with greater control, mitigates financial risk, and incentivizes founders to showcase tangible progress between stages.

For more details:
* [P2P Funding Protocol](docs/p2p-funding-protocol.md) — project types, stages, taproot scripts, transaction structure, investment flows
* [Nostr Communication Protocol](docs/nostr-communication-protocol.md) — kind 3030, NIP-78 metadata, encrypted DM investment handshake
* [NIP-3030: Decentralized Crowdfunding](docs/nip-3030-crowdfunding.md) — Nostr event format and investment messaging summary
* FAQ: [https://angor.io/faq](https://angor.io/#FAQ)
  
## How to Use Angor?

Angor is available on web, desktop, mobile, and Android. The web app can be installed as a PWA (Progressive Web App) to provide an app-like experience on supported devices.

You can access Angor online at [https://angor.io/](https://angor.io/), or download the app directly from the [Releases](https://github.com/block-core/angor/releases) page.

### Downloads

| Platform | Download |
|----------|----------|
| Windows | `.exe` installer (x64, arm64) |
| Linux | `.deb` package, `.AppImage` (x64, arm64) |
| macOS | `.dmg` disk image (x64, arm64) |
| Android | `.apk` (unsigned, sideload) |

#### macOS first launch

The macOS builds are ad-hoc signed but not yet notarized by Apple, so on first launch Gatekeeper shows an "unidentified developer" warning. To open the app:

- **Right-click** (or Control-click) `Angor.app` in `/Applications` → **Open** → **Open** in the dialog. macOS remembers the choice after that.

If macOS reports the app as **"damaged and can't be opened"** (an older download, or if the right-click step doesn't appear), clear the download quarantine flag, then open normally:

```bash
xattr -dr com.apple.quarantine /Applications/Angor.app
```

## Code Signing

Free code signing on Windows provided by [SignPath.io](https://signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

## Releasing

To create a new release:

1. **Tag a version**: Push a tag starting with `v` (e.g., `v1.0.0`)
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. **Automatic build**: The `release-avalonia.yml` workflow will:
   - Run tests
   - Build installers for all platforms (Windows, Linux, macOS, Android)
   - Create a GitHub Release with all artifacts

3. **Manual trigger**: You can also trigger a release manually from the Actions tab using `workflow_dispatch`

### Version Format
- Release: `v1.0.0` → Creates a full release
- Pre-release: `v1.0.0-beta` or `v1.0.0-rc1` → Creates a pre-release

## Contributing

Check out [this page](/CONTRIBUTING.MD)

## Contact
* Discord: [https://www.blockcore.net/discord](https://www.blockcore.net/discord)
* Telegram: [https://t.me/angor_io](https://t.me/angor_io)
