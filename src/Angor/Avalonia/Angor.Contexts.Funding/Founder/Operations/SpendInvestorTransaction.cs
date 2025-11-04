using Angor.Contests.CrossCutting;
using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Founder.Domain;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Services;
using Angor.Contexts.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class SpendInvestorTransaction
{
    public record SpendInvestorTransactionRequest(Guid WalletId, ProjectId ProjectId, FeeEstimation SelectedFee, IEnumerable<SpendTransactionDto> ToSpend) : IRequest<Result<TransactionDraft>>;

    public class SpendInvestorTransactionHandler(
        IWalletOperations walletOperations,
        IFounderTransactionActions founderTransactionActions,
        INetworkConfiguration networkConfiguration,
        IIndexerService indexerService,
        IProjectService projectService,
        IDerivationOperations derivationOperations,
        ISeedwordsProvider seedwordsProvider,
        ITransactionService transactionService,
        IWalletAccountBalanceService walletAccountBalanceService,
        IGenericDocumentCollection<DerivedProjectKeys> derivedProjectKeysCollection
    ) : IRequestHandler<SpendInvestorTransactionRequest, Result<TransactionDraft>>
    {
        public async Task<Result<TransactionDraft>> Handle(SpendInvestorTransactionRequest request, CancellationToken cancellationToken)
        {
            var groupedByStage = request.ToSpend.GroupBy(x => x.StageId).ToList();
            if (groupedByStage.Count > 1)
                return Result.Failure<TransactionDraft>("You can only spend one stage at a time.");
            
            var selectedStageId = groupedByStage.First().Key;
            var network = networkConfiguration.GetNetwork();

            var project = await projectService.GetAsync(request.ProjectId);
            if (project.IsFailure)
            {
                return Result.Failure<TransactionDraft>(project.Error);
            }
            
            var founderKey = await GetProjectFounderKeyAsync(request.WalletId, request.ProjectId.Value);
            if (founderKey == null)
                return Result.Failure<TransactionDraft>("Project keys not found in storage. Please load founder projects first.");
            
            var founderContext = new FounderContext { ProjectInfo = project.Value.ToProjectInfo(), ProjectSeeders = new ProjectSeeders() };

            var tasks = request.ToSpend.Select(x => GetInvestmentByInvestorKey(request.ProjectId.Value, x));
            
            var investmentTransactions = await Task.WhenAll(tasks);
            founderContext.InvestmentTrasnactionsHex = investmentTransactions.Where(hex => hex != string.Empty).ToList();
            
            var addressResult = await GetUnfundedReleaseAddress(request.WalletId);
            if (addressResult.IsFailure) 
                return Result.Failure<TransactionDraft>("Could not get an unfunded release address");
            
            var addressScript = BitcoinAddress.Create(addressResult.Value, network).ScriptPubKey;
            
            var signedTransaction = founderTransactionActions.SpendFounderStage(founderContext.ProjectInfo,
                founderContext.InvestmentTrasnactionsHex, selectedStageId, addressScript,
                founderKey, request.SelectedFee); 
            
            //var response = await walletOperations.PublishTransactionAsync(network, signedTransaction.Transaction);

            return Result.Success(new TransactionDraft
            {
                SignedTxHex = signedTransaction.Transaction.ToHex(),
                TransactionFee = new Amount(signedTransaction.TransactionFee),
                TransactionId = signedTransaction.Transaction.GetHash().ToString()
            });

            //TODO handle the caching of pending transactions properly
            // // add all outptus to the pending list
            // var accountInfo = storage.GetAccountInfo(network.Name);
            // var unconfirmedInbound = _cacheStorage.GetUnconfirmedInboundFunds();
            // var pendingInbound = _WalletOperations.UpdateAccountUnconfirmedInfoWithSpentTransaction(accountInfo, signedTransaction.Transaction);
            // unconfirmedInbound.AddRange(pendingInbound);
            // _cacheStorage.SetUnconfirmedInboundFunds(unconfirmedInbound);
            //
            // var unconfirmedOutbound = _cacheStorage.GetUnconfirmedOutboundFunds();
            // unconfirmedOutbound.AddRange(signedTransaction.Transaction.Inputs.Select(_ => new Outpoint(_.PrevOut.Hash.ToString(), (int)_.PrevOut.N)));
            // _cacheStorage.SetUnconfirmedOutboundFunds(unconfirmedOutbound);
            //
            // // mark stage as spent
            // stageDatas.FirstOrDefault(_ => _.StageIndex == selectedStageId)?.Items.ForEach(_ =>
            // {
            //     if (signedTransaction.Transaction.Inputs.Any(a => _.Trxid == a.PrevOut.Hash.ToString() && _.Outputindex == a.PrevOut.N))
            //         _.IsSpent = true;
            // });
        }

        private async Task<string> GetInvestmentByInvestorKey(string projectId, SpendTransactionDto x)
        {
            var investment = await indexerService.GetInvestmentAsync(projectId, x.InvestorAddress);
            if (investment == null)
                return string.Empty;
            return await transactionService.GetTransactionHexByIdAsync(investment.TransactionId) ?? string.Empty;
        }

        private async Task<string?> GetProjectFounderKeyAsync(Guid walletId, string projectId)
        {
            // Try to get from storage first
            var storedKeysResult = await derivedProjectKeysCollection.FindByIdAsync(walletId.ToString());

            if (!storedKeysResult.IsSuccess || storedKeysResult.Value == null) 
                return null;
            
            var storedKey = storedKeysResult.Value.Keys.FirstOrDefault(k => k.ProjectIdentifier == projectId);
            
            if (storedKey == null) 
                return null;
            
            // Use the stored index to derive the founder private key
            var words = await seedwordsProvider.GetSensitiveData(walletId);
            var key = derivationOperations.DeriveFounderPrivateKey(words.Value.ToWalletWords(), storedKey.Index);
            return Encoders.Hex.EncodeData(key.ToBytes());
        }
        
        private async Task<Result<string>> GetUnfundedReleaseAddress(Guid walletId)
        {
            var accountBalanceResult = await walletAccountBalanceService.RefreshAccountBalanceInfoAsync(walletId);
            if (accountBalanceResult.IsFailure)
                return Result.Failure<string>(accountBalanceResult.Error);

            var accountInfo = accountBalanceResult.Value.AccountInfo;

            var nextChangeAddress = accountInfo.GetNextChangeReceiveAddress();
            
            return string.IsNullOrEmpty(nextChangeAddress) 
                ? Result.Failure<string>("Could not get the next change address") 
                : Result.Success(nextChangeAddress);
        }
    }
}