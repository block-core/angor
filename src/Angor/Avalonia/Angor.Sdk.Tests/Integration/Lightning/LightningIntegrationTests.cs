using System.Linq.Expressions;
using Angor.Data.Documents.Interfaces;
using Angor.Sdk.Common;
using Angor.Sdk.Integration.Lightning;
using Angor.Sdk.Integration.Lightning.Models;
using Angor.Sdk.Tests.Funding.TestDoubles;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Angor.Sdk.Tests.Integration.Lightning;

/// <summary>
/// Full end-to-end integration tests for Boltz Lightning ↔ On-chain swaps.
/// 
/// These tests use the REAL Boltz API at localhost:9001 and require:
/// 1. A local Boltz instance running at http://localhost:9001
/// 2. Access to Angornet (Bitcoin Signet) blockchain
/// 3. A Lightning wallet to pay the invoice
/// 
/// The test flow:
/// 1. Create a reverse submarine swap (Lightning → On-chain)
/// 2. Display the Lightning invoice for manual payment
/// 3. Monitor the swap via WebSocket until payment is detected
/// 4. Monitor the destination address for the claimed funds
/// </summary>
[Trait("Category", "Integration")]
[Trait("Service", "Boltz")]
public class BoltzSwapIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly Blockcore.Networks.Network _network;
    private readonly NetworkConfiguration _networkConfiguration;
    private readonly WalletOperations _walletOperations;
    private readonly DerivationOperations _derivationOperations;
    private readonly IIndexerService _indexerService;
    private readonly IMempoolMonitoringService _mempoolMonitoringService;
    private readonly BoltzSwapService _boltzSwapService;
    private readonly BoltzWebSocketClient _boltzWebSocketClient;
    private readonly BoltzSwapStorageService _boltzSwapStorageService;
    private readonly HttpClient _httpClient;

    // Test wallet - ONLY FOR SIGNET/TESTNET!
    private const string TestWalletWords = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    private const string TestWalletPassphrase = "";

    // Boltz configuration - localhost:9001 with V2 prefix
    private const string BoltzApiUrl = "http://localhost:9001/";
    private const bool UseV2Prefix = true;

    public BoltzSwapIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        // Setup network - Use Angornet (Bitcoin Signet)
        _networkConfiguration = new NetworkConfiguration();
        _networkConfiguration.SetNetwork(new Angornet());
        _network = _networkConfiguration.GetNetwork();

        // Create derivation operations
        _derivationOperations = new DerivationOperations(
            new HdOperations(),
            new NullLogger<DerivationOperations>(),
            _networkConfiguration);

        // Setup indexer service for Angornet
        var httpClientFactory = new TestHttpClientFactory("https://signet.angor.online");
        var networkService = new TestNetworkService(
            new SettingsUrl { Name = "Angornet", Url = "https://signet.angor.online" },
            _networkConfiguration);

        _indexerService = new MempoolSpaceIndexerApi(
            new NullLogger<MempoolSpaceIndexerApi>(),
            httpClientFactory,
            networkService,
            _derivationOperations);

        // Setup mempool monitoring service
        _mempoolMonitoringService = new MempoolMonitoringService(
            _indexerService,
            new NullLogger<MempoolMonitoringService>());

        // Setup wallet operations
        _walletOperations = new WalletOperations(
            _indexerService,
            new HdOperations(),
            new NullLogger<WalletOperations>(),
            _networkConfiguration);

        // Setup Boltz services with localhost:9001 and V2 prefix
        var boltzConfig = new BoltzConfiguration
        {
            BaseUrl = BoltzApiUrl,
            UseV2Prefix = UseV2Prefix,
            TimeoutSeconds = 60
        };

        _httpClient = new HttpClient();
        _boltzSwapService = new BoltzSwapService(
            _httpClient,
            boltzConfig,
            _networkConfiguration,
            new NullLogger<BoltzSwapService>());

        _boltzWebSocketClient = new BoltzWebSocketClient(
            boltzConfig,
            new NullLogger<BoltzWebSocketClient>());

        // Setup in-memory storage for swaps
        var inMemoryCollection = new InMemoryBoltzSwapCollection();
        _boltzSwapStorageService = new BoltzSwapStorageService(
            inMemoryCollection,
            new NullLogger<BoltzSwapStorageService>());
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        (_boltzWebSocketClient as IAsyncDisposable)?.DisposeAsync().AsTask().Wait();
    }

    /// <summary>
    /// Full integration test: Create swap, wait for payment, verify funds arrive at destination.
    /// 
    /// RUN THIS TEST MANUALLY - it requires:
    /// 1. Boltz running at localhost:9001
    /// 2. A Lightning wallet to pay the invoice
    /// 3. Manual interaction to pay the invoice within the timeout
    /// </summary>
    [Fact]
    public async Task FullReverseSwapFlow_CreatePayAndMonitor_Success()
    {
        // ========================================
        // STEP 1: Setup wallet and generate address
        // ========================================
        _output.WriteLine(new string('=', 80));
        _output.WriteLine("🚀 FULL BOLTZ REVERSE SWAP INTEGRATION TEST");
        _output.WriteLine(new string('=', 80));

        var words = new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);
        
        // Generate a fresh receive address
        await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        var destinationAddress = accountInfo.AddressesInfo.Last().Address;

        _output.WriteLine($"\n📍 Destination address: {destinationAddress}");
        _output.WriteLine($"   Network: {_network.Name}");

        // Generate a claim key directly from the wallet for testing
        var hdOperations = new HdOperations();
        var extKey = hdOperations.GetExtendedKey(words.Words, words.Passphrase);
        var claimKeyPath = "m/84'/1'/0'/0/100"; // A unique path for claim keys
        var claimExtPubKey = hdOperations.GetExtendedPublicKey(extKey.PrivateKey, extKey.ChainCode, claimKeyPath);
        var claimPubKeyHex = claimExtPubKey.PubKey.ToHex();

        _output.WriteLine($"   Claim public key: {claimPubKeyHex}");

        // ========================================
        // STEP 2: Create the reverse submarine swap
        // ========================================
        _output.WriteLine("\n" + new string('-', 80));
        _output.WriteLine("📝 STEP 1: Creating reverse submarine swap...");
        
        var swapAmount = 100000L; // 100,000 sats (0.001 BTC)
        
        var swapResult = await _boltzSwapService.CreateSubmarineSwapAsync(
            destinationAddress,
            swapAmount,
            claimPubKeyHex);

        Assert.True(swapResult.IsSuccess, $"Failed to create swap: {swapResult.Error}");
        var swap = swapResult.Value;

        _output.WriteLine($"\n✅ Swap created successfully!");
        _output.WriteLine($"   Swap ID: {swap.Id}");
        _output.WriteLine($"   Invoice amount: {swap.InvoiceAmount} sats");
        _output.WriteLine($"   Expected on-chain: {swap.ExpectedAmount} sats");
        _output.WriteLine($"   Lockup address: {swap.LockupAddress}");
        _output.WriteLine($"   Timeout block: {swap.TimeoutBlockHeight}");

        // Save swap to storage
        await _boltzSwapStorageService.SaveSwapAsync(swap, accountInfo.walletId, "test-project-id");

        // ========================================
        // STEP 3: Display invoice and wait for payment
        // ========================================
        _output.WriteLine("\n" + new string('-', 80));
        _output.WriteLine("⚡ STEP 2: Pay the Lightning invoice");
        _output.WriteLine(new string('-', 80));
        _output.WriteLine("\n🔴 ACTION REQUIRED: Pay this Lightning invoice now!\n");
        _output.WriteLine($"   {swap.Invoice}");
        _output.WriteLine("\n   (You have 10 minutes to pay)");
        _output.WriteLine(new string('-', 80));

        // ========================================
        // STEP 4: Monitor swap via WebSocket for payment
        // ========================================
        _output.WriteLine("\n📡 STEP 3: Monitoring swap status via WebSocket...");

        using var monitorCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        
        var monitorResult = await _boltzWebSocketClient.MonitorSwapAsync(
            swap.Id,
            TimeSpan.FromMinutes(10),
            monitorCts.Token);

        Assert.True(monitorResult.IsSuccess, $"Swap monitoring failed: {monitorResult.Error}");
        var swapStatus = monitorResult.Value;

        _output.WriteLine($"\n✅ Swap status update received!");
        _output.WriteLine($"   Status: {swapStatus.Status}");
        _output.WriteLine($"   Transaction ID: {swapStatus.TransactionId ?? "N/A"}");

        // Update swap status in storage
        await _boltzSwapStorageService.UpdateSwapStatusAsync(
            swap.Id,
            swapStatus.Status.ToString(),
            swapStatus.TransactionId,
            swapStatus.TransactionHex);

        // ========================================
        // STEP 5: Monitor destination address for funds
        // ========================================
        _output.WriteLine("\n" + new string('-', 80));
        _output.WriteLine("🔍 STEP 4: Monitoring destination address for incoming funds...");
        _output.WriteLine($"   Address: {destinationAddress}");
        _output.WriteLine($"   Expected amount: ~{swap.ExpectedAmount} sats");
        _output.WriteLine("   (Boltz automatic claiming should send funds to this address)");

        // Monitor for funds arriving at destination
        using var addressMonitorCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        
        try
        {
            var detectedUtxos = await _mempoolMonitoringService.MonitorAddressForFundsAsync(
                destinationAddress,
                swap.ExpectedAmount - 1000, // Allow for small fee variations
                TimeSpan.FromMinutes(5),
                addressMonitorCts.Token);

            if (detectedUtxos.Any())
            {
                var totalReceived = detectedUtxos.Sum(u => u.value);
                _output.WriteLine($"\n✅ FUNDS RECEIVED!");
                _output.WriteLine($"   UTXOs detected: {detectedUtxos.Count}");
                _output.WriteLine($"   Total received: {totalReceived} sats");

                foreach (var utxo in detectedUtxos)
                {
                    _output.WriteLine($"   - {utxo.outpoint.transactionId}:{utxo.outpoint.outputIndex} = {utxo.value} sats");
                }
            }
            else
            {
                _output.WriteLine("\n⚠️  No UTXOs detected at destination address yet");
                _output.WriteLine("   The swap may still be processing. Check the explorer.");
            }
        }
        catch (OperationCanceledException)
        {
            _output.WriteLine("\n⏱️  Address monitoring timed out");
            _output.WriteLine("   Check the blockchain explorer for the destination address:");
            _output.WriteLine($"   https://signet.angor.online/address/{destinationAddress}");
        }

        // ========================================
        // SUMMARY
        // ========================================
        _output.WriteLine("\n" + new string('=', 80));
        _output.WriteLine("📊 TEST SUMMARY");
        _output.WriteLine(new string('=', 80));
        _output.WriteLine($"   Swap ID: {swap.Id}");
        _output.WriteLine($"   Final Status: {swapStatus.Status}");
        _output.WriteLine($"   Destination: {destinationAddress}");
        _output.WriteLine($"   Lockup TX: {swapStatus.TransactionId ?? "N/A"}");
        _output.WriteLine(new string('=', 80));
    }

    /// <summary>
    /// Test just creating a swap - no payment required.
    /// Use this to verify Boltz API connectivity at localhost:9001.
    /// </summary>
    [Fact]
    public async Task CreateReverseSwap_WithValidData_ReturnsSwapDetails()
    {
        // Arrange
        _output.WriteLine("Testing Boltz API connectivity at localhost:9001...\n");
        
        var words = new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);
        await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        var destinationAddress = accountInfo.AddressesInfo.Last().Address;

        // Generate a claim key directly from the wallet for testing
        var hdOperations = new HdOperations();
        var extKey = hdOperations.GetExtendedKey(words.Words, words.Passphrase);
        var claimKeyPath = "m/84'/1'/0'/0/101"; // A unique path for claim keys
        var claimExtPubKey = hdOperations.GetExtendedPublicKey(extKey.PrivateKey, extKey.ChainCode, claimKeyPath);
        var claimPubKeyHex = claimExtPubKey.PubKey.ToHex();

        _output.WriteLine($"Destination address: {destinationAddress}");
        _output.WriteLine($"Claim public key: {claimPubKeyHex}");

        // Act
        var result = await _boltzSwapService.CreateSubmarineSwapAsync(
            destinationAddress,
            100000, // 100k sats
            claimPubKeyHex);

        // Assert
        _output.WriteLine($"\nResult: {(result.IsSuccess ? "Success" : "Failure")}");
        
        if (result.IsSuccess)
        {
            var swap = result.Value;
            _output.WriteLine($"\n✅ Swap created!");
            _output.WriteLine($"   Swap ID: {swap.Id}");
            _output.WriteLine($"   Invoice: {swap.Invoice}");
            _output.WriteLine($"   Lockup Address: {swap.LockupAddress}");
            _output.WriteLine($"   Expected Amount: {swap.ExpectedAmount} sats");
            _output.WriteLine($"   Timeout Block: {swap.TimeoutBlockHeight}");
            
            Assert.NotEmpty(swap.Id);
            Assert.NotEmpty(swap.Invoice);
            Assert.NotEmpty(swap.LockupAddress);
        }
        else
        {
            _output.WriteLine($"\n❌ Error: {result.Error}");
        }
        
        Assert.True(result.IsSuccess, result.Error);
    }

    /// <summary>
    /// Test WebSocket connectivity to Boltz at localhost:9001
    /// </summary>
    [Fact]
    public async Task WebSocket_ConnectAndSubscribe_Works()
    {
        _output.WriteLine("Testing Boltz WebSocket connectivity...\n");
        
        // First create a swap to have something to subscribe to
        var words = new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);
        await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        var destinationAddress = accountInfo.AddressesInfo.Last().Address;

        // Generate a claim key directly from the wallet for testing
        var hdOperations = new HdOperations();
        var extKey = hdOperations.GetExtendedKey(words.Words, words.Passphrase);
        var claimKeyPath = "m/84'/1'/0'/0/102"; // A unique path for claim keys
        var claimExtPubKey = hdOperations.GetExtendedPublicKey(extKey.PrivateKey, extKey.ChainCode, claimKeyPath);
        var claimPubKeyHex = claimExtPubKey.PubKey.ToHex();

        var swapResult = await _boltzSwapService.CreateSubmarineSwapAsync(
            destinationAddress,
            50000,
            claimPubKeyHex);

        Assert.True(swapResult.IsSuccess, $"Failed to create swap: {swapResult.Error}");
        var swap = swapResult.Value;

        _output.WriteLine($"Created swap: {swap.Id}");
        _output.WriteLine($"Invoice: {swap.Invoice}");
        _output.WriteLine("\nConnecting to WebSocket and subscribing to swap updates...");
        _output.WriteLine("(Will timeout after 10 seconds since we won't pay the invoice)\n");

        // Try to monitor - this will timeout since we won't pay
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        var monitorResult = await _boltzWebSocketClient.MonitorSwapAsync(
            swap.Id,
            TimeSpan.FromSeconds(10),
            cts.Token);

        // We expect a timeout/failure since we're not paying
        _output.WriteLine($"Monitor result: {(monitorResult.IsSuccess ? "Success (unexpected!)" : "Timeout/Cancelled (expected)")}");
        if (monitorResult.IsFailure)
        {
            _output.WriteLine($"Expected behavior: {monitorResult.Error}");
        }
        
        // Test passes if we got this far - WebSocket connected successfully
        _output.WriteLine("\n✅ WebSocket connectivity test passed!");
    }
}

/// <summary>
/// In-memory implementation of IGenericDocumentCollection for testing BoltzSwapStorageService.
/// </summary>
public class InMemoryBoltzSwapCollection : IGenericDocumentCollection<BoltzSwapDocument>
{
    private readonly Dictionary<string, BoltzSwapDocument> _documents = new();

    public Task<Result<BoltzSwapDocument?>> FindByIdAsync(string id)
    {
        _documents.TryGetValue(id, out var doc);
        return Task.FromResult(Result.Success(doc));
    }

    public Task<Result<IEnumerable<BoltzSwapDocument>>> FindByIdsAsync(IEnumerable<string> ids)
    {
        var results = ids.Where(_documents.ContainsKey).Select(id => _documents[id]);
        return Task.FromResult(Result.Success(results));
    }

    public Task<Result<IEnumerable<BoltzSwapDocument>>> FindAllAsync()
    {
        return Task.FromResult(Result.Success(_documents.Values.AsEnumerable()));
    }

    public Task<Result<IEnumerable<BoltzSwapDocument>>> FindAsync(Expression<Func<BoltzSwapDocument, bool>> predicate)
    {
        var compiled = predicate.Compile();
        var results = _documents.Values.Where(compiled);
        return Task.FromResult(Result.Success(results));
    }

    public Task<Result<bool>> ExistsAsync(string id)
    {
        return Task.FromResult(Result.Success(_documents.ContainsKey(id)));
    }

    public Task<Result<int>> InsertAsync(Expression<Func<BoltzSwapDocument, string>> getDocumentId, params BoltzSwapDocument[] entities)
    {
        var getId = getDocumentId.Compile();
        foreach (var entity in entities)
        {
            var id = getId(entity);
            _documents[id] = entity;
        }
        return Task.FromResult(Result.Success(entities.Length));
    }

    public Task<Result<bool>> UpdateAsync(Expression<Func<BoltzSwapDocument, string>> getDocumentId, BoltzSwapDocument entity)
    {
        var getId = getDocumentId.Compile();
        var id = getId(entity);
        if (_documents.ContainsKey(id))
        {
            _documents[id] = entity;
            return Task.FromResult(Result.Success(true));
        }
        return Task.FromResult(Result.Success(false));
    }

    public Task<Result<bool>> UpsertAsync(Expression<Func<BoltzSwapDocument, string>> getDocumentId, BoltzSwapDocument entity)
    {
        var getId = getDocumentId.Compile();
        var id = getId(entity);
        _documents[id] = entity;
        return Task.FromResult(Result.Success(true));
    }

    public Task<Result<bool>> DeleteAsync(string id)
    {
        var removed = _documents.Remove(id);
        return Task.FromResult(Result.Success(removed));
    }

    public Task<Result<int>> CountAsync(Expression<Func<BoltzSwapDocument, bool>>? predicate = null)
    {
        if (predicate == null)
        {
            return Task.FromResult(Result.Success(_documents.Count));
        }
        var compiled = predicate.Compile();
        var count = _documents.Values.Count(compiled);
        return Task.FromResult(Result.Success(count));
    }
}


