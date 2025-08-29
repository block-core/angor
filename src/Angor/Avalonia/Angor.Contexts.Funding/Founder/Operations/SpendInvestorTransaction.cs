using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Founder.Dtos;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Founder.Operations;

public static class SpendInvestorTransaction
{
    public record SpendInvestorTransactionRequest(Guid WalletId, ProjectId ProjectId, FeeEstimation SelectedFee, IEnumerable<SpendTransactionDto> ToSpend) : IRequest<Result>;

    public class SpendInvestorTransactionHandler(IWalletOperations walletOperations, IFounderTransactionActions founderTransactionActions,
        INetworkConfiguration networkConfiguration, IIndexerService indexerService, IProjectRepository projectRepository,
        IDerivationOperations derivationOperations, ISeedwordsProvider seedwordsProvider) : IRequestHandler<SpendInvestorTransactionRequest, Result>
    {
        public async Task<Result> Handle(SpendInvestorTransactionRequest request, CancellationToken cancellationToken)
        {
            var groupedByStage = request.ToSpend.GroupBy(x => x.StageId).ToList();
            if (groupedByStage.Count > 1)
                return Result.Failure("You can only spend one stage at a time.");
            
            var selectedStageId = groupedByStage.First().Key;
            var network = networkConfiguration.GetNetwork();

            var project = await projectRepository.Get(request.ProjectId);
            if (project.IsFailure)
            {
                return Result.Failure(project.Error);
            }
            
            var founderKey = await GetProjectFounderKeyAsync(request.WalletId, request.ProjectId.Value);
            
            var founderContext = new FounderContext { ProjectInfo = project.Value.ToProjectInfo(), ProjectSeeders = new ProjectSeeders() };

            var tasks = request.ToSpend.Select(x =>
                    GetInvestmentByInvestorKey(request.ProjectId.Value, x, network))
                .Merge();
            
            var investmentTransactions = await tasks.ToList();
            founderContext.InvestmentTrasnactionsHex = investmentTransactions
                .Select(t => t.transactionHex)
                .ToList();
            
            var addressResult = await GetUnfundedReleaseAddress((await seedwordsProvider.GetSensitiveData(request.WalletId)).Value.ToWalletWords());
            if (addressResult.IsFailure) 
                return Result.Failure("Could not get an unfunded release address");
            
            var addressScript = BitcoinWitPubKeyAddress.Create(addressResult.Value, network).ScriptPubKey;
            
            var signedTransaction = founderTransactionActions.SpendFounderStage(founderContext.ProjectInfo,
                founderContext.InvestmentTrasnactionsHex, selectedStageId, addressScript,
                founderKey, request.SelectedFee); 
            
            var response = await walletOperations.PublishTransactionAsync(network, signedTransaction.Transaction);


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
            
            return !response.Success ? Result.Failure(response.Message) : Result.Success();
        }

        private IObservable<(string InvestorAddress, int StageId, string transactionHex)> GetInvestmentByInvestorKey(string projectId,
            SpendTransactionDto x, Network network)
        {
            return indexerService.GetInvestmentAsync(projectId, x.InvestorAddress)
                .ToObservable()
                .Select(t => t?.TransactionId)
                .Where(t => t != null)
                .SelectMany(t => indexerService.GetTransactionHexByIdAsync(t!).ToObservable())
                .Select(transaction => (x.InvestorAddress, x.StageId, transaction));
        }

        private async Task<string> GetProjectFounderKeyAsync(Guid walletId, string projectId)
        {
            var words = await seedwordsProvider.GetSensitiveData(walletId);
            var network = networkConfiguration.GetNetwork();
                            
            var founderKeys = derivationOperations.DeriveProjectKeys(words.Value.ToWalletWords(), networkConfiguration.GetAngorKey());
                            
            var keys = founderKeys.Keys.First(k => k.ProjectIdentifier == projectId);
                            
            var key = derivationOperations.DeriveFounderPrivateKey(words.Value.ToWalletWords(), keys.Index);
                            
            return Encoders.Hex.EncodeData(key.ToBytes());
        }
        
        private Task<Result<string>> GetUnfundedReleaseAddress(WalletWords wallet)
        {
            return Result.Try(async () =>
            {
                var accountInfo = walletOperations.BuildAccountInfoForWalletWords(wallet);
                await walletOperations.UpdateAccountInfoWithNewAddressesAsync(accountInfo);

                return accountInfo.GetNextReceiveAddress();
            }).EnsureNotNull("Could not get the unfunded release address");
        }
    }
}