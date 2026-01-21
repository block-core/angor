using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Investor.Operations;

/// <summary>
/// Notifies the founder of a below-threshold investment that was published directly to the blockchain.
/// This allows the investment to appear in the founder's investment list with the investor's Nostr pubkey for chat functionality.
/// </summary>
public static class NotifyFounderOfInvestment
{
    public record NotifyFounderOfInvestmentRequest(
        WalletId WalletId, 
        ProjectId ProjectId, 
        InvestmentDraft Draft) : IRequest<Result<NotifyFounderOfInvestmentResponse>>;

    public record NotifyFounderOfInvestmentResponse(DateTime EventTime, string EventId);

    public class NotifyFounderOfInvestmentHandler(
        IProjectService projectService,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        IEncryptionService encryptionService,
        ISerializer serializer,
        ISignService signService) : IRequestHandler<NotifyFounderOfInvestmentRequest, Result<NotifyFounderOfInvestmentResponse>>
    {
        public async Task<Result<NotifyFounderOfInvestmentResponse>> Handle(
            NotifyFounderOfInvestmentRequest request, 
            CancellationToken cancellationToken)
        {
            var projectResult = await projectService.GetAsync(request.ProjectId);
            if (projectResult.IsFailure)
            {
                return Result.Failure<NotifyFounderOfInvestmentResponse>(projectResult.Error);
            }

            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);
            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<NotifyFounderOfInvestmentResponse>(sensitiveDataResult.Error);
            }

            var walletWords = sensitiveDataResult.Value.ToWalletWords();
            var project = projectResult.Value;

            var notificationResult = await SendInvestmentNotification(
                walletWords, 
                project, 
                request.ProjectId.Value,
                request.Draft.TransactionId);

            if (notificationResult.IsFailure)
            {
                return Result.Failure<NotifyFounderOfInvestmentResponse>(notificationResult.Error);
            }

            return Result.Success(new NotifyFounderOfInvestmentResponse(
                notificationResult.Value.eventTime, 
                notificationResult.Value.eventId));
        }

        private async Task<Result<(DateTime eventTime, string eventId)>> SendInvestmentNotification(
            WalletWords walletWords, 
            Project project,
            string projectIdentifier,
            string transactionId)
        {
            try
            {
                var investorNostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(
                    walletWords, 
                    project.FounderKey);
                var investorNostrPrivateKeyHex = Encoders.Hex.EncodeData(investorNostrPrivateKey.ToBytes());

                var notification = new InvestmentNotification
                {
                    ProjectIdentifier = projectIdentifier,
                    TransactionId = transactionId
                };

                var serializedNotification = serializer.Serialize(notification);

                var encryptedContent = await encryptionService.EncryptNostrContentAsync(
                    investorNostrPrivateKeyHex,
                    project.NostrPubKey,
                    serializedNotification);

                var (eventTime, eventId) = signService.NotifyInvestmentCompleted(
                    encryptedContent, 
                    investorNostrPrivateKeyHex, 
                    project.NostrPubKey, 
                    _ => { });

                return Result.Success((eventTime, eventId));
            }
            catch (Exception ex)
            {
                return Result.Failure<(DateTime, string)>($"Error sending investment notification: {ex.Message}");
            }
        }
    }
}

