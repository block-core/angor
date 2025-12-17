
<p align="center">
    <img src="https://github.com/user-attachments/assets/fe8c48ab-3479-4312-8e09-7dedce6850f5" height="100" alt="Angor" />
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
* Specifications: [bcip-0005](https://github.com/block-core/bcips/blob/main/bcip-0005.md)
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
