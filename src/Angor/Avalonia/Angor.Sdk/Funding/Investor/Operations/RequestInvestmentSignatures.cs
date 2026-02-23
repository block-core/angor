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

public static class RequestInvestmentSignatures
{
    public record RequestFounderSignaturesRequest(WalletId WalletId, ProjectId ProjectId, InvestmentDraft Draft) : IRequest<Result<RequestFounderSignaturesResponse>>;

    public record RequestFounderSignaturesResponse(Guid InvestmentId);

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
        IWalletAccountBalanceService walletAccountBalanceService) : IRequestHandler<RequestFounderSignaturesRequest, Result<RequestFounderSignaturesResponse>>
    {
        public async Task<Result<RequestFounderSignaturesResponse>> Handle(RequestFounderSignaturesRequest request, CancellationToken cancellationToken)
        {
            var txnHex = request.Draft.SignedTxHex;
            var network = networkConfiguration.GetNetwork();
            var strippedInvestmentTransaction = network.CreateTransaction(txnHex);
            var transactionId = strippedInvestmentTransaction.GetHash().ToString();
            strippedInvestmentTransaction.Inputs.ForEach(f => f.WitScript = WitScript.Empty);

            var projectResult = await projectService.GetAsync(request.ProjectId);

            if (projectResult.IsFailure)
            {
                return Result.Failure<RequestFounderSignaturesResponse>(projectResult.Error);
            }

            var (investorKey, _) = projectScriptsBuilder.GetInvestmentDataFromOpReturnScript(strippedInvestmentTransaction.Outputs[1].ScriptPubKey);

            var existingInvestment = await Result.Try(() => angorIndexerService.GetInvestmentAsync(request.ProjectId.Value, investorKey));

            if (existingInvestment is { IsSuccess: true, Value: not null })
                return Result.Failure<RequestFounderSignaturesResponse>("An investment with the same key already exists on the blockchain.");

            var sensitiveDataResult = await seedwordsProvider.GetSensitiveData(request.WalletId.Value);

            if (sensitiveDataResult.IsFailure)
            {
                return Result.Failure<RequestFounderSignaturesResponse>(sensitiveDataResult.Error);
            }

            var walletWords = sensitiveDataResult.Value.ToWalletWords();
            var project = projectResult.Value;

            var sendSignatureResult = await SendSignatureRequest(request.WalletId, walletWords, project, strippedInvestmentTransaction.ToHex());

            if (sendSignatureResult.IsFailure)
            {
                return Result.Failure<RequestFounderSignaturesResponse>(sendSignatureResult.Error);
            }

            await portfolioService.AddOrUpdate(request.WalletId.Value, new InvestmentRecord
            {
                InvestmentTransactionHash = transactionId,
                InvestmentTransactionHex = request.Draft.SignedTxHex,
                InvestorPubKey = request.Draft.InvestorKey,
                ProjectIdentifier = request.ProjectId.Value,
                UnfundedReleaseAddress = string.Empty, //TODO: Set this to the actual unfunded release address once implemented
                RequestEventId = sendSignatureResult.Value.eventId,
                RequestEventTime = sendSignatureResult.Value.createdTime,
            });

            // Reserve UTXOs used in this investment transaction
            await ReserveUtxosForInvestment(request.WalletId, request.Draft.SignedTxHex);

            return Result.Success(new RequestFounderSignaturesResponse(Guid.Empty));
        }

        private async Task<Result<(DateTime createdTime, string eventId)>> SendSignatureRequest(WalletId walletId, WalletWords walletWords, Project project, string signedTransactionHex)
        {
            try
            {
                string nostrPubKey = project.NostrPubKey;

                var investorNostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(walletWords, project.FounderKey);
                var investorNostrPrivateKeyHex = Encoders.Hex.EncodeData(investorNostrPrivateKey.ToBytes());
                var releaseAddressResult = await GetUnfundedReleaseAddress(walletId);

                if (releaseAddressResult.IsFailure)
                {
                    return Result.Failure<(DateTime, string)>(releaseAddressResult.Error);
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

                return Result.Success((time, id));
            }
            catch (Exception ex)
            {
                return Result.Failure<(DateTime, string)>($"Error while sending the signature request {ex.Message}");
            }
        }

        private async Task<Result<string>> GetUnfundedReleaseAddress(WalletId walletId)
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

        private async Task ReserveUtxosForInvestment(WalletId walletId, string signedTxHex)
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
                if (!accountBalanceInfo.AccountInfo.UtxoReservedForInvestment.Contains(outpointString))
                {
                    accountBalanceInfo.AccountInfo.UtxoReservedForInvestment.Add(outpointString);
                }
            }

            await walletAccountBalanceService.SaveAccountBalanceInfoAsync(walletId, accountBalanceInfo);
        }
    }
}