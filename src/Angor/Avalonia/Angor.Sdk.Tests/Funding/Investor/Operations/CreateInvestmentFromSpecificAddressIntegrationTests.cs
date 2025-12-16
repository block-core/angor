using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Tests.Funding.TestDoubles;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Protocol;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

/// <summary>
/// Integration tests for CreateInvestmentFromSpecificAddress handler.
/// These tests actually publish transactions to Angornet (Bitcoin Signet) and verify they are correctly spent.
/// 
/// IMPORTANT: These tests require:
/// 1. Access to Angornet (Bitcoin Signet) blockchain
/// 2. Angor indexer service (https://signet.angor.online)
/// 3. Real signet bitcoins (small amounts)
/// 
/// Run these tests manually when you have Angornet setup ready.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Network", "Angornet")]
public class CreateInvestmentFromSpecificAddressIntegrationTests : IDisposable
{
    private readonly Network _network;
    private readonly WalletOperations _walletOperations;
    private readonly DerivationOperations _derivationOperations;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly IIndexerService _realIndexerService;
    private readonly CreateInvestmentFromSpecificAddress.CreateInvestmentFromSpecificAddressHandler _sut;
    private readonly ITestOutputHelper _output;

    // Test wallet words - ONLY FOR ANGORNET (SIGNET)!
    private const string TestWalletWords = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
    private const string TestWalletPassphrase = "";

    public CreateInvestmentFromSpecificAddressIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Setup network - Use Angornet (Bitcoin Signet)
        var networkConfiguration = new NetworkConfiguration();
        networkConfiguration.SetNetwork(new Angornet());
        _network = networkConfiguration.GetNetwork();

        // Create derivation operations first (needed by indexer)
        _derivationOperations = new DerivationOperations(
            new HdOperations(),
            new NullLogger<DerivationOperations>(),
            networkConfiguration);

        // Setup REAL indexer service using MempoolSpaceIndexerApi for Angornet
        // Create real HttpClientFactory implementation
        var httpClientFactory = new TestHttpClientFactory("https://signet.angor.online");
        
        // Create real NetworkService implementation
        var networkService = new TestNetworkService(
            new SettingsUrl { Name = "Angornet", Url = "https://signet.angor.online" },
            networkConfiguration);

        _realIndexerService = new MempoolSpaceIndexerApi(
            new NullLogger<MempoolSpaceIndexerApi>(),
            httpClientFactory,
            networkService,
            _derivationOperations);

        // Setup wallet operations with real indexer
        _walletOperations = new WalletOperations(
            _realIndexerService,
            new HdOperations(),
            new NullLogger<WalletOperations>(),
            networkConfiguration);

        var investorTransactionActions = new InvestorTransactionActions(
            new NullLogger<InvestorTransactionActions>(),
            new InvestmentScriptBuilder(new SeederScriptTreeBuilder()),
            new ProjectScriptsBuilder(_derivationOperations),
            new SpendingTransactionBuilder(networkConfiguration, 
                new ProjectScriptsBuilder(_derivationOperations), 
                new InvestmentScriptBuilder(new SeederScriptTreeBuilder())),
            new InvestmentTransactionBuilder(networkConfiguration, 
                new ProjectScriptsBuilder(_derivationOperations), 
                new InvestmentScriptBuilder(new SeederScriptTreeBuilder()), 
                new TaprootScriptBuilder()),
            new TaprootScriptBuilder(),
            networkConfiguration);

        // Setup mocks for non-critical services (project metadata, wallet data)
        _mockProjectService = new Mock<IProjectService>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();
        
        // Use REAL mempool monitoring service for integration tests
        var realMempoolService = new MempoolMonitoringService(
            _realIndexerService,
            new NullLogger<MempoolMonitoringService>());

        // Create the handler
        _sut = new CreateInvestmentFromSpecificAddress.CreateInvestmentFromSpecificAddressHandler(
            _mockProjectService.Object,
            investorTransactionActions,
            _mockSeedwordsProvider.Object,
            _walletOperations,
            _derivationOperations,
            _mockWalletAccountBalanceService.Object,
            realMempoolService, // ← Using REAL service, not mock!
            new NullLogger<CreateInvestmentFromSpecificAddress.CreateInvestmentFromSpecificAddressHandler>());
    }

    [Fact]
    public async Task CreateAndPublishInvestment_WithRealAngornetFunds_PublishesSuccessfully()
    {
        // Arrange
        var words = new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);
        
        // Generate a funding address
        await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        var fundingAddress = accountInfo.AddressesInfo.First();

        // Create test project
        var project = CreateTestProject();
        var walletId = new WalletId(accountInfo.walletId);

        // Setup mocks
        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(project));

        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(It.IsAny<string>()))
            .ReturnsAsync(Result.Success((TestWalletWords, Maybe<string>.None)));

        var accountBalance = new AccountBalanceInfo();
        accountBalance.UpdateAccountBalanceInfo(accountInfo, new List<UtxoData>());
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(accountBalance));

        // Use the Angornet miner faucet to automatically fund the address
        _output.WriteLine(new string('=', 80));
        _output.WriteLine("💰 Using Angornet Miner Faucet to fund test address");
        _output.WriteLine($"   Target Address: {fundingAddress.Address}");
        _output.WriteLine($"   Amount: 60,000,000 sats (0.6 BTC)");
        _output.WriteLine(new string('=', 80));

        var minerFaucet = new AngornetMinerFaucet(
            _walletOperations,
            _realIndexerService,
            _network,
            new NullLogger<AngornetMinerFaucet>());
        
        // The REAL mempool monitoring service will detect the funds on Angornet
        // No mocking - this is a real integration test!

        var request = new CreateInvestmentFromSpecificAddress.CreateInvestmentFromSpecificAddressRequest(
            walletId,
            project.Id,
            new Amount(50000000), // 0.5 BTC investment
            new DomainFeerate(10000), // 10 sat/vB
            fundingAddress.Address,
            null,
            null);

        // Act
        var actTask =  _sut.Handle(request, CancellationToken.None);
        
        await Task.Delay(10000); // Wait a bit before funding to ensure monitoring is active
        
        // Fund the address using the miner wallet
        var fundingTxId = await minerFaucet.FundAddressAsync(
            fundingAddress.Address,
            60000000, // 0.6 BTC
            10); // 10 sat/vB fee

        var result = await actTask;
        
        _output.WriteLine($"✅ Funding transaction published: {fundingTxId}");
        _output.WriteLine($"   Explorer: https://signet.angor.online/tx/{fundingTxId}");
        _output.WriteLine("\nWaiting for mempool monitoring to detect the funds...");
        
        // Assert - Transaction creation
        var error = result.IsFailure ? result.Error : string.Empty;
        
        Assert.True(result.IsSuccess, $"Handler failed: {error}");
        Assert.NotNull(result.Value.InvestmentDraft);
        Assert.NotNull(result.Value.InvestmentDraft.SignedTxHex);
        var txId = result.Value.InvestmentDraft.TransactionId;
        Assert.NotNull(txId);

        // Act - Publish the transaction to Angornet (signet)
        var transaction = _network.CreateTransaction(result.Value.InvestmentDraft.SignedTxHex);
        var publishResult = await _walletOperations.PublishTransactionAsync(_network, transaction);

        // Assert - Publication
        Assert.True(publishResult.Success, $"Failed to publish transaction: {publishResult.Message}");
        Assert.Equal(transaction, publishResult.Data);

        // Output transaction details for manual verification
        _output.WriteLine("✅ Transaction published successfully!");
        _output.WriteLine($"Transaction ID: {txId}");
        _output.WriteLine($"Explorer URL: https://signet.angor.online/tx/{txId}");
        _output.WriteLine($"Funding Address: {fundingAddress.Address}");
        _output.WriteLine($"Investment Amount: {result.Value.InvestmentDraft.AngorFee.Sats} sats");
        _output.WriteLine($"Miner Fee: {result.Value.InvestmentDraft.MinerFee.Sats} sats");
        _output.WriteLine($"Total Fee: {result.Value.InvestmentDraft.TransactionFee.Sats} sats");
        
        // Manual verification step: Check the transaction on Angor explorer
        _output.WriteLine($"\nPlease manually verify the transaction at:");
        _output.WriteLine($"https://signet.angor.online/tx/{txId}");
    }

    [Fact(Skip = "Integration test - requires Angornet setup. Run manually.")]
    public async Task VerifyInvestmentTransaction_ChecksOutputsCorrectly()
    {
        // This test verifies that an investment transaction has the correct outputs
        // Run this AFTER the CreateAndPublishInvestment test succeeds

        var txId = "YOUR_TRANSACTION_ID_HERE"; // Replace with actual txId from previous test

        _output.WriteLine($"Manual Verification Steps:");
        _output.WriteLine($"1. Go to: https://signet.angor.online/tx/{txId}");
        _output.WriteLine($"2. Verify the transaction has inputs from the funding address");
        _output.WriteLine($"3. Verify there are investment outputs (taproot or OP_RETURN)");
        _output.WriteLine($"4. Check that total fees are reasonable");
        _output.WriteLine($"5. Confirm transaction is confirmed in a block");
        
        // Placeholder assertion
        Assert.True(true, "This test requires manual verification");
        
        await Task.CompletedTask;
    }

    [Fact(Skip = "Integration test - requires Angornet (signet) funds. Run manually when you have signet bitcoins.")]
    public async Task EndToEnd_CreateMonitorAndPublish_WorksWithRealMempool()
    {
        // This is a full end-to-end test that:
        // 1. Creates a funding address
        // 2. Monitors mempool for funds (you need to send signet BTC manually)
        // 3. Creates and signs the investment transaction
        // 4. Publishes to Angornet (signet)
        // 5. Verifies it's in mempool

        // Arrange
        var words = new WalletWords { Words = TestWalletWords, Passphrase = TestWalletPassphrase };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);
        await _walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);
        var fundingAddress = accountInfo.AddressesInfo.First();

        _output.WriteLine($"📍 Send signet BTC to: {fundingAddress.Address}");
        _output.WriteLine($"You can get signet BTC from: https://signetfaucet.com/");
        _output.WriteLine($"Or use Angor faucet (if available)");
        _output.WriteLine($"Waiting for funds...");

        // TODO: Implement real mempool monitoring
        // For now, this test documents the expected flow

        Assert.True(true, "This test is a documentation of the expected integration flow");
    }

    private Project CreateTestProject()
    {
        var founderKey = _derivationOperations.DeriveFounderKey(
            new WalletWords { Words = TestWalletWords }, 1);

         var id = _derivationOperations.DeriveAngorKey(NetworkConfiguration.AngorTestKey, founderKey);
        
        return new Project
        {
            Id = new ProjectId(id),
            Name = "Integration Test Project",
            ShortDescription = "Test project for integration testing",
            FounderKey = founderKey,
            FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(
                new WalletWords { Words = TestWalletWords }, founderKey),
            NostrPubKey = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)),
            ProjectType = ProjectType.Invest,
            TargetAmount = Money.Coins(10).Satoshi, // 10 BTC target
            StartingDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddDays(90),
            EndDate = DateTime.UtcNow.AddDays(90),
            PenaltyDuration = TimeSpan.FromDays(10),
            Stages = new List<Angor.Sdk.Funding.Projects.Domain.Stage>
            {
                new Angor.Sdk.Funding.Projects.Domain.Stage { RatioOfTotal = 0.33m, ReleaseDate = DateTime.UtcNow.AddDays(30), Index = 0 },
                new Angor.Sdk.Funding.Projects.Domain.Stage { RatioOfTotal = 0.33m, ReleaseDate = DateTime.UtcNow.AddDays(60), Index = 1 },
                new Angor.Sdk.Funding.Projects.Domain.Stage { RatioOfTotal = 0.34m, ReleaseDate = DateTime.UtcNow.AddDays(90), Index = 2 }
            }
        };
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

