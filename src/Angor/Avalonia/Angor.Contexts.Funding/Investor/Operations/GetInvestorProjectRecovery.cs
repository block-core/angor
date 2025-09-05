using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class GetInvestorProjectRecovery
{
    public record GetInvestorProjectRecoveryRequest(Guid WalletId, ProjectId ProjectId) : IRequest<Result<InvestorProjectRecoveryDto>>;

    public class Handler(
        IInvestmentRepository investmentRepository,
        IProjectRepository projectRepository,
        IIndexerService indexerService,
        INetworkConfiguration networkConfiguration,
        IInvestorTransactionActions investorTransactionActions
    ) : IRequestHandler<GetInvestorProjectRecoveryRequest, Result<InvestorProjectRecoveryDto>>
    {
        public Task<Result<InvestorProjectRecoveryDto>> Handle(GetInvestorProjectRecoveryRequest request, CancellationToken cancellationToken)
        {
            // TODO: Implement real logic here
            var now = DateTime.UtcNow;
            var expiry = now.AddDays(60);

            var dto = new InvestorProjectRecoveryDto
            {
                ProjectIdentifier = request.ProjectId.Value,
                Name = "Manage Investment (Test Data)",
                ExpiryDate = expiry,
                PenaltyDays = 90,
                CanRecover = true,
                CanRelease = true,
                EndOfProject = false,
            };

            // 1) Not spent (eligible for Recover)
            dto.Items.Add(new InvestorStageItemDto
            {
                StageIndex = 0,
                Amount = 198_0000,
                IsSpent = false,
                Status = "Not Spent",
                ScriptType = ProjectScriptTypeEnum.Unknown,
            });

            // 2) In penalty (not expired)
            dto.Items.Add(new InvestorStageItemDto
            {
                StageIndex = 1,
                Amount = 594_0000,
                IsSpent = true,
                Status = "Penalty, released in 37.1 days",
                ScriptType = ProjectScriptTypeEnum.InvestorWithPenalty,
            });

            // 3) In penalty (expired)
            dto.Items.Add(new InvestorStageItemDto
            {
                StageIndex = 2,
                Amount = 1_188_0000,
                IsSpent = true,
                Status = "Penalty can be released",
                ScriptType = ProjectScriptTypeEnum.InvestorWithPenalty,
            });

            // 4) Spent by founder
            dto.Items.Add(new InvestorStageItemDto
            {
                StageIndex = 3,
                Amount = 250_0000,
                IsSpent = true,
                Status = "Spent by founder",
                ScriptType = ProjectScriptTypeEnum.Founder,
            });

            // 5) Spent by investor (unfunded release)
            dto.Items.Add(new InvestorStageItemDto
            {
                StageIndex = 4,
                Amount = 350_0000,
                IsSpent = true,
                Status = "Spent by investor",
                ScriptType = ProjectScriptTypeEnum.InvestorNoPenalty,
            });

            dto.TotalSpendable = dto.Items.Where(i => !i.IsSpent).Sum(i => i.Amount);
            dto.TotalInPenalty = dto.Items.Where(i => i.ScriptType == ProjectScriptTypeEnum.InvestorWithPenalty).Sum(i => i.Amount);

            return Task.FromResult(Result.Success(dto));
        }
    }
}
