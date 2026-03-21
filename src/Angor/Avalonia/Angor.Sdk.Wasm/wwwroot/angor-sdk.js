/**
 * Angor SDK - JavaScript wrapper for C# WASM bindings
 * Usage:
 *   const sdk = await loadAngorSdk();
 *   const result = await sdk.initialize('testnet');
 */

// Wait for Blazor to be ready
window.angorSdkReady = new Promise((resolve) => {
    window.angorSdkReadyResolver = resolve;
});

// Called after Blazor WASM is initialized
Blazor.start().then(() => {
    window.angorSdkReadyResolver();
});

/**
 * Load and initialize the Angor SDK
 * @returns {Promise<IAngorSdk>} The SDK interface
 */
async function loadAngorSdk() {
    await window.angorSdkReady;
    
    return {
        /**
         * Initialize the SDK with network configuration
         * @param {string} network - 'mainnet' or 'testnet'
         * @returns {Promise<SdkResult>}
         */
        async initialize(network) {
            const result = await DotNet.invokeMethodAsync('Angor.Sdk.Wasm', 'Initialize', network);
            return JSON.parse(result);
        },

        /**
         * Generate a new wallet with seed words
         * @param {number} wordCount - Number of seed words (12 or 24)
         * @returns {Promise<SdkResult<{seedWords: string[]}>>}
         */
        async generateWallet(wordCount = 12) {
            const result = await DotNet.invokeMethodAsync('Angor.Sdk.Wasm', 'GenerateWallet', wordCount);
            return JSON.parse(result);
        },

        /**
         * Get project details by ID
         * @param {string} projectId - The project ID (nostr pubkey)
         * @returns {Promise<SdkResult<ProjectInfo>>}
         */
        async getProject(projectId) {
            const result = await DotNet.invokeMethodAsync('Angor.Sdk.Wasm', 'GetProject', projectId);
            return JSON.parse(result);
        },

        /**
         * Create an investment transaction
         * @param {string} walletId - The investor's wallet ID
         * @param {string} projectId - The target project ID
         * @param {number} amountSats - Investment amount in satoshis
         * @param {number} feeRateSatsPerVb - Fee rate in sats/vB
         * @returns {Promise<SdkResult<InvestmentDraft>>}
         */
        async createInvestment(walletId, projectId, amountSats, feeRateSatsPerVb) {
            const result = await DotNet.invokeMethodAsync(
                'Angor.Sdk.Wasm',
                'CreateInvestment',
                walletId,
                projectId,
                amountSats,
                feeRateSatsPerVb
            );
            return JSON.parse(result);
        },

        /**
         * Sign a transaction with wallet credentials
         * @param {string} transactionHex - Raw transaction hex
         * @param {string} walletSeedWords - Space-separated seed words
         * @returns {Promise<SdkResult<{signedTxHex: string}>>}
         */
        async signTransaction(transactionHex, walletSeedWords) {
            const result = await DotNet.invokeMethodAsync(
                'Angor.Sdk.Wasm',
                'SignTransaction',
                transactionHex,
                walletSeedWords
            );
            return JSON.parse(result);
        },

        /**
         * Broadcast a signed transaction
         * @param {string} signedTransactionHex - Signed transaction hex
         * @returns {Promise<SdkResult<{txId: string}>>}
         */
        async broadcastTransaction(signedTransactionHex) {
            const result = await DotNet.invokeMethodAsync(
                'Angor.Sdk.Wasm',
                'BroadcastTransaction',
                signedTransactionHex
            );
            return JSON.parse(result);
        },

        /**
         * Derive project keys for a founder
         * @param {string} walletSeedWords - Space-separated seed words
         * @param {string} angorRootKey - Angor root key
         * @returns {Promise<SdkResult<{founderKey: string, nostrKey: string}>>}
         */
        async deriveProjectKeys(walletSeedWords, angorRootKey) {
            const result = await DotNet.invokeMethodAsync(
                'Angor.Sdk.Wasm',
                'DeriveProjectKeys',
                walletSeedWords,
                angorRootKey
            );
            return JSON.parse(result);
        }
    };
}

// Export for ES modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { loadAngorSdk };
}

// Export globally
window.loadAngorSdk = loadAngorSdk;
