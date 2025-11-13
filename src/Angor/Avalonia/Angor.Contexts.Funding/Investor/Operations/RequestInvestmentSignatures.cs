using Angor.Contests.CrossCutting;
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

public static class RequestInvestmentSignatures
{
    public class RequestFounderSignaturesRequest(Guid walletId, ProjectId projectId, InvestmentDraft draft) : IRequest<Result<Guid>>
    {
        public ProjectId ProjectId { get; } = projectId;
        public InvestmentDraft Draft { get; } = draft;
        public Guid WalletId { get; } = walletId;
    }
    
    public class RequestFounderSignaturesHandler(
        IProjectService projectService,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        IEncryptionService encryptionService,
        INetworkConfiguration networkConfiguration,
        ISerializer serializer,
        ISignService signService,
        IPortfolioService portfolioService,
        IProjectScriptsBuilder projectScriptsBuilder,
        IAngorIndexerService angorIndexerService,
        IWalletAccountBalanceService walletAccountBalanceService) : IRequestHandler<RequestFounderSignaturesRequest, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(RequestFounderSignaturesRequest request, CancellationToken cancellationToken)
        {
            var txnHex = request.Draft.SignedTxHex;
            var network = networkConfiguration.GetNetwork();
            var strippedInvestmentTransaction = network.CreateTransaction(txnHex);
            var transactionId = strippedInvestmentTransaction.GetHash().ToString();
            strippedInvestmentTransaction.Inputs.ForEach(f => f.WitScript = WitScript.Empty);

            var projectResult = await projectService.GetAsync(request.ProjectId);

            if (projectResult.IsFailure)
            {
                return Result.Failure<Guid>(projectResult.Error);
            }

            var (investorKey,_) = projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(strippedInvestmentTransaction.Outputs[1].ScriptPubKey);
            
            var existingInvestment = await Result.Try(() => angorIndexerService.GetInvestmentAsync(request.ProjectId.Value,investorKey));

            if (existingInvestment is { IsSuccess: true, Value: not null })
                return Result.Failure<Guid>("An investment with the same key already exists on the blockchain.");
            
            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(request.WalletId);

            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<Guid>(sensitiveDataResult.Error);
            }

            var walletWords = sensitiveDataResult.Value.ToWalletWords();
            var project = projectResult.Value;

            var sendSignatureResult = await SendSignatureRequest(request.WalletId, walletWords, project, strippedInvestmentTransaction.ToHex());

            if (sendSignatureResult.IsFailure)
            {
                return Result.Failure<Guid>(sendSignatureResult.Error);
            }
            
            await portfolioService.AddOrUpdate(request.WalletId, new InvestmentRecord
            {
                InvestmentTransactionHash = transactionId,
                InvestmentTransactionHex = request.Draft.SignedTxHex,
                InvestorPubKey = request.Draft.InvestorKey,
                ProjectIdentifier = request.ProjectId.Value,
                UnfundedReleaseAddress = null, //TODO: Set this to the actual unfunded release address once implemented
                RequestEventId = sendSignatureResult.Value.eventId,
                RequestEventTime = sendSignatureResult.Value.createdTime,
            });
            
            return Result.Success(Guid.Empty);
        }

        private async Task<Result<(DateTime createdTime,string eventId)>> SendSignatureRequest(Guid walletId, WalletWords walletWords, Project project, string signedTransactionHex)
        {
            try
            {
                string nostrPubKey = project.NostrPubKey;

                var investorNostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(walletWords, project.FounderKey);
                var investorNostrPrivateKeyHex = Encoders.Hex.EncodeData(investorNostrPrivateKey.ToBytes());
                var releaseAddressResult = await GetUnfundedReleaseAddress(walletId);

                if (releaseAddressResult.IsFailure)
                {
                    return Result.Failure<(DateTime,string)>(releaseAddressResult.Error);
                }
                
                var releaseAddress = releaseAddressResult.Value;

                var signRecoveryRequest = new SignRecoveryRequest
                {
                    ProjectIdentifier = project.Id.Value,
                    InvestmentTransactionHex = signedTransactionHex,
                    UnfundedReleaseAddress = releaseAddress,
                };

                var serializedRecoveryRequest = serializer.Serialize(signRecoveryRequest);
                
                var encryptedContent = await encryptionService.EncryptNostrContentAsync(
                    investorNostrPrivateKeyHex,
                    nostrPubKey,
                    serializedRecoveryRequest);

                var (time, id) = signService.RequestInvestmentSigs(encryptedContent, investorNostrPrivateKeyHex, project.NostrPubKey, _ => { });

                return Result.Success((time,id));
            }
            catch (Exception ex)
            {
                return Result.Failure<(DateTime,string)>($"Error while sending the signature request {ex.Message}");
            }
        }

        private async Task<Result<string>> GetUnfundedReleaseAddress(Guid walletId)
        {
            // Get account info from database
            var accountBalanceResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(walletId);
            if (accountBalanceResult.IsFailure)
                return Result.Failure<string>(accountBalanceResult.Error);
            
            var accountInfo = accountBalanceResult.Value.AccountInfo;

            var address = accountInfo.GetNextReceiveAddress();
            if (string.IsNullOrEmpty(address))
                return Result.Failure<string>("Could not get the unfunded release address");

            return Result.Success(address);
        }
    }
}