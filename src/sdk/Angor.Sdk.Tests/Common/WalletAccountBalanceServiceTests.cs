using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Angor.Data.Documents.Interfaces;
using Xunit;

namespace Angor.Sdk.Tests.Common;

public class WalletAccountBalanceServiceTests
{
    private readonly Mock<IWalletOperations> _walletOperations = new();
    private readonly Mock<IGenericDocumentCollection<WalletAccountBalanceInfo>> _collection = new();

    private WalletAccountBalanceService CreateSut() =>
        new(_walletOperations.Object, _collection.Object,
            NullLogger<WalletAccountBalanceService>.Instance);

    [Fact]
    public async Task RefreshAccountBalanceInfoAsync_removes_stale_pending_receive_utxos()
    {
        var walletId = new WalletId("wallet-1");
        var accountInfo = new AccountInfo
        {
            walletId = walletId.Value,
            ExtPubKey = "ext",
            RootExtPubKey = "root",
            Path = "m/84'/1'/0'"
        };

        accountInfo.AddressesInfo.Add(new AddressInfo { Address = "tb1qtest" });

        var stalePending = new UtxoData
        {
            address = "tb1qtest",
            scriptHex = "0014",
            outpoint = new Outpoint("stale-tx", 0),
            value = 50_000,
            blockIndex = 0
        };

        var balanceInfo = new AccountBalanceInfo();
        balanceInfo.UpdateAccountBalanceInfo(accountInfo, [stalePending]);

        _walletOperations
            .Setup(x => x.UpdateDataForExistingAddressesAsync(accountInfo))
            .Returns(Task.CompletedTask);
        _walletOperations
            .Setup(x => x.UpdateAccountInfoWithNewAddressesAsync(accountInfo))
            .Returns(Task.CompletedTask);

        _collection
            .Setup(x => x.FindByIdAsync(walletId.Value))
            .ReturnsAsync(Result.Success<WalletAccountBalanceInfo?>(new WalletAccountBalanceInfo
            {
                WalletId = walletId.Value,
                AccountBalanceInfo = balanceInfo
            }));
        _collection
            .Setup(x => x.UpsertAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<WalletAccountBalanceInfo, string>>>(), It.IsAny<WalletAccountBalanceInfo>()))
            .ReturnsAsync(Result.Success(true));

        var sut = CreateSut();

        var result = await sut.RefreshAccountBalanceInfoAsync(walletId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.AccountPendingReceive);
        Assert.Equal(0, result.Value.TotalUnconfirmedBalance);
    }
}
