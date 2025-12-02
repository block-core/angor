using Angor.Contexts.CrossCutting;
using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Services;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
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

namespace Angor.Contexts.Funding.Investor.Operations;

public static class CancelInvestmentSignatures
{
    public class CancelInvestmentSignaturesRequest(WalletId walletId, ProjectId projectId, string investmentId) : IRequest<Result>
    {
        public ProjectId ProjectId { get; } = projectId;
        public string InvestmentId { get; } = investmentId;
        public WalletId WalletId { get; } = walletId;
    }

    public class CancelInvestmentSignaturesHandler(
        IPortfolioService portfolioService) : IRequestHandler<CancelInvestmentSignaturesRequest, Result>
    {
        public async Task<Result> Handle(CancelInvestmentSignaturesRequest request, CancellationToken cancellationToken)
        {
            var investmentRecords = await portfolioService.GetByWalletId(request.WalletId.Value);

            var record = investmentRecords.Value.ProjectIdentifiers
                .FirstOrDefault(r => r.ProjectIdentifier == request.ProjectId.Value && r.InvestmentTransactionHash == request.InvestmentId);

            if (record == null)
                return Result.Failure<string>("Investment record not found.");

            var res = await portfolioService.RemoveInvestmentRecordAsync(request.WalletId.Value, record);

            if (res.IsFailure)
                return Result.Failure<string>(res.Error);

            return Result.Success();
        }
    }
}