using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class RequestInvestment
{
    public class RequestFounderSignaturesHandler(
        IProjectRepository projectRepository,
        ISeedwordsProvider seedwordsProvider,
        INetworkConfiguration networkConfiguration,
        ISerializer serializer,
        IWalletOperations walletOperations,
        ISignaturesService signatureService) : IRequestHandler<RequestFounderSignaturesRequest, Result>
    {
        public async Task<Result> Handle(RequestFounderSignaturesRequest request, CancellationToken cancellationToken)
        {
            var txnHex = request.Draft.SignedTxHex;
            var network = networkConfiguration.GetNetwork();
            var strippedInvestmentTransaction = network.CreateTransaction(txnHex);
            strippedInvestmentTransaction.Inputs.ForEach(f => f.WitScript = WitScript.Empty);

            var projectResult = await projectRepository.Get(request.ProjectId);

            if (projectResult.IsFailure)
            {
                return Result.Failure<ISignaturesService.EventSendResponse>(projectResult.Error);
            }

            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(request.WalletId);

            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<ISignaturesService.EventSendResponse>(sensitiveDataResult.Error);
            }

            var walletWords = sensitiveDataResult.Value.ToWalletWords();
            var project = projectResult.Value;

            return await SendSignatureRequest(request.WalletId, walletWords, project, strippedInvestmentTransaction.ToHex());
        }

        private async Task<Result<ISignaturesService.EventSendResponse>> SendSignatureRequest(Guid walletId, WalletWords walletWords, Project project, string signedTransactionHex)
        {
            try
            {
                var releaseAddressResult = await GetUnfundedReleaseAddress(walletWords);

                if (releaseAddressResult.IsFailure)
                {
                    return Result.Failure<ISignaturesService.EventSendResponse>(releaseAddressResult.Error);
                }
                
                var releaseAddress = releaseAddressResult.Value;

                var signRecoveryRequest = new SignRecoveryRequest
                {
                    ProjectIdentifier = project.Id.Value,
                    InvestmentTransactionHex = signedTransactionHex,
                    UnfundedReleaseAddress = releaseAddress,
                };

                var key = new KeyIdentifier(walletId, project.NostrPubKey);
                return await signatureService.PostInvestmentRequest(key, serializer.Serialize(signRecoveryRequest),  project.NostrPubKey);

            }
            catch (Exception ex)
            {
                return Result.Failure<ISignaturesService.EventSendResponse>($"Error while sending the signature request {ex.Message}");
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
    
    public class RequestFounderSignaturesRequest(Guid walletId, ProjectId projectId, CreateInvestment.Draft draft) : IRequest<Result>
    {
        public ProjectId ProjectId { get; } = projectId;
        public CreateInvestment.Draft Draft { get; } = draft;
        public Guid WalletId { get; } = walletId;
    }
}