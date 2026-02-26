using Angor.Sdk.Common;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class NotifyFounderOfCancellation
{
    public record NotifyFounderOfCancellationRequest(
        WalletId WalletId, 
        ProjectId ProjectId, 
        string RequestEventId) : IRequest<Result<NotifyFounderOfCancellationResponse>>;

    public record NotifyFounderOfCancellationResponse(DateTime EventTime, string EventId);

    public class NotifyFounderOfCancellationHandler(
        IProjectService projectService,
        ISeedwordsProvider seedwordsProvider,
        IDerivationOperations derivationOperations,
        ISerializer serializer,
        ISignService signService) : IRequestHandler<NotifyFounderOfCancellationRequest, Result<NotifyFounderOfCancellationResponse>>
    {
        public async Task<Result<NotifyFounderOfCancellationResponse>> Handle(
            NotifyFounderOfCancellationRequest request, 
            CancellationToken cancellationToken)
        {
            var projectResult = await projectService.GetAsync(request.ProjectId);
            if (projectResult.IsFailure)
                return Result.Failure<NotifyFounderOfCancellationResponse>(projectResult.Error);

            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);
            if (sensitiveDataResult.IsFailure)
                return Result.Failure<NotifyFounderOfCancellationResponse>(sensitiveDataResult.Error);

            var walletWords = sensitiveDataResult.Value.ToWalletWords();
            var project = projectResult.Value;

            try
            {
                var investorNostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(
                    walletWords, project.FounderKey);
                var investorNostrPrivateKeyHex = Encoders.Hex.EncodeData(investorNostrPrivateKey.ToBytes());

                var notification = new CancellationNotification
                {
                    ProjectIdentifier = request.ProjectId.Value,
                    RequestEventId = request.RequestEventId
                };

                var content = serializer.Serialize(notification);

                var (eventTime, eventId) = signService.NotifyInvestmentCanceled(
                    content, 
                    investorNostrPrivateKeyHex, 
                    project.NostrPubKey, 
                    _ => { });

                return Result.Success(new NotifyFounderOfCancellationResponse(eventTime, eventId));
            }
            catch (Exception ex)
            {
                return Result.Failure<NotifyFounderOfCancellationResponse>($"Error sending cancellation notification: {ex.Message}");
            }
        }
    }
}

