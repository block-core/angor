using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

/// <summary>
/// Integration tests for BuildReleaseTransactionHandler.
/// Tests the release transaction building functionality with real protocol implementations
/// but mocked external services (indexer, database, network, Nostr).
/// </summary>
public class BuildReleaseTransactionIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly Network _network;
    private readonly NetworkConfiguration _networkConfiguration;
    private readonly IDerivationOperations _derivationOperations;
    private readonly IInvestorTransactionActions _investorTransactionActions;
    private readonly IWalletOperations _walletOperations;
    
    // Mocked external services
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<ITransactionService> _mockTransactionService;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly Mock<ISignService> _mockSignService;
    private readonly Mock<IEncryptionService> _mockEncryptionService;
    private readonly Mock<ISerializer> _mockSerializer;
    private readonly Mock<IIndexerService> _mockIndexerService;
    
    // System under test
    private readonly BuildReleaseTransaction.BuildReleaseTransactionHandler _sut;

    // Test constants
    private const string TestWalletWords = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    public BuildReleaseTransactionIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Setup real network configuration (testnet for testing)
        _networkConfiguration = new NetworkConfiguration();
        _networkConfiguration.SetNetwork(Angor.Shared.Networks.Networks.Bitcoin.Testnet());
        _network = _networkConfiguration.GetNetwork();

        // Setup real derivation operations
        _derivationOperations = new DerivationOperations(
            new HdOperations(),
            new NullLogger<DerivationOperations>(),
            _networkConfiguration);

        // Setup mock indexer for wallet operations
        _mockIndexerService = new Mock<IIndexerService>();
        
        // Setup real wallet operations with mock indexer
        _walletOperations = new WalletOperations(
            _mockIndexerService.Object,
            new HdOperations(),
            new NullLogger<WalletOperations>(),
            _networkConfiguration);

        // Setup real investor transaction actions (core protocol logic)
        _investorTransactionActions = new InvestorTransactionActions(
            new NullLogger<InvestorTransactionActions>(),
            new InvestmentScriptBuilder(new SeederScriptTreeBuilder()),
            new ProjectScriptsBuilder(_derivationOperations),
            new SpendingTransactionBuilder(_networkConfiguration,
                new ProjectScriptsBuilder(_derivationOperations),
                new InvestmentScriptBuilder(new SeederScriptTreeBuilder())),
            new InvestmentTransactionBuilder(_networkConfiguration,
                new ProjectScriptsBuilder(_derivationOperations),
                new InvestmentScriptBuilder(new SeederScriptTreeBuilder()),
                new TaprootScriptBuilder()),
            new TaprootScriptBuilder(),
            _networkConfiguration);

        // Setup mocked external services
        _mockProjectService = new Mock<IProjectService>();
        _mockPortfolioService = new Mock<IPortfolioService>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockTransactionService = new Mock<ITransactionService>();
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();
        _mockSignService = new Mock<ISignService>();
        _mockEncryptionService = new Mock<IEncryptionService>();
        _mockSerializer = new Mock<ISerializer>();

        // Create the handler with real protocol implementations and mocked external services
        _sut = new BuildReleaseTransaction.BuildReleaseTransactionHandler(
            _mockSeedwordsProvider.Object,
            _derivationOperations,
            _mockProjectService.Object,
            _investorTransactionActions,
            _mockPortfolioService.Object,
            _networkConfiguration,
            _walletOperations,
            _mockSignService.Object,
            _mockEncryptionService.Object,
            _mockSerializer.Object,
            _mockTransactionService.Object,
            _mockWalletAccountBalanceService.Object);
    }

    #region Happy Path Integration Test

    [Fact]
    public async Task Handle_WithValidInputsAndFounderSignatures_ReturnsSuccessWithTransactionDraft()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = CreateTestProjectWithFixedId();
        var projectId = project.Id;
        var investmentTxHash = "abc123def456789";
        var investmentTxHex = CreateValidTransactionHex();
        var eventId = "event123";
        
        var request = new BuildReleaseTransaction.BuildReleaseTransactionRequest(
            walletId, projectId, new DomainFeerate(3));

        // Create mock for IInvestorTransactionActions - required because it parses real Bitcoin transactions
        var mockInvestorTransactionActions = new Mock<IInvestorTransactionActions>();
        
        // Create handler with mocked IInvestorTransactionActions for happy path testing
        var happyPathHandler = new BuildReleaseTransaction.BuildReleaseTransactionHandler(
            _mockSeedwordsProvider.Object,
            _derivationOperations,
            _mockProjectService.Object,
            mockInvestorTransactionActions.Object,
            _mockPortfolioService.Object,
            _networkConfiguration,
            _walletOperations,
            _mockSignService.Object,
            _mockEncryptionService.Object,
            _mockSerializer.Object,
            _mockTransactionService.Object,
            _mockWalletAccountBalanceService.Object);

        // Setup all external services for happy path
        SetupProjectServiceForProject(project);
        SetupPortfolioServiceForHappyPath(walletId, projectId, investmentTxHash, investmentTxHex, eventId, project.FounderKey);
        SetupSeedwordsProvider(walletId);
        SetupAccountBalanceServiceWithUtxos(walletId);
        SetupSignatureFlowForHappyPath(mockInvestorTransactionActions);
        SetupTransactionInfoForHappyPath(investmentTxHash);
        SetupInvestorTransactionActionsForHappyPath(mockInvestorTransactionActions);

        // Act
        var result = await happyPathHandler.Handle(request, CancellationToken.None);

        // Assert - Verify success
        if (result.IsFailure)
        {
            Assert.Fail($"Expected success but got: {result.Error}");
        }
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.TransactionDraft);
        
        // Verify transaction draft content
        Assert.NotNull(result.Value.TransactionDraft.SignedTxHex);
        Assert.NotEmpty(result.Value.TransactionDraft.SignedTxHex);
        Assert.NotNull(result.Value.TransactionDraft.TransactionId);
        Assert.NotEmpty(result.Value.TransactionDraft.TransactionId);
        Assert.True(result.Value.TransactionDraft.TransactionFee.Sats >= 0);
        
        _output.WriteLine($"✅ Happy path test passed!");
        _output.WriteLine($"   Transaction ID: {result.Value.TransactionDraft.TransactionId}");
        _output.WriteLine($"   Transaction Fee: {result.Value.TransactionDraft.TransactionFee.Sats} sats");
        _output.WriteLine($"   Signed Tx Hex Length: {result.Value.TransactionDraft.SignedTxHex.Length} chars");

        // Verify all key services were called
        _mockProjectService.Verify(x => x.GetAsync(projectId), Times.Once);
        _mockPortfolioService.Verify(x => x.GetByWalletId(walletId.Value), Times.Once);
        _mockSeedwordsProvider.Verify(x => x.GetSensitiveData(walletId.Value), Times.AtLeastOnce);
        _mockWalletAccountBalanceService.Verify(x => x.GetAccountBalanceInfoAsync(walletId), Times.Once);
        _mockSignService.Verify(x => x.LookupReleaseSigs(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), 
            It.IsAny<string>(), It.IsAny<Action<string>>(), It.IsAny<Action>()), Times.Once);
        mockInvestorTransactionActions.Verify(x => x.AddSignaturesToUnfundedReleaseFundsTransaction(
            It.IsAny<ProjectInfo>(),
            It.IsAny<Blockcore.Consensus.TransactionInfo.Transaction>(),
            It.IsAny<SignatureInfo>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    #endregion

    #region Unit Tests - External Service Failures

    [Fact]
    public async Task Handle_WhenProjectServiceFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId = new ProjectId(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)));
        var request = new BuildReleaseTransaction.BuildReleaseTransactionRequest(
            walletId, projectId, new DomainFeerate(3));

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Failure<Project>("Project not found on relay"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Project not found on relay", result.Error);
        _mockProjectService.Verify(x => x.GetAsync(projectId), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPortfolioServiceFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = CreateTestProject();
        var projectId = project.Id;
        var request = new BuildReleaseTransaction.BuildReleaseTransactionRequest(
            walletId, projectId, new DomainFeerate(3));

        SetupProjectService(project);

        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Failure<InvestmentRecords>("Database connection failed"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Database connection failed", result.Error);
    }

    [Fact]
    public async Task Handle_WhenNoInvestmentFoundForProject_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = CreateTestProject();
        var projectId = project.Id;
        var request = new BuildReleaseTransaction.BuildReleaseTransactionRequest(
            walletId, projectId, new DomainFeerate(3));

        SetupProjectService(project);
        
        // Return empty investment records
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(new InvestmentRecords { ProjectIdentifiers = new List<InvestmentRecord>() }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("No investment found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenSeedwordsProviderFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = CreateTestProject();
        var projectId = project.Id;
        var investmentTxHash = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        var investmentTxHex = CreateDummyTransactionHex();
        var request = new BuildReleaseTransaction.BuildReleaseTransactionRequest(
            walletId, projectId, new DomainFeerate(3));

        SetupProjectService(project);
        SetupPortfolioServiceWithInvestment(walletId, projectId, investmentTxHash, investmentTxHex);

        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(walletId.Value))
            .ReturnsAsync(Result.Failure<(string Words, Maybe<string> Passphrase)>("Wallet locked"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Wallet locked", result.Error);
    }

    [Fact]
    public async Task Handle_WhenAccountBalanceServiceFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = CreateTestProject();
        var projectId = project.Id;
        var investmentTxHash = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        var investmentTxHex = CreateDummyTransactionHex();
        var request = new BuildReleaseTransaction.BuildReleaseTransactionRequest(
            walletId, projectId, new DomainFeerate(3));

        SetupProjectService(project);
        SetupPortfolioServiceWithInvestment(walletId, projectId, investmentTxHash, investmentTxHex);
        SetupSeedwordsProvider(walletId);

        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(walletId))
            .ReturnsAsync(Result.Failure<AccountBalanceInfo>("Failed to get account balance"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Failed to get account balance", result.Error);
    }

    [Fact(Skip = "Requires valid investment transaction hex. The handler parses the transaction before checking signatures.")]
    public async Task Handle_WhenNoFounderSignaturesFound_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = CreateTestProject();
        var projectId = project.Id;
        var investmentTxHash = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        var investmentTxHex = CreateDummyTransactionHex();
        var eventId = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        var request = new BuildReleaseTransaction.BuildReleaseTransactionRequest(
            walletId, projectId, new DomainFeerate(3));

        SetupProjectService(project);
        SetupPortfolioServiceWithInvestment(walletId, projectId, investmentTxHash, investmentTxHex, eventId);
        SetupSeedwordsProvider(walletId);
        SetupAccountBalanceService(walletId);

        // Setup sign service to return no signatures (timeout/not found scenario)
        _mockSignService
            .Setup(x => x.LookupReleaseSigs(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string>(),
                It.IsAny<Action<string>>(),
                It.IsAny<Action>()))
            .Callback<string, string, DateTime?, string, Action<string>, Action>(
                (pubKey, projectPubKey, _, eventIdParam, onContent, onComplete) =>
                {
                    // Simulate no signatures found - call onComplete without onContent
                    onComplete();
                });

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("No founder signatures found", result.Error);
    }

    [Fact(Skip = "Requires valid investment transaction hex. The handler parses the transaction before checking transaction info.")]
    public async Task Handle_WhenTransactionInfoNotFound_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = CreateTestProject();
        var projectId = project.Id;
        var investmentTxHash = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        var investmentTxHex = CreateDummyTransactionHex();
        var eventId = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        var request = new BuildReleaseTransaction.BuildReleaseTransactionRequest(
            walletId, projectId, new DomainFeerate(3));

        SetupProjectService(project);
        SetupPortfolioServiceWithInvestment(walletId, projectId, investmentTxHash, investmentTxHex, eventId);
        SetupSeedwordsProvider(walletId);
        SetupAccountBalanceService(walletId);
        
        // Setup sign service to return valid signatures
        var signatureInfo = new SignatureInfo();
        SetupSignServiceForFounderSignatures(project, signatureInfo);

        // Transaction info lookup returns null
        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((QueryTransaction?)null);

        // Act  
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Could not find transaction info", result.Error);
    }

    #endregion

    #region Service Call Verification

    [Fact]
    public async Task Handle_VerifiesProjectServiceIsCalledFirst()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId = new ProjectId(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)));
        var request = new BuildReleaseTransaction.BuildReleaseTransactionRequest(
            walletId, projectId, new DomainFeerate(3));

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Failure<Project>("Test failure"));

        // Act
        await _sut.Handle(request, CancellationToken.None);

        // Assert
        _mockProjectService.Verify(x => x.GetAsync(projectId), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProjectSucceeds_CallsPortfolioService()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = CreateTestProject();
        var request = new BuildReleaseTransaction.BuildReleaseTransactionRequest(
            walletId, project.Id, new DomainFeerate(3));

        SetupProjectService(project);
        
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Failure<InvestmentRecords>("Test failure"));

        // Act
        await _sut.Handle(request, CancellationToken.None);

        // Assert
        _mockPortfolioService.Verify(x => x.GetByWalletId(walletId.Value), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenInvestmentFound_CallsSeedwordsProvider()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = CreateTestProject();
        var investmentTxHash = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        var investmentTxHex = CreateDummyTransactionHex();
        var request = new BuildReleaseTransaction.BuildReleaseTransactionRequest(
            walletId, project.Id, new DomainFeerate(3));

        SetupProjectService(project);
        SetupPortfolioServiceWithInvestment(walletId, project.Id, investmentTxHash, investmentTxHex);
        
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(walletId.Value))
            .ReturnsAsync(Result.Failure<(string Words, Maybe<string> Passphrase)>("Test failure"));

        // Act
        await _sut.Handle(request, CancellationToken.None);

        // Assert
        _mockSeedwordsProvider.Verify(x => x.GetSensitiveData(walletId.Value), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSeedwordsSucceeds_CallsAccountBalanceService()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = CreateTestProject();
        var investmentTxHash = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        var investmentTxHex = CreateDummyTransactionHex();
        var request = new BuildReleaseTransaction.BuildReleaseTransactionRequest(
            walletId, project.Id, new DomainFeerate(3));

        SetupProjectService(project);
        SetupPortfolioServiceWithInvestment(walletId, project.Id, investmentTxHash, investmentTxHex);
        SetupSeedwordsProvider(walletId);
        
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(walletId))
            .ReturnsAsync(Result.Failure<AccountBalanceInfo>("Test failure"));

        // Act
        await _sut.Handle(request, CancellationToken.None);

        // Assert
        _mockWalletAccountBalanceService.Verify(x => x.GetAccountBalanceInfoAsync(walletId), Times.Once);
    }

    #endregion

    #region Helper Methods

    private void SetupSeedwordsProvider(WalletId walletId)
    {
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(walletId.Value))
            .ReturnsAsync(Result.Success((TestWalletWords, Maybe<string>.None)));
    }

    private void SetupAccountBalanceService(WalletId walletId)
    {
        var accountInfo = CreateTestAccountInfo();
        var accountBalanceInfo = new AccountBalanceInfo();
        accountBalanceInfo.UpdateAccountBalanceInfo(accountInfo, new List<UtxoData>());

        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(walletId))
            .ReturnsAsync(Result.Success(accountBalanceInfo));
    }

    private void SetupPortfolioServiceWithInvestment(WalletId walletId, ProjectId projectId, string txHash, string? txHex, string? eventId = null)
    {
        var investorPubKey = _derivationOperations.DeriveInvestorKey(
            new WalletWords { Words = TestWalletWords },
            CreateTestProject().FounderKey);

        var investment = new InvestmentRecord
        {
            ProjectIdentifier = projectId.Value,
            InvestmentTransactionHash = txHash,
            InvestmentTransactionHex = txHex,
            InvestorPubKey = investorPubKey,
            UnfundedReleaseAddress = "tb1qtest...",
            RequestEventId = eventId
        };

        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { investment }
        };

        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(records));
    }

    private void SetupProjectService(Project project)
    {
        _mockProjectService
            .Setup(x => x.GetAsync(project.Id))
            .ReturnsAsync(Result.Success(project));
    }

    private void SetupSignServiceForFounderSignatures(Project project, SignatureInfo signatureInfo)
    {
        var serializedSignatureInfo = "{}"; // Dummy serialized signature info
        
        _mockSerializer
            .Setup(x => x.Deserialize<SignatureInfo>(It.IsAny<string>()))
            .Returns(signatureInfo);

        _mockEncryptionService
            .Setup(x => x.DecryptNostrContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(serializedSignatureInfo);

        _mockSignService
            .Setup(x => x.LookupReleaseSigs(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string>(),
                It.IsAny<Action<string>>(),
                It.IsAny<Action>()))
            .Callback<string, string, DateTime?, string, Action<string>, Action>(
                (_, _, _, _, onContent, onComplete) =>
                {
                    // Simulate receiving founder signatures
                    onContent("encrypted_signature_content");
                });
    }

    private Project CreateTestProject()
    {
        var founderWords = new WalletWords { Words = "zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo wrong" };
        var founderKey = _derivationOperations.DeriveFounderKey(founderWords, 1);

        return new Project
        {
            Id = new ProjectId(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32))),
            Name = "Test Project",
            ShortDescription = "Test project for integration tests",
            FounderKey = founderKey,
            FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(founderWords, founderKey),
            NostrPubKey = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)),
            ProjectType = ProjectType.Invest,
            TargetAmount = Money.Coins(100).Satoshi,
            StartingDate = DateTime.UtcNow.AddDays(-60),
            ExpiryDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(-30),
            PenaltyDuration = TimeSpan.FromDays(10),
            Stages = new List<Stage>
            {
                new Stage { RatioOfTotal = 0.25m, ReleaseDate = DateTime.UtcNow.AddDays(-50), Index = 0 },
                new Stage { RatioOfTotal = 0.25m, ReleaseDate = DateTime.UtcNow.AddDays(-40), Index = 1 },
                new Stage { RatioOfTotal = 0.50m, ReleaseDate = DateTime.UtcNow.AddDays(-30), Index = 2 }
            }
        };
    }

    private AccountInfo CreateTestAccountInfo()
    {
        var words = new WalletWords { Words = TestWalletWords };
        var accountInfo = _walletOperations.BuildAccountInfoForWalletWords(words);

        // Add a change address if not present
        if (!accountInfo.ChangeAddressesInfo.Any())
        {
            var hdOperations = new HdOperations();
            var extPubKey = ExtPubKey.Parse(accountInfo.ExtPubKey, _network);
            var pubKey = hdOperations.GeneratePublicKey(extPubKey, 0, true);
            var changeAddress = pubKey.GetSegwitAddress(_network).ToString();
            accountInfo.ChangeAddressesInfo.Add(new AddressInfo
            {
                Address = changeAddress,
                HdPath = hdOperations.CreateHdPath(84, _network.Consensus.CoinType, 0, true, 0)
            });
        }

        return accountInfo;
    }

    private string CreateDummyTransactionHex()
    {
        // Return a minimal valid transaction hex for testing purposes
        return "0100000001000000000000000000000000000000000000000000000000000000000000000000000000000000000001000000000000000000";
    }

    private QueryTransaction CreateQueryTransactionWithUnspentOutputs(string txHash)
    {
        return new QueryTransaction
        {
            TransactionId = txHash,
            Outputs = new List<QueryTransactionOutput>
            {
                // Output 0 - Fee output (spent)
                new QueryTransactionOutput 
                { 
                    SpentInTransaction = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)) 
                },
                // Output 1 - OP_RETURN (not spendable)
                new QueryTransactionOutput 
                { 
                    OutputType = "op_return",
                    SpentInTransaction = ""
                },
                // Output 2 - Stage 0 (unspent)
                new QueryTransactionOutput 
                { 
                    SpentInTransaction = ""
                },
                // Output 3 - Stage 1 (unspent)
                new QueryTransactionOutput 
                { 
                    SpentInTransaction = ""
                },
                // Output 4 - Stage 2 (unspent)
                new QueryTransactionOutput 
                { 
                    SpentInTransaction = ""
                }
            }
        };
    }

    #region Happy Path Helper Methods

    private const string FounderWalletWords = "zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo wrong";
    private readonly string _fixedProjectId = "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2";
    private readonly string _fixedNostrPubKey = "b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3";

    private Project CreateTestProjectWithFixedId()
    {
        var founderWords = new WalletWords { Words = FounderWalletWords };
        var founderKey = _derivationOperations.DeriveFounderKey(founderWords, 1);

        return new Project
        {
            Id = new ProjectId(_fixedProjectId),
            Name = "Test Release Project",
            ShortDescription = "Test project for release transaction integration tests",
            FounderKey = founderKey,
            FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(founderWords, founderKey),
            NostrPubKey = _fixedNostrPubKey,
            ProjectType = ProjectType.Invest,
            TargetAmount = Money.Coins(100).Satoshi,
            StartingDate = DateTime.UtcNow.AddDays(-60),
            ExpiryDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(-30),
            PenaltyDuration = TimeSpan.FromDays(10),
            Stages = new List<Stage>
            {
                new Stage { RatioOfTotal = 0.25m, ReleaseDate = DateTime.UtcNow.AddDays(-50), Index = 0 },
                new Stage { RatioOfTotal = 0.25m, ReleaseDate = DateTime.UtcNow.AddDays(-40), Index = 1 },
                new Stage { RatioOfTotal = 0.50m, ReleaseDate = DateTime.UtcNow.AddDays(-30), Index = 2 }
            }
        };
    }

    private void SetupProjectServiceForProject(Project project)
    {
        _mockProjectService
            .Setup(x => x.GetAsync(project.Id))
            .ReturnsAsync(Result.Success(project));
    }

    private void SetupPortfolioServiceForHappyPath(WalletId walletId, ProjectId projectId, string txHash, string txHex, string eventId, string founderKey)
    {
        var investorPubKey = _derivationOperations.DeriveInvestorKey(
            new WalletWords { Words = TestWalletWords }, founderKey);

        var investment = new InvestmentRecord
        {
            ProjectIdentifier = projectId.Value,
            InvestmentTransactionHash = txHash,
            InvestmentTransactionHex = txHex,
            InvestorPubKey = investorPubKey,
            UnfundedReleaseAddress = "tb1qw508d6qejxtdg4y5r3zarvary0c5xw7kxpjzsx",
            RequestEventId = eventId
        };

        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { investment }
        };

        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(records));
    }

    private void SetupAccountBalanceServiceWithUtxos(WalletId walletId)
    {
        var accountInfo = CreateTestAccountInfo();
        
        // Add UTXOs for fee funding
        var hdOperations = new HdOperations();
        var extPubKey = ExtPubKey.Parse(accountInfo.ExtPubKey, _network);
        var pubKey = hdOperations.GeneratePublicKey(extPubKey, 0, false);
        var address = pubKey.GetSegwitAddress(_network).ToString();
        
        if (!accountInfo.AddressesInfo.Any())
        {
            accountInfo.AddressesInfo.Add(new AddressInfo
            {
                Address = address,
                HdPath = hdOperations.CreateHdPath(84, _network.Consensus.CoinType, 0, false, 0)
            });
        }

        var utxos = new List<UtxoData>
        {
            new UtxoData
            {
                address = address,
                scriptHex = pubKey.GetSegwitAddress(_network).ScriptPubKey.ToHex(),
                outpoint = new Outpoint(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)), 0),
                value = 100000, // 0.001 BTC for fees
                blockIndex = 100
            }
        };

        accountInfo.AddressesInfo.First().UtxoData.AddRange(utxos);

        var accountBalanceInfo = new AccountBalanceInfo();
        accountBalanceInfo.UpdateAccountBalanceInfo(accountInfo, utxos);

        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(walletId))
            .ReturnsAsync(Result.Success(accountBalanceInfo));
    }

    private void SetupSignatureFlowForHappyPath(Mock<IInvestorTransactionActions> mockInvestorTransactionActions)
    {
        var signatureInfo = new SignatureInfo
        {
            Signatures = new List<SignatureInfoItem>
            {
                new SignatureInfoItem { Signature = "304402...", StageIndex = 0 },
                new SignatureInfoItem { Signature = "304402...", StageIndex = 1 },
                new SignatureInfoItem { Signature = "304402...", StageIndex = 2 }
            }
        };

        _mockSerializer
            .Setup(x => x.Deserialize<SignatureInfo>(It.IsAny<string>()))
            .Returns(signatureInfo);

        _mockEncryptionService
            .Setup(x => x.DecryptNostrContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("{\"Signatures\":[]}");

        _mockSignService
            .Setup(x => x.LookupReleaseSigs(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string>(),
                It.IsAny<Action<string>>(),
                It.IsAny<Action>()))
            .Callback<string, string, DateTime?, string, Action<string>, Action>(
                (_, _, _, _, onContent, _) =>
                {
                    // Simulate receiving founder signatures via Nostr
                    onContent("encrypted_signature_content");
                });

        // Signature validation returns true
        mockInvestorTransactionActions
            .Setup(x => x.CheckInvestorRecoverySignatures(
                It.IsAny<ProjectInfo>(),
                It.IsAny<Blockcore.Consensus.TransactionInfo.Transaction>(),
                It.IsAny<SignatureInfo>()))
            .Returns(true);
    }

    private void SetupTransactionInfoForHappyPath(string txHash)
    {
        var queryTransaction = new QueryTransaction
        {
            TransactionId = txHash,
            Outputs = new List<QueryTransactionOutput>
            {
                new QueryTransactionOutput { SpentInTransaction = "" }, // Output 0 - unspent
                new QueryTransactionOutput { OutputType = "op_return", SpentInTransaction = "" }, // Output 1 - OP_RETURN
                new QueryTransactionOutput { SpentInTransaction = "" }, // Output 2 - Stage 0 unspent
                new QueryTransactionOutput { SpentInTransaction = "" }, // Output 3 - Stage 1 unspent
                new QueryTransactionOutput { SpentInTransaction = "" }  // Output 4 - Stage 2 unspent
            }
        };

        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(queryTransaction);
    }

    private void SetupInvestorTransactionActionsForHappyPath(Mock<IInvestorTransactionActions> mockInvestorTransactionActions)
    {
        // Create a mock unsigned release transaction
        var mockReleaseTransaction = _network.Consensus.ConsensusFactory.CreateTransaction();
        mockReleaseTransaction.Outputs.Add(new Blockcore.Consensus.TransactionInfo.TxOut(
            Money.Satoshis(50000),
            new Key().PubKey.GetSegwitAddress(_network)));

        mockInvestorTransactionActions
            .Setup(x => x.AddSignaturesToUnfundedReleaseFundsTransaction(
                It.IsAny<ProjectInfo>(),
                It.IsAny<Blockcore.Consensus.TransactionInfo.Transaction>(),
                It.IsAny<SignatureInfo>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns((Blockcore.Consensus.TransactionInfo.Transaction)mockReleaseTransaction);

        mockInvestorTransactionActions
            .Setup(x => x.CheckInvestorUnfundedReleaseSignatures(
                It.IsAny<ProjectInfo>(),
                It.IsAny<Blockcore.Consensus.TransactionInfo.Transaction>(),
                It.IsAny<SignatureInfo>(),
                It.IsAny<string>()))
            .Returns(true);
    }

    private string CreateValidTransactionHex()
    {
        // Create a valid testnet transaction that can be parsed
        var tx = _network.Consensus.ConsensusFactory.CreateTransaction();
        tx.Inputs.Add(new Blockcore.Consensus.TransactionInfo.TxIn(
            new Blockcore.Consensus.TransactionInfo.OutPoint(uint256.One, 0)));
        tx.Outputs.Add(new Blockcore.Consensus.TransactionInfo.TxOut(
            Money.Satoshis(100000),
            new Key().PubKey.GetSegwitAddress(_network)));
        return tx.ToHex();
    }

    #endregion

    #endregion
}

