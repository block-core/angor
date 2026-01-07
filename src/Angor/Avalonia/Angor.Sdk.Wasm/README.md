# Angor SDK WebAssembly

This project compiles the Angor SDK to WebAssembly (WASM) for use in TypeScript/JavaScript applications.

## Build

```bash
cd Angor.Sdk.Wasm
dotnet build
```

The output will be in `bin/Debug/net9.0/wwwroot/_framework/`.

## Production Build

For a smaller, optimized bundle:

```bash
dotnet publish -c Release
```

## Usage in TypeScript/JavaScript

### Option 1: Script Tag

```html
<!DOCTYPE html>
<html>
<head>
    <base href="/" />
</head>
<body>
    <script src="_framework/blazor.webassembly.js"></script>
    <script src="angor-sdk.js"></script>
    <script>
        async function main() {
            const sdk = await loadAngorSdk();
            
            // Initialize with network
            const initResult = await sdk.initialize('testnet');
            console.log('SDK initialized:', initResult);
            
            // Generate a wallet
            const wallet = await sdk.generateWallet(12);
            console.log('Wallet:', wallet);
            
            // Get project details
            const project = await sdk.getProject('npub1...');
            console.log('Project:', project);
        }
        
        main().catch(console.error);
    </script>
</body>
</html>
```

### Option 2: ES Module (TypeScript)

```typescript
import { loadAngorSdk, IAngorSdk, SdkResult } from './angor-sdk';

async function main(): Promise<void> {
    const sdk: IAngorSdk = await loadAngorSdk();
    
    // Initialize the SDK
    const result = await sdk.initialize('testnet');
    if (!result.success) {
        console.error('Failed to initialize:', result.error);
        return;
    }
    
    // Create an investment
    const investment = await sdk.createInvestment(
        'wallet-id',
        'project-id',
        100000, // 100,000 sats
        5       // 5 sat/vB fee rate
    );
    
    console.log('Investment draft:', investment);
}
```

## API Reference

### `loadAngorSdk(): Promise<IAngorSdk>`

Loads and initializes the WASM module. Must be called before using any SDK methods.

### `IAngorSdk.initialize(network: 'mainnet' | 'testnet'): Promise<SdkResult>`

Initialize the SDK with the specified Bitcoin network.

### `IAngorSdk.generateWallet(wordCount?: 12 | 24): Promise<SdkResult<WalletInfo>>`

Generate a new HD wallet with BIP39 seed words.

### `IAngorSdk.getProject(projectId: string): Promise<SdkResult<ProjectInfo>>`

Fetch project details by Angor project ID.

### `IAngorSdk.createInvestment(...): Promise<SdkResult<InvestmentDraft>>`

Create an investment transaction draft for a project.

### `IAngorSdk.signTransaction(...): Promise<SdkResult<SignedTransaction>>`

Sign a transaction with wallet seed words.

### `IAngorSdk.broadcastTransaction(...): Promise<SdkResult<BroadcastResult>>`

Broadcast a signed transaction to the Bitcoin network.

### `IAngorSdk.deriveProjectKeys(...): Promise<SdkResult<ProjectKeys>>`

Derive project-specific keys for founders.

## Type Definitions

TypeScript type definitions are available in `typescript/angor-sdk.d.ts`.

## Bundle Size

The uncompressed bundle is large due to including the .NET runtime. For production:

1. Use gzip compression (`.gz` files are pre-generated)
2. Enable HTTP/2 for parallel loading
3. Consider using a CDN

## Browser Compatibility

Requires browsers with WebAssembly support:
- Chrome 57+
- Firefox 52+
- Safari 11+
- Edge 16+

## Notes

- The SDK runs entirely in the browser - no server required
- Private keys never leave the browser
- All Bitcoin transactions are constructed and signed client-side
