/**
 * TypeScript type definitions for Angor SDK WASM bindings.
 * 
 * Usage:
 * ```typescript
 * import { AngorSdk } from './angor-sdk';
 * 
 * const sdk = await AngorSdk.create();
 * const result = await sdk.initialize('testnet');
 * ```
 */

export interface SdkResult<T = unknown> {
  success: boolean;
  error?: string;
  message?: string;
  data?: T;
}

export interface WalletInfo {
  seedWords: string[];
  publicKey: string;
  address: string;
}

export interface ProjectInfo {
  id: string;
  name: string;
  description: string;
  targetAmount: number;
  stages: StageInfo[];
  founderPubKey: string;
  nostrPubKey: string;
}

export interface StageInfo {
  index: number;
  percentage: number;
  releaseDate?: string;
}

export interface InvestmentDraft {
  transactionHex: string;
  totalAmount: number;
  fee: number;
  stageBreakdown: StageAmount[];
}

export interface StageAmount {
  stageIndex: number;
  amount: number;
}

export interface SignedTransaction {
  transactionHex: string;
  txId: string;
}

export interface BroadcastResult {
  txId: string;
  confirmed: boolean;
}

/**
 * Angor SDK interface for TypeScript consumers.
 * All methods return promises that resolve to SdkResult objects.
 */
export interface IAngorSdk {
  /**
   * Initialize the SDK with network configuration.
   * @param network - 'mainnet' or 'testnet'
   */
  initialize(network: 'mainnet' | 'testnet'): Promise<SdkResult>;

  /**
   * Generate a new wallet with BIP39 seed words.
   * @param wordCount - Number of seed words (12 or 24)
   */
  generateWallet(wordCount?: 12 | 24): Promise<SdkResult<WalletInfo>>;

  /**
   * Fetch project details by project ID.
   * @param projectId - The Angor project identifier
   */
  getProject(projectId: string): Promise<SdkResult<ProjectInfo>>;

  /**
   * Create an investment transaction draft.
   * @param walletId - Wallet identifier
   * @param projectId - Target project ID
   * @param amountSats - Investment amount in satoshis
   * @param feeRateSatsPerVb - Fee rate in sats/vByte
   */
  createInvestment(
    walletId: string,
    projectId: string,
    amountSats: number,
    feeRateSatsPerVb: number
  ): Promise<SdkResult<InvestmentDraft>>;

  /**
   * Sign a transaction with wallet credentials.
   * @param transactionHex - Unsigned transaction hex
   * @param walletSeedWords - Space-separated seed words
   */
  signTransaction(
    transactionHex: string,
    walletSeedWords: string
  ): Promise<SdkResult<SignedTransaction>>;

  /**
   * Broadcast a signed transaction to the network.
   * @param signedTransactionHex - Signed transaction hex
   */
  broadcastTransaction(
    signedTransactionHex: string
  ): Promise<SdkResult<BroadcastResult>>;

  /**
   * Derive project keys for a founder.
   * @param walletSeedWords - Founder's wallet seed words
   * @param angorRootKey - Angor root key for derivation
   */
  deriveProjectKeys(
    walletSeedWords: string,
    angorRootKey: string
  ): Promise<SdkResult<{ founderKey: string; recoveryKey: string }>>;
}

/**
 * Load and initialize the Angor SDK WASM module.
 * Uses Blazor WebAssembly and DotNet.invokeMethodAsync for interop.
 * 
 * @example
 * ```typescript
 * // In your HTML, include the Blazor script first:
 * // <script src="_framework/blazor.webassembly.js"></script>
 * // <script src="angor-sdk.js"></script>
 * 
 * const sdk = await loadAngorSdk();
 * const result = await sdk.initialize('testnet');
 * ```
 */
export function loadAngorSdk(): Promise<IAngorSdk>;

// Global declaration for script tag usage
declare global {
  interface Window {
    loadAngorSdk: typeof loadAngorSdk;
    angorSdkReady: Promise<void>;
    DotNet: {
      invokeMethodAsync<T>(assemblyName: string, methodName: string, ...args: unknown[]): Promise<T>;
    };
  }
}
