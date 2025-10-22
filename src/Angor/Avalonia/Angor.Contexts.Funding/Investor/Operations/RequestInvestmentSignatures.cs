using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Shared.TransactionDrafts;
using Angor.Shared;
using Angor.Shared.Models;
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
        IProjectRepository projectRepository,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        IEncryptionService encryptionService,
        INetworkConfiguration networkConfiguration,
        ISerializer serializer,
        IWalletOperations walletOperations,
        ISignService signService,
        IPortfolioRepository portfolioRepository) : IRequestHandler<RequestFounderSignaturesRequest, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(RequestFounderSignaturesRequest request, CancellationToken cancellationToken)
        {
            var txnHex = request.Draft.SignedTxHex;
            var network = networkConfiguration.GetNetwork();
            var strippedInvestmentTransaction = network.CreateTransaction(txnHex);
            var transactionId = strippedInvestmentTransaction.GetHash().ToString();
            strippedInvestmentTransaction.Inputs.ForEach(f => f.WitScript = WitScript.Empty);

            var projectResult = await projectRepository.GetAsync(request.ProjectId);

            if (projectResult.IsFailure)
            {
                return Result.Failure<Guid>(projectResult.Error);
            }

            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(request.WalletId);

            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<Guid>(sensitiveDataResult.Error);
            }

            var walletWords = sensitiveDataResult.Value.ToWalletWords();
            var project = projectResult.Value;

            var sendSignatureResult = await SendSignatureRequest(walletWords, project, strippedInvestmentTransaction.ToHex());

            if (sendSignatureResult.IsFailure)
            {
                return Result.Failure<Guid>(sendSignatureResult.Error);
            }
            
            await portfolioRepository.Add(request.WalletId, new InvestmentRecord
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

        private async Task<Result<(DateTime createdTime,string eventId)>> SendSignatureRequest(WalletWords walletWords, Project project, string signedTransactionHex)
        {
            try
            {
                string nostrPubKey = project.NostrPubKey;

                var investorNostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(walletWords, project.FounderKey);
                var investorNostrPrivateKeyHex = Encoders.Hex.EncodeData(investorNostrPrivateKey.ToBytes());
                var releaseAddressResult = await GetUnfundedReleaseAddress(walletWords);

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