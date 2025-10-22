using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Shared;
using Angor.Shared.Protocol;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Contexts.Funding.Investor.Operations;

public static class CheckPenaltyThreshold
{
    public record CheckPenaltyThresholdRequest(ProjectId ProjectId, Amount Amount) : IRequest<Result<bool>>;

    public class CheckPenaltyThresholdHandler(
        IProjectRepository projectRepository,
        IInvestorTransactionActions investorTransactionActions) 
        : IRequestHandler<CheckPenaltyThresholdRequest, Result<bool>>
    {
        public async Task<Result<bool>> Handle(CheckPenaltyThresholdRequest request, CancellationToken cancellationToken)
        {
            try
            {
                // Get the project to access its info
                var projectResult = await projectRepository.GetAsync(request.ProjectId);
                if (projectResult.IsFailure)
                {
                    return Result.Failure<bool>(projectResult.Error);
                }

                var projectInfo = projectResult.Value.ToProjectInfo();
                
                // Use the centralized threshold check logic
                var isAboveThreshold = investorTransactionActions.IsInvestmentAbovePenaltyThreshold(
                    projectInfo, 
                    request.Amount.Sats);

                return Result.Success(isAboveThreshold);
            }
            catch (Exception ex)
            {
                return Result.Failure<bool>($"Error checking penalty threshold: {ex.Message}");
            }
        }
    }
}
