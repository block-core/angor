using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using Angor.Data.Documents.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Angor.Shared.Utilities;
using NBitcoin;
using NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;
using Angor.Sdk.Funding.Projects;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class BuildRecoveryTransaction
{
    public record BuildRecoveryTransactionRequest(WalletId WalletId, ProjectId ProjectId, DomainFeerate SelectedFeeRate) : IRequest<Result<BuildRecoveryTransactionResponse>>;

    public record BuildRecoveryTransactionResponse(RecoveryTransactionDraft TransactionDraft);

    public class BuildRecoveryTransactionHandler(ISeedwordsProvider provider, IDerivationOperations derivationOperations,
            IProjectService projectService, IInvestorTransactionActions investorTransactionActions,
            IPortfolioService investmentService, INetworkConfiguration networkConfiguration,
            IWalletOperations walletOperations, ISignService signService,
            IEncryptionService decrypter, ISerializer serializer, ITransactionService transactionService,
            IWalletAccountBalanceService walletAccountBalanceService) : IRequestHandler<BuildRecoveryTransactionRequest, Result<BuildRecoveryTransactionResponse>>
    {
        public async Task<Result<BuildRecoveryTransactionResponse>> Handle(BuildRecoveryTransactionRequest request, CancellationToken cancellationToken)
        {
            var words = await provider.GetSensitiveData(request.WalletId.Value);
            if (words.IsFailure)
                return Result.Failure<BuildRecoveryTransactionResponse>(words.Error);

            // Get account info from database
            var accountBalanceResult = await walletAccountBalanceService.GetAccountBalanceInfoAsync(request.WalletId);
            if (accountBalanceResult.IsFailure)
                return Result.Failure<BuildRecoveryTransactionResponse>(accountBalanceResult.Error);

            var accountInfo = accountBalanceResult.Value.AccountInfo;

            var project = await projectService.GetAsync(request.ProjectId);
            if (project.IsFailure)
                return Result.Failure<BuildRecoveryTransactionResponse>(project.Error);
            var investments = await investmentService.GetByWalletId(request.WalletId.Value);
            if (investments.IsFailure)
                return Result.Failure<BuildRecoveryTransactionResponse>(investments.Error);

            var investment = investments.Value.ProjectIdentifiers.FirstOrDefault(p => p.ProjectIdentifier == request.ProjectId.Value);
            if (investment is null)
                return Result.Failure<BuildRecoveryTransactionResponse>("No investment found for this project");

            var investorPrivateKey = derivationOperations.DeriveInvestorPrivateKey(words.Value.ToWalletWords(), project.Value.FounderKey);

            var investmentTransaction = networkConfiguration.GetNetwork().CreateTransaction(investment.InvestmentTransactionHex);

            // Direct investments (below threshold) skip the founder approval handshake,
            // so there are no recovery signatures available. Recovery requires founder signatures
            // that are only generated during the approval flow.
            // Direct investments can use end-of-project claim after the project expires instead.
            if (investment.RequestEventTime is null || investment.RequestEventId is null)
                return Result.Failure<BuildRecoveryTransactionResponse>(
                    "Recovery is not available for direct investments (below threshold). " +
                    "Use end-of-project claim after the project expires, or ask the founder to release funds.");

            var signatureLookup = await LookupFounderSignatures(request.WalletId.Value, project.Value, investment.RequestEventTime.Value, investment.RequestEventId,
              investmentTransaction);

            if (signatureLookup.IsFailure)
                return Result.Failure<BuildRecoveryTransactionResponse>(signatureLookup.Error ?? "Could not retrieve founder signatures");
            if (signatureLookup.Value is null)
                return Result.Failure<BuildRecoveryTransactionResponse>("No founder signatures found");

            var unsignedRecoveryTransaction = investorTransactionActions.AddSignaturesToRecoverSeederFundsTransaction(project.Value.ToProjectInfo(), investmentTransaction, signatureLookup.Value, investorPrivateKey);

            var transactionInfo = await transactionService.GetTransactionInfoByIdAsync(investmentTransaction.GetHash().ToString());

            if (transactionInfo is null)
                return Result.Failure<BuildRecoveryTransactionResponse>("Could not find transaction info");

            var investmentTxId = investmentTransaction.GetHash();
            var spentOutpoints = transactionInfo.Outputs
                .Select((output, i) => new { output, i })
                .Where(x => new Script(Encoders.Hex.DecodeData(x.output.ScriptPubKey)).IsTaprooOutput()
                            && !string.IsNullOrEmpty(x.output.SpentInTransaction))
                .Select(x => new OutPoint(investmentTxId, x.i))
                .ToHashSet();

            for (int i = unsignedRecoveryTransaction.Inputs.Count - 1; i >= 0; i--)
            {
                if (!spentOutpoints.Contains(unsignedRecoveryTransaction.Inputs[i].PrevOut))
                    continue;

                unsignedRecoveryTransaction.Inputs.RemoveAt(i);
                unsignedRecoveryTransaction.Outputs.RemoveAt(i);
            }

            var changeAddress = accountInfo.GetNextChangeReceiveAddress();
            if (changeAddress == null)
                return Result.Failure<BuildRecoveryTransactionResponse>("Could not get a change address");

            // add fee to the recovery trx
            var recoveryTransaction = walletOperations.AddFeeAndSignTransaction(changeAddress, unsignedRecoveryTransaction, words.Value.ToWalletWords(), accountInfo, request.SelectedFeeRate.SatsPerKilobyte);

            return Result.Success(new BuildRecoveryTransactionResponse(new RecoveryTransactionDraft
            {
                SignedTxHex = recoveryTransaction.Transaction.ToHex(),
                TransactionFee = new Amount(recoveryTransaction.TransactionFee),
                TransactionId = recoveryTransaction.Transaction.GetHash().ToString()
            }));
        }

        private async Task<Result<SignatureInfo?>> LookupFounderSignatures(string walletId, Project project, DateTime createdAt, string eventId,
            Transaction investment)
        {
            var sensitiveDataResult = await provider.GetSensitiveData(walletId);
            var pubKey =
                derivationOperations.DeriveNostrPubKey(sensitiveDataResult.Value.ToWalletWords(),
                    project.FounderKey);
            var nostrPrivateKey =
                await derivationOperations.DeriveProjectNostrPrivateKeyAsync(sensitiveDataResult.Value.ToWalletWords(),
                    project.FounderKey);

            var privateKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());

            var signatureInfo = new SignatureInfo();
            var tcs = new TaskCompletionSource<Result<SignatureInfo?>>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            cts.Token.Register(() => { if (!tcs.Task.IsCompleted) tcs.TrySetResult(Result.Success<SignatureInfo?>(null)); });

            var projectPubKey = project.NostrPubKey;

            // The relay sends EOSE immediately after the matching event, but the event handler
            // below does slow async work (decrypt -> deserialize -> full Schnorr validation).
            // SignService invokes the handler fire-and-forget, so without tracking the in-flight
            // handler task the EOSE callback wins the TrySetResult race and a perfectly valid
            // signature is reported as "No founder signatures found". Track the handler task and
            // make EOSE wait for it before concluding that nothing arrived.
            var handlerGate = new object();
            Task? handlerTask = null;

            signService.LookupSignatureForInvestmentRequest(pubKey, projectPubKey, createdAt, eventId,
                content =>
                {
                    var task = HandleSignatureEventAsync(content);
                    lock (handlerGate)
                    {
                        handlerTask = task;
                    }
                    return task;
                },
                () =>
                {
                    Task? inFlight;
                    lock (handlerGate)
                    {
                        inFlight = handlerTask;
                    }

                    if (inFlight is null)
                    {
                        // No event was ever delivered for this subscription — genuinely nothing to find.
                        tcs.TrySetResult(Result.Success<SignatureInfo?>(null));
                        return;
                    }

                    // An event is being processed: let it settle the result first.
                    // TrySetResult means the handler's outcome wins if it got there.
                    inFlight.ContinueWith(
                        _ => tcs.TrySetResult(Result.Success<SignatureInfo?>(null)),
                        TaskScheduler.Default);
                });

            await tcs.Task;

            return tcs.Task.Result;

            async Task HandleSignatureEventAsync(string content)
            {
                try
                {
                    var signatures =
                        await decrypter.DecryptNostrContentAsync(privateKeyHex, projectPubKey, content);

                    signatureInfo = serializer.Deserialize<SignatureInfo>(signatures);

                    var validSignatures =
                        investorTransactionActions.CheckInvestorRecoverySignatures(project.ToProjectInfo(),
                            investment, signatureInfo);

                    tcs.TrySetResult(validSignatures
                        ? Result.Success<SignatureInfo?>(signatureInfo)
                        : Result.Failure<SignatureInfo?>("Invalid signatures"));
                }
                catch (Exception e)
                {
                    // This task is never awaited by SignService, so an unhandled exception here
                    // used to vanish silently and degrade into the misleading 30s-timeout path.
                    tcs.TrySetResult(Result.Failure<SignatureInfo?>(
                        "Failed to process founder signatures: " + e.Message));
                }
            }
        }
    }
}
