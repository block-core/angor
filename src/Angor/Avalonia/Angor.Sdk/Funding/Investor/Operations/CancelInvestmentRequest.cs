using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol.Scripts;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class CancelInvestmentRequest
{
    public record CancelInvestmentRequestRequest(WalletId WalletId, ProjectId ProjectId, string InvestmentId) : IRequest<Result<CancelInvestmentRequestResponse>>;

    public record CancelInvestmentRequestResponse();

    public class CancelInvestmentRequestHandler(
        IPortfolioService portfolioService,
        INetworkConfiguration networkConfiguration,
        IWalletAccountBalanceService walletAccountBalanceService,
        IMediator mediator) : IRequestHandler<CancelInvestmentRequestRequest, Result<CancelInvestmentRequestResponse>>
    {
        public async Task<Result<CancelInvestmentRequestResponse>> Handle(CancelInvestmentRequestRequest request, CancellationToken cancellationToken)
        {
            var investmentRecords = await portfolioService.GetByWalletId(request.WalletId.Value);

            var record = investmentRecords.Value.ProjectIdentifiers
                .FirstOrDefault(r => r.ProjectIdentifier == request.ProjectId.Value && r.InvestmentTransactionHash == request.InvestmentId);

            if (record == null)
                return Result.Failure<CancelInvestmentRequestResponse>("Investment record not found.");

            // Release reserved UTXOs before removing the investment record
            if (!string.IsNullOrEmpty(record.InvestmentTransactionHex))
            {
                await ReleaseReservedUtxos(request.WalletId, record.InvestmentTransactionHex);
            }

            // Notify founder of cancellation if there was a signature request
            if (!string.IsNullOrEmpty(record.RequestEventId))
            {
                await mediator.Send(new NotifyFounderOfCancellation.NotifyFounderOfCancellationRequest(
                    request.WalletId,
                    request.ProjectId,
                    record.RequestEventId), cancellationToken);
            }

            var res = await portfolioService.RemoveInvestmentRecordAsync(request.WalletId.Value, record);

            if (res.IsFailure)
                return Result.Failure<CancelInvestmentRequestResponse>(res.Error);

            return Result.Success(new CancelInvestmentRequestResponse());
        }

        private async Task ReleaseReservedUtxos(WalletId walletId, string signedTxHex)
        {
            var network = networkConfiguration.GetNetwork();
            var transaction = network.CreateTransaction(signedTxHex);

            var accountBalanceResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(walletId);
            if (accountBalanceResult.IsFailure)
                return;

            var accountBalanceInfo = accountBalanceResult.Value;

            foreach (var input in transaction.Inputs)
            {
                var outpointString = input.PrevOut.ToString();
                accountBalanceInfo.AccountInfo.UtxoReservedForInvestment.Remove(outpointString);
            }

            await walletAccountBalanceService.SaveAccountBalanceInfoAsync(walletId, accountBalanceInfo);
        }
    }
}