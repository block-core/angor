using FluentAssertions;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Protocol;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Protocol.TransactionBuilders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Angor.Shared.Services;
using Angor.Test.Protocol;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Angor.Test;

public class WalletOperationsTest : AngorTestData
{
    private WalletOperations _sut;

    private readonly Mock<IIndexerService> _indexerService;
    private readonly InvestorTransactionActions _investorTransactionActions;
    private readonly FounderTransactionActions _founderTransactionActions;
    private readonly IHdOperations _hdOperations;

    public WalletOperationsTest()
    {
        _indexerService = new Mock<IIndexerService>();

        _sut = new WalletOperations(_indexerService.Object, new HdOperations(), new NullLogger<WalletOperations>(), _networkConfiguration.Object);

        _investorTransactionActions = new InvestorTransactionActions(new NullLogger<InvestorTransactionActions>(),
            new InvestmentScriptBuilder(new SeederScriptTreeBuilder()),
            new ProjectScriptsBuilder(_derivationOperations),
            new SpendingTransactionBuilder(_networkConfiguration.Object, new ProjectScriptsBuilder(_derivationOperations), new InvestmentScriptBuilder(new SeederScriptTreeBuilder())),
            new InvestmentTransactionBuilder(_networkConfiguration.Object, new ProjectScriptsBuilder(_derivationOperations), new InvestmentScriptBuilder(new SeederScriptTreeBuilder()), new TaprootScriptBuilder()),
            new TaprootScriptBuilder(),
            _networkConfiguration.Object);

        _founderTransactionActions = new FounderTransactionActions(new NullLogger<FounderTransactionActions>(), _networkConfiguration.Object, new ProjectScriptsBuilder(_derivationOperations),
            new InvestmentScriptBuilder(new SeederScriptTreeBuilder()), new TaprootScriptBuilder());
    }


    private void AddCoins(AccountInfo accountInfo, int utxos, long amount)
    {
        var network = _networkConfiguration.Object.GetNetwork();

        int callCount = 0;
        _indexerService.Setup(_ => _.GetAdressBalancesAsync(It.IsAny<List<AddressInfo>>(), It.IsAny<bool>())).Returns((List<AddressInfo> info, bool conf) =>
        {
            if (callCount == 1)
                return Task.FromResult(Enumerable.Empty<AddressBalance>().ToArray());

            var res = info.Select(s => new AddressBalance { address = s.Address, balance = Money.Satoshis(amount).Satoshi }).ToArray();

            callCount++;
            return Task.FromResult(res);
        });

        int outputIndex = 0;
        _indexerService.Setup(_ => _.FetchUtxoAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>())).Returns<string, int, int>((address, limit, offset) =>
        {
            var res = new List<UtxoData>
            {
                new ()
                {
                    address =address,
                    value = Money.Satoshis(amount).Satoshi,
                    outpoint = new Outpoint( uint256.Zero.ToString(),outputIndex++ ),
                    scriptHex = BitcoinAddress.Create(address, network.BitcoinNetwork).ScriptPubKey.ToHex()
                }
            };

            return Task.FromResult(res);
        });

        _sut.UpdateDataForExistingAddressesAsync(accountInfo).Wait();

        _sut.UpdateAccountInfoWithNewAddressesAsync(accountInfo).Wait();
    }

    private string GenerateScriptHex(string address, AngorNetwork network)
    {
        try
        {
            var segwitAddress = BitcoinAddress.Create(address, network.BitcoinNetwork);
            return segwitAddress.ScriptPubKey.ToHex();
        }
        catch (FormatException ex)
        {
            Console.WriteLine($"Error: Invalid address format. Details: {ex.Message}");
            throw; 
        }
    }

    [Fact]
    public void AddFeeAndSignTransaction_test()
    {
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };

        AccountInfo accountInfo = _sut.BuildAccountInfoForWalletWords(words);

        AddCoins(accountInfo, 6, 500000000);

        var network = _networkConfiguration.Object.GetNetwork();

        var changeAddress = new Key().PubKey.GetAddress(ScriptPubKeyType.Segwit, network.BitcoinNetwork).ToString();

        // Generate test data dynamically (previously used hardcoded Blockcore-format hex)
        var projectInfo = new ProjectInfo();
        projectInfo.TargetAmount = Money.Coins(3).Satoshi;
        projectInfo.StartDate = DateTime.UtcNow;
        projectInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
        projectInfo.PenaltyDays = 10;
        projectInfo.Stages = new List<Stage>
        {
            new Stage { AmountToRelease = 25, ReleaseDate = DateTime.UtcNow.AddDays(1) },
            new Stage { AmountToRelease = 25, ReleaseDate = DateTime.UtcNow.AddDays(2) },
            new Stage { AmountToRelease = 50, ReleaseDate = DateTime.UtcNow.AddDays(3) }
        };
        projectInfo.FounderKey = _derivationOperations.DeriveFounderKey(words, 1);
        projectInfo.FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, projectInfo.FounderKey);
        projectInfo.ProjectIdentifier = _derivationOperations.DeriveAngorKey(angorRootKey, projectInfo.FounderKey);

        var founderRecoveryPrivateKey = _derivationOperations.DeriveFounderRecoveryPrivateKey(words, projectInfo.FounderKey);
        var investorKey = _derivationOperations.DeriveInvestorKey(words, projectInfo.FounderKey);
        var investorPrivateKey = _derivationOperations.DeriveInvestorPrivateKey(words, projectInfo.FounderKey);

        var investmentTransaction = _investorTransactionActions.CreateInvestmentTransaction(projectInfo, investorKey, Money.Coins(10).Satoshi);
        var signedInvestmentTransaction = _sut.AddInputsAndSignTransaction(accountInfo.GetNextReceiveAddress(), investmentTransaction, words, accountInfo, 3000);

        var strippedInvestmentTransaction = network.CreateTransaction(signedInvestmentTransaction.Transaction.ToHex());
        strippedInvestmentTransaction.Inputs.ForEach(f => f.WitScript = WitScript.Empty);

        var unsignedRecoveryTransaction = _investorTransactionActions.BuildRecoverInvestorFundsTransaction(projectInfo, strippedInvestmentTransaction);
        var recoverySigs = _founderTransactionActions.SignInvestorRecoveryTransactions(projectInfo, strippedInvestmentTransaction.ToHex(), unsignedRecoveryTransaction, founderRecoveryPrivateKey);
        var recoveryTransaction = _investorTransactionActions.AddSignaturesToRecoverSeederFundsTransaction(projectInfo, signedInvestmentTransaction.Transaction, recoverySigs, investorPrivateKey);

        // remove the first stage to simulate that is was spent
        recoveryTransaction.Outputs.RemoveAt(0);
        recoveryTransaction.Inputs.RemoveAt(0);

        var recoveryTransactions = _sut.AddFeeAndSignTransaction(changeAddress, recoveryTransaction, words, accountInfo, 3000);

        // add the inputs of the investment trx
        List<Coin> coins = new();
        foreach (var output in investmentTransaction.Outputs.AsIndexedOutputs().Skip(2))
        {
            coins.Add(new Coin(new OutPoint(signedInvestmentTransaction.Transaction.GetHash(), output.N), output.TxOut));
        }

        //add all utxos as coins (easier)
        foreach (var utxo in accountInfo.AddressesInfo.Concat(accountInfo.ChangeAddressesInfo).SelectMany(s => s.UtxoData))
        {
            coins.Add(new Coin(uint256.Parse(utxo.outpoint.transactionId), (uint)utxo.outpoint.outputIndex,
                new Money(utxo.value), Script.FromHex(utxo.scriptHex))); //Adding fee inputs
        }

        TransactionValidation.ThanTheTransactionHasNoErrors(recoveryTransactions.Transaction, coins);
    }

    [Fact]
    public void AddInputsAndSignTransaction()
    {
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };

        AccountInfo accountInfo = _sut.BuildAccountInfoForWalletWords(words);

        AddCoins(accountInfo, 6, 500000000);

        var network = _networkConfiguration.Object.GetNetwork();

        var projectInfo = new ProjectInfo();
        projectInfo.TargetAmount = Money.Coins(3).Satoshi;
        projectInfo.StartDate = DateTime.UtcNow;
        projectInfo.ExpiryDate = DateTime.UtcNow.AddDays(5);
        projectInfo.PenaltyDays = 10;
        projectInfo.Stages = new List<Stage>
        {
            new Stage { AmountToRelease = 25, ReleaseDate = DateTime.UtcNow.AddDays(1) },
            new Stage { AmountToRelease = 25, ReleaseDate = DateTime.UtcNow.AddDays(2) },
            new Stage { AmountToRelease = 50, ReleaseDate = DateTime.UtcNow.AddDays(3) }
        };
        projectInfo.FounderKey = _derivationOperations.DeriveFounderKey(words, 1);
        projectInfo.FounderRecoveryKey = _derivationOperations.DeriveFounderRecoveryKey(words, projectInfo.FounderKey);
        projectInfo.ProjectIdentifier = _derivationOperations.DeriveAngorKey(angorRootKey, projectInfo.FounderKey);

        var founderRecoveryPrivateKey = _derivationOperations.DeriveFounderRecoveryPrivateKey(words, projectInfo.FounderKey);

        var investmentAmount = 10;
        var investorKey = _derivationOperations.DeriveInvestorKey(words, projectInfo.FounderKey);
        var investorPrivateKey = _derivationOperations.DeriveInvestorPrivateKey(words, projectInfo.FounderKey);

        var investmentTransaction = _investorTransactionActions.CreateInvestmentTransaction(projectInfo, investorKey, Money.Coins(investmentAmount).Satoshi);

        Assert.Equal(5, investmentTransaction.Outputs.Count); // 1 for angor fee, 1 for op return, 3 for stages
        Assert.Equal(247500000, investmentTransaction.Outputs[2].Value);
        Assert.Equal(247500000, investmentTransaction.Outputs[3].Value);
        Assert.Equal(495000000, investmentTransaction.Outputs[4].Value);

        var signedInvestmentTransaction = _sut.AddInputsAndSignTransaction(accountInfo.GetNextReceiveAddress(), investmentTransaction, words, accountInfo, 3000);

        var strippedInvestmentTransaction = network.CreateTransaction(signedInvestmentTransaction.Transaction.ToHex());
        strippedInvestmentTransaction.Inputs.ForEach(f => f.WitScript = WitScript.Empty);
        Assert.Equal(signedInvestmentTransaction.Transaction.GetHash(), strippedInvestmentTransaction.GetHash());

        var unsignedRecoveryTransaction = _investorTransactionActions.BuildRecoverInvestorFundsTransaction(projectInfo, strippedInvestmentTransaction);
        var recoverySigs = _founderTransactionActions.SignInvestorRecoveryTransactions(projectInfo, strippedInvestmentTransaction.ToHex(), unsignedRecoveryTransaction, founderRecoveryPrivateKey);

        var sigCheckResult  = _investorTransactionActions.CheckInvestorRecoverySignatures(projectInfo, signedInvestmentTransaction.Transaction, recoverySigs);
        Assert.True(sigCheckResult, "failed to validate the founders signatures");

        var recoveryTransaction = _investorTransactionActions.AddSignaturesToRecoverSeederFundsTransaction(projectInfo, signedInvestmentTransaction.Transaction, recoverySigs, investorPrivateKey);

        // remove the first stage to simulate that is was spent
        recoveryTransaction.Outputs.RemoveAt(0);
        recoveryTransaction.Inputs.RemoveAt(0);

        var signedRecoveryTransaction = _sut.AddFeeAndSignTransaction(accountInfo.GetNextReceiveAddress(), recoveryTransaction, words, accountInfo, 3000);

        // add the inputs of the investment trx
        List<Coin> coins = new();
        foreach (var output in investmentTransaction.Outputs.AsIndexedOutputs().Skip(2))
        {
            coins.Add(new Coin(new OutPoint(signedInvestmentTransaction.Transaction.GetHash(), output.N), output.TxOut));
        }

        //add all utxos as coins (easier)
        foreach (var utxo in accountInfo.AddressesInfo.Concat(accountInfo.ChangeAddressesInfo).SelectMany(s => s.UtxoData))
        {
            coins.Add(new Coin(uint256.Parse(utxo.outpoint.transactionId), (uint)utxo.outpoint.outputIndex,
                new Money(utxo.value), Script.FromHex(utxo.scriptHex))); //Adding fee inputs
        }

        TransactionValidation.ThanTheTransactionHasNoErrors(signedRecoveryTransaction.Transaction, coins);
    }

    [Fact]
    public void GenerateWalletWords_ReturnsCorrectFormat()
    {
        // Arrange
        var walletOps = new WalletOperations(_indexerService.Object, _hdOperations, NullLogger<WalletOperations>.Instance, _networkConfiguration.Object);

        // Act
        var result = walletOps.GenerateWalletWords();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(12, result.Split(' ').Length); // Assuming a 12-word mnemonic
    }

    [Fact]
    public async Task transaction_fails_due_to_insufficient_funds() // funds are null
    {
        // Arrange
        var mockNetworkConfiguration = new Mock<INetworkConfiguration>();
        var mockIndexerService = new Mock<IIndexerService>();
        var mockHdOperations = new Mock<IHdOperations>();
        var mockLogger = new Mock<ILogger<WalletOperations>>();
        var network = _networkConfiguration.Object.GetNetwork();
        mockNetworkConfiguration.Setup(x => x.GetNetwork()).Returns(network);
        mockIndexerService.Setup(x => x.PublishTransactionAsync(It.IsAny<string>())).ReturnsAsync(string.Empty);

        var walletOperations = new WalletOperations(mockIndexerService.Object, mockHdOperations.Object, mockLogger.Object, mockNetworkConfiguration.Object);

        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };
        string address = "tb1qeu7wvxjg7ft4fzngsdxmv0pphdux2uthq4z679";
        AccountInfo accountInfo = _sut.BuildAccountInfoForWalletWords(words);
        string scriptHex = GenerateScriptHex(address, network);
        var sendInfo = new SendInfo
        {
            SendAmount = Money.Coins(0.01m).Satoshi,
            SendUtxos = new Dictionary<string, UtxoDataWithPath>
        {
            {
                "key", new UtxoDataWithPath
                {
                    UtxoData = new UtxoData
                    {
                        value = 500,  //  insufficient to cover the send amount and fees
                        address = address,
                        scriptHex = scriptHex,
                        outpoint = new Outpoint(), // Ensure Outpoint is also correctly initialized
                        blockIndex = 1,
                        PendingSpent = false
                    },
                    HdPath = "your_hd_path_here"
                }
            }
        },
            FeeRate = Money.Coins(3000).Satoshi,
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ApplicationException>(() => walletOperations.SendAmountToAddress(words, sendInfo));
        Assert.Contains("not enough funds", exception.Message);
    }




    [Fact]
    public async Task TransactionSucceeds_WithSufficientFundsWallet()
    {
        // Arrange
        var mockNetworkConfiguration = new Mock<INetworkConfiguration>();
        var mockIndexerService = new Mock<IIndexerService>();
        var mockHdOperations = new Mock<IHdOperations>();
        var mockLogger = new Mock<ILogger<WalletOperations>>();
        var network = _networkConfiguration.Object.GetNetwork();
        mockNetworkConfiguration.Setup(x => x.GetNetwork()).Returns(network);
        mockHdOperations.Setup(x => x.GetAccountHdPath(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                        .Returns("m/0/0");
        var expectedExtendedKey = new ExtKey();
        mockHdOperations.Setup(x => x.GetExtendedKey(It.IsAny<string>(), It.IsAny<string>())).Returns(expectedExtendedKey);

        var walletOperations = new WalletOperations(mockIndexerService.Object, mockHdOperations.Object, mockLogger.Object, mockNetworkConfiguration.Object);

        var words = new WalletWords { Words = "suspect lesson reduce catalog melt lucky decade harvest plastic output hello panel", Passphrase = "" };
        string address = "tb1qeu7wvxjg7ft4fzngsdxmv0pphdux2uthq4z679";
        string scriptHex = GenerateScriptHex(address, network);
        var sendInfo = new SendInfo
        {
            SendToAddress = "tb1qw4vvm955kq5vrnx48m3x6kq8rlpgcauzzx63sr",
            ChangeAddress = "tb1qw4vvm955kq5vrnx48m3x6kq8rlpgcauzzx63sr",
            SendAmount = Money.Coins(0.01m).Satoshi,
            FeeRate = Money.Satoshis(3000).Satoshi,
            SendUtxos = new Dictionary<string, UtxoDataWithPath>
            {
                {
                    "key", new UtxoDataWithPath
                    {
                        UtxoData = new UtxoData
                        {
                            value = 1500000, // Sufficient to cover the send amount and estimated fees
                            address = address,
                            scriptHex = scriptHex,
                            outpoint = new Outpoint("0000000000000000000000000000000000000000000000000000000000000000", 0),
                            blockIndex = 1,
                            PendingSpent = false
                        },
                        HdPath = "m/0/0"
                    }
                }
            },
        };

        // Act
        var operationResult = await walletOperations.SendAmountToAddress(words, sendInfo);

        // Assert
        Assert.True(operationResult.Success);
        Assert.NotNull(operationResult.Data); // ensure data is saved
    }

    [Fact]
    public async Task SendAllToAddress_subtracts_fee_and_creates_no_change_output()
    {
        // Arrange
        var network = _networkConfiguration.Object.GetNetwork();
        var hdOperations = new HdOperations();
        var indexer = new Mock<IIndexerService>();
        indexer.Setup(x => x.PublishTransactionAsync(It.IsAny<string>())).ReturnsAsync(string.Empty);
        var walletOperations = new WalletOperations(indexer.Object, hdOperations,
            new NullLogger<WalletOperations>(), _networkConfiguration.Object);

        var words = new WalletWords
        {
            Words = "suspect lesson reduce catalog melt lucky decade harvest plastic output hello panel",
            Passphrase = ""
        };
        ExtKey accountKey = hdOperations.GetExtendedKey(words.Words, words.Passphrase);
        Key inputKey = accountKey.Derive(new KeyPath("0/0")).PrivateKey;
        string inputAddress = inputKey.PubKey.WitHash.GetAddress(network.BitcoinNetwork).ToString();
        string destination = new Key().PubKey.WitHash.GetAddress(network.BitcoinNetwork).ToString();
        const long inputSats = 100_000;
        const long satsPerVByte = 20;

        var sendInfo = new SendInfo
        {
            SendToAddress = destination,
            FeeRate = satsPerVByte * 1000,
            SendUtxos = new Dictionary<string, UtxoDataWithPath>
            {
                ["coin"] = new()
                {
                    HdPath = "m/0/0",
                    UtxoData = new UtxoData
                    {
                        value = inputSats,
                        address = inputAddress,
                        scriptHex = BitcoinAddress.Create(inputAddress, network.BitcoinNetwork).ScriptPubKey.ToHex(),
                        outpoint = new Outpoint(uint256.One.ToString(), 0),
                        blockIndex = 1,
                    }
                }
            }
        };

        // Act
        OperationResult<Transaction> result = await walletOperations.SendAllToAddress(words, sendInfo);

        // Assert
        Assert.True(result.Success, result.Message);
        Assert.Single(result.Data.Outputs);
        long outputSats = result.Data.Outputs[0].Value.Satoshi;
        long feeSats = inputSats - outputSats;
        Assert.True(feeSats > 0);
        Assert.InRange(feeSats, result.Data.GetVirtualSize() * satsPerVByte - satsPerVByte,
            result.Data.GetVirtualSize() * satsPerVByte + satsPerVByte);
        Assert.Equal(BitcoinAddress.Create(destination, network.BitcoinNetwork).ScriptPubKey,
            result.Data.Outputs[0].ScriptPubKey);
    }


    [Fact]
    public void GetUnspentOutputsForTransaction_ReturnsCorrectOutputs()
    {
        // Arrange
        var mockHdOperations = new Mock<IHdOperations>();
        var walletWords = new WalletWords { Words = "suspect lesson reduce catalog melt lucky decade harvest plastic output hello panel", Passphrase = "" };
        var utxos = new List<UtxoDataWithPath>
    {
        new UtxoDataWithPath
        {
            UtxoData = new UtxoData
            {
                value = 1500000,
                address = "tb1qeu7wvxjg7ft4fzngsdxmv0pphdux2uthq4z679",
                scriptHex = "0014b7d165bb8b25f567f05c57d3b484159582ac2827",
                outpoint = new Outpoint("0000000000000000000000000000000000000000000000000000000000000000", 0),
                blockIndex = 1,
                PendingSpent = false
            },
            HdPath = "m/0/0"
        }
    };

        var expectedExtKey = new ExtKey();
        mockHdOperations.Setup(x => x.GetExtendedKey(walletWords.Words, walletWords.Passphrase)).Returns(expectedExtKey);

        var walletOperations = new WalletOperations(null, mockHdOperations.Object, null, null);

        // Act
        var signingCoins = walletOperations.GetUnspentOutputsForTransaction(walletWords, utxos);

        // Assert
        Assert.Single(signingCoins);
        Assert.Equal((uint)0, signingCoins[0].Coin.Outpoint.N);
        Assert.Equal(1500000, signingCoins[0].Coin.Amount.Satoshi);
        Assert.Equal(expectedExtKey.Derive(new KeyPath("m/0/0")).PrivateKey, signingCoins[0].Key);
    }

    private (WalletWords words, AccountInfo account) SingleUtxoWallet(long amount)
    {
        var words = new WalletWords { Words = "sorry poet adapt sister barely loud praise spray option oxygen hero surround" };
        AccountInfo accountInfo = _sut.BuildAccountInfoForWalletWords(words);
        AddCoins(accountInfo, 1, amount); // populates addresses; then keep exactly one UTXO
        foreach (var a in accountInfo.AllAddresses())
            a.UtxoData.Clear();
        var network = _networkConfiguration.Object.GetNetwork();
        var addressInfo = accountInfo.AddressesInfo.First();
        var addr = addressInfo.Address;
        addressInfo.UtxoData.Add(new UtxoData
        {
            address = addr,
            value = amount,
            outpoint = new Outpoint(uint256.One.ToString(), 0),
            scriptHex = BitcoinAddress.Create(addr, network.BitcoinNetwork).ScriptPubKey.ToHex()
        });
        return (words, accountInfo);
    }

    private Transaction RequestedOutput(AccountInfo accountInfo, long sats)
    {
        var network = _networkConfiguration.Object.GetNetwork();
        var tx = network.CreateTransaction();
        tx.Outputs.Add(new TxOut(Money.Satoshis(sats), BitcoinAddress.Create(accountInfo.AddressesInfo.Last().Address, network.BitcoinNetwork).ScriptPubKey));
        return tx;
    }

    [Fact]
    public void AddInputsAndSignTransaction_WhenChangeIsDust_FoldsChangeIntoFee()
    {
        var (words, account) = SingleUtxoWallet(10241);
        var requested = RequestedOutput(account, 10001);

        // ~1700 sat/kvB gives an estimated fee of roughly 240 sats for a 1-in/2-out tx
        var result = _sut.AddInputsAndSignTransaction(account.AddressesInfo.First().Address, requested, words, account, 1700);

        result.Transaction.Outputs.Should().NotContain(o => o.Value.Satoshi > 0 && o.Value.Satoshi < 546);
        result.Transaction.Outputs.Should().HaveCount(1);
        result.Transaction.Outputs[0].Value.Satoshi.Should().Be(10001);
        result.TransactionFee.Should().Be(10241 - 10001);
    }

    [Fact]
    public void AddInputsAndSignTransaction_WhenChangeIsNormal_KeepsChangeOutput()
    {
        var (words, account) = SingleUtxoWallet(50000);
        var requested = RequestedOutput(account, 10001);

        var result = _sut.AddInputsAndSignTransaction(account.AddressesInfo.First().Address, requested, words, account, 1700);

        result.Transaction.Outputs.Should().HaveCount(2);
        result.Transaction.Outputs[0].Value.Satoshi.Should().Be(10001);
        result.Transaction.Outputs[1].Value.Satoshi.Should().BeGreaterThan(546);
        result.TransactionFee.Should().Be(50000 - result.Transaction.Outputs.Sum(o => o.Value.Satoshi));
        result.TransactionFee.Should().BeInRange(100, 400);
    }

    [Fact]
    public void AddInputsAndSignTransaction_WhenSpendAllLeavesSubDustRemainder_Throws()
    {
        var (words, account) = SingleUtxoWallet(10000);
        var requested = RequestedOutput(account, 10000);

        // 1-in/1-out is ~110 vB, so 87,000 sat/kvB costs 9,570 sats and leaves a positive sub-dust remainder (430)
        var act = () => _sut.AddInputsAndSignTransaction(account.AddressesInfo.First().Address, requested, words, account, 87000);

        act.Should().Throw<ApplicationException>().WithMessage("*9570*430*10000*dust limit*");
    }
}
