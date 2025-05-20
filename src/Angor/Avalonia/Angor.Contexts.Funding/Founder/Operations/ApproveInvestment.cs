using Angor.Client.Services;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor;

public static class ApproveInvestment
{
    public record ApproveInvestmentRequest(Guid WalletId, ProjectId ProjectId, GetInvestments.Investment InvestmentRequest) : IRequest<Result>;

    public class ApproveInvestmentHandler(
        IProjectRepository projectRepository,
        ISeedwordsProvider seedwordsProvider, 
        IDerivationOperations derivationOperations,
        IEncryptionService encryption,
        ISignService signService,
        ISerializer serializer,
        INetworkConfiguration networkConfiguration,
        IInvestorTransactionActions investorTransactionActions,
        IFounderTransactionActions founderTransactionActions) : IRequestHandler<ApproveInvestmentRequest, Result>
    {
        public async Task<Result> Handle(ApproveInvestmentRequest request, CancellationToken cancellationToken)
        {
            var signatureItem = new SignatureItem()
            {
                EventId = request.InvestmentRequest.NostrEventId,
                SignRecoveryRequest = new SignRecoveryRequest()
                {
                    InvestmentTransactionHex = request.InvestmentRequest.InvestmentTransactionHex,
                },
                investorNostrPubKey = request.InvestmentRequest.InvestorNostrPubKey,
            };
            
            var approvalResult = await from walletWords in seedwordsProvider.GetSensitiveData(request.WalletId)
                from project in projectRepository.Get(request.ProjectId)
                select PerformSignatureApproval(signatureItem, walletWords.ToWalletWords(), project.ToProjectInfo());
            
            return approvalResult;
        }

        private Task<Result> PerformSignatureApproval(SignatureItem signature, WalletWords words, ProjectInfo projectInfo)
        {
            var investmentTransactionHex = signature.SignRecoveryRequest.InvestmentTransactionHex;
            var signatureInvestorNostrPubKey = signature.investorNostrPubKey;
            var signatureEventId = signature.EventId;
            
            return Result.Try(async () =>
            {
                var key = derivationOperations.DeriveFounderRecoveryPrivateKey(words, projectInfo.FounderKey);
                
                var signatureInfo = CreateRecoverySignatures(investmentTransactionHex, projectInfo, Encoders.Hex.EncodeData(key.ToBytes()));

                var sigJson = serializer.Serialize(signatureInfo);

                var nostrPrivateKey = await derivationOperations.DeriveProjectNostrPrivateKeyAsync(words, projectInfo.FounderKey);
                var nostrPrivateKeyHex = Encoders.Hex.EncodeData(nostrPrivateKey.ToBytes());

                
                var encryptedContent = await encryption.EncryptNostrContentAsync(nostrPrivateKeyHex, signatureInvestorNostrPubKey, sigJson);
                
                signService.SendSignaturesToInvestor(encryptedContent, nostrPrivateKeyHex, signatureInvestorNostrPubKey, signatureEventId);
            });
        }

        private SignatureInfo CreateRecoverySignatures(string transactionHex, ProjectInfo info, string founderSigningPrivateKey)
        {
            var investorTrx = networkConfiguration.GetNetwork().CreateTransaction(transactionHex);

            // build sigs
            var recoveryTrx = investorTransactionActions.BuildRecoverInvestorFundsTransaction(info, investorTrx);
            var sig = founderTransactionActions.SignInvestorRecoveryTransactions(info, transactionHex, recoveryTrx, founderSigningPrivateKey);

            if (!investorTransactionActions.CheckInvestorRecoverySignatures(info, investorTrx, sig))
            {
                throw new InvalidOperationException();
            }

            sig.SignatureType = SignatureInfoType.Recovery;

            return sig;
        }
    }

    private class SignatureItem
    {
        public string investorNostrPubKey { get; set; }
        public SignRecoveryRequest? SignRecoveryRequest { get; set; }
        public string EventId { get; set; }
    }
}